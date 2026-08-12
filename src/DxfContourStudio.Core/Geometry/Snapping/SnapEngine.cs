#nullable enable

using System;
using System.Collections.Generic;

namespace DxfContourStudio.Core.Geometry;

/// <summary>The kind of snap point a <see cref="SnapEngine"/> produced.</summary>
public enum SnapKind
{
    /// <summary>Entity endpoint (line ends, arc ends). Circles have none.</summary>
    Endpoint = 1,

    /// <summary>Midpoint of a line run or arc run.</summary>
    Midpoint = 2,

    /// <summary>Center of an arc or circle.</summary>
    Center = 4,

    /// <summary>Intersection point of two entities.</summary>
    Intersection = 8,

    /// <summary>Nearest point on an entity (projection, no "interesting" snap).</summary>
    Nearest = 16,
}

/// <summary>Set of enabled snap kinds (bit flags).</summary>
[Flags]
public enum SnapKinds
{
    None = 0,
    Endpoint = SnapKind.Endpoint,
    Midpoint = SnapKind.Midpoint,
    Center = SnapKind.Center,
    Intersection = SnapKind.Intersection,
    Nearest = SnapKind.Nearest,

    /// <summary>The default set used by the UI when "snap" is enabled.</summary>
    Default = Endpoint | Midpoint | Center | Intersection | Nearest,
}

/// <summary>
/// A candidate snap point. <see cref="IsValid"/> distinguishes "no snap"
/// from a real result; the UI shows the marker and uses
/// <see cref="WorldPoint"/>.
/// </summary>
/// <param name="IsValid">False for <see cref="None"/>.</param>
/// <param name="Kind">Which kind produced this point.</param>
/// <param name="WorldPoint">The snap location in world (drawing) coordinates.</param>
/// <param name="SourceEntityIds">The entity id(s) that produced the point
/// (two ids for an Intersection snap).</param>
/// <param name="DistanceWorld">Distance from the query point in mm.</param>
/// <param name="DistanceScreen">Optional distance in pixels; filled by the
/// caller when a viewport transform is available (-1 when unknown).</param>
public readonly record struct SnapResult(
    bool IsValid,
    SnapKind Kind,
    Point2 WorldPoint,
    IReadOnlyList<long> SourceEntityIds,
    double DistanceWorld,
    double DistanceScreen = -1)
{
    public static SnapResult None => new(false, SnapKind.Endpoint, Point2.Origin, [], double.MaxValue, -1);

    public static SnapResult At(SnapKind kind, Point2 worldPoint, IReadOnlyList<long> ids, double distanceWorld, double distanceScreen = -1)
        => new(true, kind, worldPoint, ids, distanceWorld, distanceScreen);
}

/// <summary>
/// World-space snap point search over a candidate set of entities. Pure math —
/// no WPF dependency. The caller supplies:
///  - <paramref name="candidates"/>: the entities near the cursor. A spatial
///    index (milestone D10) or the viewport culling layer should pre-filter
///    this set; the engine itself treats it as authoritative (it never scans
///    the full document behind the caller's back).
///  - <paramref name="worldTolerance"/>: the snap radius converted from pixels
///    via the viewport transform (e.g. PixelsToWorld). Snap engine stays
///    zoom-independent by design.
///
/// Priority (lowest wins): Endpoint &gt; Intersection &gt; Center &gt; Midpoint
/// &gt; Nearest. Within the same kind the closest point wins. Nearest is the
/// fallback: it only matches if the projected point is inside the tolerance.
/// </summary>
public static class SnapEngine
{
    private static readonly SnapKind[] KindPriority =
    [
        SnapKind.Endpoint, SnapKind.Intersection, SnapKind.Center, SnapKind.Midpoint, SnapKind.Nearest,
    ];

    /// <summary>
    /// Finds the best snap point within <paramref name="worldTolerance"/>.
    /// Returns <see cref="SnapResult.None"/> when nothing qualifies.
    /// Kinds are evaluated in priority order (Endpoint first); the first kind
    /// with any in-tolerance candidate wins, and within that kind the closest
    /// point is returned. Nearest is therefore only a fallback — it cannot
    /// outrank a farther endpoint.
    /// </summary>
    public static SnapResult Snap(
        IReadOnlyList<IGeometryEntity> candidates,
        Point2 queryPoint,
        double worldTolerance,
        GeometryTolerance tolerance,
        SnapKinds enabledKinds = SnapKinds.Default)
    {
        if (candidates.Count == 0 || worldTolerance <= 0)
        {
            return SnapResult.None;
        }

        foreach (var kind in KindPriority)
        {
            if (!enabledKinds.HasFlag((SnapKinds)kind))
            {
                continue;
            }

            SnapResult? bestOfKind = null;
            foreach (var candidate in Find(queryPoint, candidates, kind, tolerance))
            {
                if (candidate.DistanceWorld <= worldTolerance &&
                    (bestOfKind is null || candidate.DistanceWorld < bestOfKind.Value.DistanceWorld))
                {
                    bestOfKind = candidate;
                }
            }

            if (bestOfKind is { } found)
            {
                return found;
            }
        }

        return SnapResult.None;
    }

    private static IEnumerable<SnapResult> Find(
        Point2 query, IReadOnlyList<IGeometryEntity> candidates, SnapKind kind, GeometryTolerance tolerance)
    {
        switch (kind)
        {
            case SnapKind.Endpoint:
                foreach (var e in candidates)
                {
                    if (e is CircleGeometry)
                    {
                        continue; // Circles have no endpoints.
                    }

                    foreach (var run in RunsOf(e))
                    {
                        yield return SnapResult.At(kind, run.StartPoint, [e.Id], query.DistanceTo(run.StartPoint));
                        yield return SnapResult.At(kind, run.EndPoint, [e.Id], query.DistanceTo(run.EndPoint));
                    }
                }

                break;

            case SnapKind.Midpoint:
                foreach (var e in candidates)
                {
                    if (e is CircleGeometry)
                    {
                        continue; // Circles have no midpoint.
                    }

                    foreach (var run in RunsOf(e))
                    {
                        Point2 m = run.PointAtParameter(0.5);
                        yield return SnapResult.At(kind, m, [e.Id], query.DistanceTo(m));
                    }
                }

                break;

            case SnapKind.Center:
                foreach (var e in candidates)
                {
                    Point2? c = e switch
                    {
                        ArcGeometry arc => arc.Center,
                        CircleGeometry circle => circle.Center,
                        _ => null,
                    };
                    if (c is { } center)
                    {
                        yield return SnapResult.At(kind, center, [e.Id], query.DistanceTo(center));
                    }
                }

                break;

            case SnapKind.Intersection:
                // O(candidates²) pair test; the caller is expected to pass a
                // spatially narrowed candidate set. D10 plugs the index here.
                for (int i = 0; i < candidates.Count; i++)
                {
                    for (int j = i + 1; j < candidates.Count; j++)
                    {
                        var a = candidates[i];
                        var b = candidates[j];
                        var r = IntersectionEngine.IntersectCurves(a, b, tolerance);
                        if (r.Kind is not (CurveIntersectionKind.Point or CurveIntersectionKind.TwoPoints))
                        {
                            continue;
                        }

                        foreach (var p in r.Points)
                        {
                            yield return SnapResult.At(kind, p, [a.Id, b.Id], query.DistanceTo(p));
                        }
                    }
                }

                break;

            default: // Nearest
                foreach (var e in candidates)
                {
                    foreach (var run in RunsOf(e))
                    {
                        Point2 n = NearestOnRun(query, run);
                        yield return SnapResult.At(kind, n, [e.Id], query.DistanceTo(n));
                    }
                }

                break;
        }
    }

    private static Point2 NearestOnRun(Point2 query, IPathSegment run)
    {
        return run switch
        {
            LineSegment l => NearestOnLine(query, l),
            ArcSegment a => NearestOnArc(query, a),
            _ => run.StartPoint,
        };
    }

    private static Point2 NearestOnLine(Point2 query, LineSegment l)
    {
        double dx = l.EndPoint.X - l.StartPoint.X;
        double dy = l.EndPoint.Y - l.StartPoint.Y;
        double len2 = dx * dx + dy * dy;
        if (len2 <= 0)
        {
            return l.StartPoint;
        }

        double t = ((query.X - l.StartPoint.X) * dx + (query.Y - l.StartPoint.Y) * dy) / len2;
        t = MathUtil.Clamp(t, 0.0, 1.0);
        return new Point2(l.StartPoint.X + t * dx, l.StartPoint.Y + t * dy);
    }

    private static Point2 NearestOnArc(Point2 query, ArcSegment a)
    {
        double angle = Math.Atan2(query.Y - a.Center.Y, query.X - a.Center.X);
        double sweepLen = Math.Abs(a.SweepRadians);
        double rel = a.IsCounterClockwise
            ? MathUtil.Normalize0To2Pi(angle - a.StartAngleRadians)
            : MathUtil.Normalize0To2Pi(a.StartAngleRadians - angle);
        if (rel > sweepLen)
        {
            // Outside the sweep: clamp to the nearest arc endpoint.
            return query.DistanceTo(a.StartPoint) <= query.DistanceTo(a.EndPoint)
                ? a.StartPoint
                : a.EndPoint;
        }

        return new Point2(
            a.Center.X + a.Radius * Math.Cos(angle),
            a.Center.Y + a.Radius * Math.Sin(angle));
    }

    private static IReadOnlyList<IPathSegment> RunsOf(IGeometryEntity entity) => entity switch
    {
        LineGeometry l => [new LineSegment(l.StartPoint, l.EndPoint)],
        ArcGeometry arc => [new ArcSegment(arc.Center, arc.Radius, arc.StartAngleRadians, arc.SweepRadians, arc.IsCounterClockwise)],
        CircleGeometry c => [new ArcSegment(c.Center, c.Radius, 0, MathUtil.TwoPi, true)],
        PolylineGeometry p when p.Segments.Count > 0 => p.Segments,
        _ => [],
    };
}