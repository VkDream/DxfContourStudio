#nullable enable

using System;
using System.Collections.Generic;

namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// Pure curve splitting: projects a world point onto a path (Line, Arc or
/// Polyline), maps it to a path-relative parameter in [0,1] and splits the
/// entity into two same-kind pieces at that parameter.
///
/// Contract (docs/ADR/ADR-012-Break-Semantics.md):
/// - the cutting point must lie on the curve within <c>tolerance</c>
///   (TryProjectParameter fails otherwise);
/// - parameter 0 and 1 (endpoints) never split;
/// - splitting a polyline cuts the run containing the parameter; both pieces
///   stay polylines with their exact runs, the two halves of the cut run are
///   LineSegment/ArcSegment pieces;
/// - a closed polyline becomes two open polylines (both start/end at the cut);
/// - Id policy: the caller assigns the ids (left keeps the original id, right
///   gets a fresh one, see ADR-013).
/// </summary>
public static class PathBreaker
{
    /// <summary>
    /// Projects <paramref name="point"/> onto <paramref name="entity"/>.
    /// Returns the path parameter t ∈ [0,1] and the projected point when the
    /// nearest distance is within <paramref name="tolerance"/>.
    /// </summary>
    public static bool TryProjectParameter(IGeometryEntity entity, Point2 point, double tolerance, out double t, out Point2 projected)
    {
        t = -1;
        projected = default;

        switch (entity)
        {
            case LineGeometry line:
                return ProjectLine(line.P0, line.P1, point, tolerance, out t, out projected);

            case ArcGeometry arc:
                return ProjectArc(arc, point, tolerance, out t, out projected);

            case PolylineGeometry poly:
                return ProjectPolyline(poly, point, tolerance, out t, out projected);

            default:
                return false;
        }
    }

    /// <summary>
    /// Splits the entity at path parameter t (strictly inside (0,1)).
    /// Returns null when the parameter is out of range or the entity kind is
    /// unsupported. The pieces are new entities with the supplied ids; they
    /// keep the source layer and visibility.
    /// </summary>
    public static (IGeometryEntity left, IGeometryEntity right)? SplitEntity(IGeometryEntity entity, double t, long leftId, long rightId)
    {
        if (t <= 0 || t >= 1)
        {
            return null;
        }

        return entity switch
        {
            LineGeometry line => SplitLine(line, t, leftId, rightId),
            ArcGeometry arc => SplitArc(arc, t, leftId, rightId),
            PolylineGeometry polyline => SplitPolyline(polyline, t, leftId, rightId),
            _ => null,
        };
    }

    private static (IGeometryEntity, IGeometryEntity)? SplitLine(LineGeometry line, double t, long leftId, long rightId)
    {
        Point2 cut = line.PointAtParameter(t);
        return (
            new LineGeometry(leftId, line.LayerName, line.P0, cut, line.IsVisible),
            new LineGeometry(rightId, line.LayerName, cut, line.P1, line.IsVisible));
    }

    private static (IGeometryEntity, IGeometryEntity)? SplitArc(ArcGeometry arc, double t, long leftId, long rightId)
    {
        double splitAngle = arc.StartAngleRadians + t * arc.SweepRadians;
        var left = new ArcGeometry(leftId, arc.LayerName, arc.Center, arc.Radius,
            arc.StartAngleRadians, t * arc.SweepRadians, arc.IsCounterClockwise, arc.IsVisible);
        var right = new ArcGeometry(rightId, arc.LayerName, arc.Center, arc.Radius,
            splitAngle, (1 - t) * arc.SweepRadians, arc.IsCounterClockwise, arc.IsVisible);
        return (left, right);
    }

    private static (IGeometryEntity, IGeometryEntity) SplitPolyline(PolylineGeometry poly, double t, long leftId, long rightId)
    {
        var runs = poly.Segments;
        (int runIndex, double localT) = LocateRun(runs, t);

        var leftRuns = new List<IPathSegment>(runIndex + 1);
        var rightRuns = new List<IPathSegment>(runs.Count - runIndex);
        for (int i = 0; i < runIndex; i++)
        {
            leftRuns.Add(runs[i]);
        }

        (var leftPiece, var rightPiece) = SplitRun(runs[runIndex], localT);
        leftRuns.Add(leftPiece);
        rightRuns.Add(rightPiece);
        for (int i = runIndex + 1; i < runs.Count; i++)
        {
            rightRuns.Add(runs[i]);
        }

        var leftPoly = new PolylineGeometry(leftId, poly.LayerName, leftRuns, isClosed: false, poly.IsVisible);
        var rightPoly = new PolylineGeometry(rightId, poly.LayerName, rightRuns, isClosed: false, poly.IsVisible);
        return (leftPoly, rightPoly);
    }

    /// <summary>Maps a path parameter t to the owning run and its local t.</summary>
    private static (int runIndex, double localT) LocateRun(IReadOnlyList<IPathSegment> runs, double t)
    {
        double totalLen = 0;
        foreach (var run in runs)
        {
            totalLen += run.Length;
        }

        if (totalLen <= 0)
        {
            return (runs.Count - 1, 0);
        }

        double target = Math.Clamp(t, 0, 1) * totalLen;
        double cum = 0;
        for (int i = 0; i < runs.Count; i++)
        {
            double len = runs[i].Length;
            if (target <= cum + len || i == runs.Count - 1)
            {
                double local = len > 0 ? Math.Clamp((target - cum) / len, 0, 1) : 0;
                return (i, local);
            }

            cum += len;
        }

        return (runs.Count - 1, 1);
    }

    /// <summary>Splits one run at the local parameter; both pieces keep the run type.</summary>
    private static (IPathSegment, IPathSegment) SplitRun(IPathSegment run, double t)
    {
        return run switch
        {
            LineSegment l => (new LineSegment(l.StartPoint, l.PointAtParameter(t)),
                              new LineSegment(l.PointAtParameter(t), l.EndPoint)),
            ArcSegment a => (new ArcSegment(a.Center, a.Radius, a.StartAngleRadians, t * a.SweepRadians, a.IsCounterClockwise),
                             new ArcSegment(a.Center, a.Radius, a.StartAngleRadians + t * a.SweepRadians, (1 - t) * a.SweepRadians, a.IsCounterClockwise)),
            _ => (run, run),
        };
    }

    private static bool ProjectLine(Point2 a, Point2 b, Point2 p, double tolerance, out double t, out Point2 projected)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lenSq = dx * dx + dy * dy;
        if (lenSq <= double.Epsilon)
        {
            t = 0;
            projected = a;
            return a.DistanceTo(p) <= tolerance;
        }

        t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0, 1);
        projected = new Point2(a.X + t * dx, a.Y + t * dy);
        return projected.DistanceTo(p) <= tolerance;
    }

    private static bool ProjectArc(ArcGeometry arc, Point2 p, double tolerance, out double t, out Point2 projected)
    {
        // Parameterize by directed angular offset, mirroring PointAtParameter.
        double ang = Math.Atan2(p.Y - arc.Center.Y, p.X - arc.Center.X);
        double delta = arc.IsCounterClockwise
            ? WrapPositive(ang - arc.StartAngleRadians)
            : WrapPositive(arc.StartAngleRadians - ang);
        double effSweep = Math.Abs(arc.SweepRadians);
        if (effSweep <= 1e-12)
        {
            t = 0;
            projected = arc.StartPoint;
            return arc.StartPoint.DistanceTo(p) <= tolerance;
        }

        t = Math.Clamp(delta / effSweep, 0, 1);
        projected = arc.PointAtParameter(t);
        return projected.DistanceTo(p) <= tolerance;
    }

    private static bool ProjectPolyline(PolylineGeometry poly, Point2 p, double tolerance, out double t, out Point2 projected)
    {
        t = -1;
        projected = default;
        double bestDist = double.PositiveInfinity;
        double totalLen = 0;
        foreach (var run in poly.Segments)
        {
            totalLen += run.Length;
        }

        if (totalLen <= 0)
        {
            return false;
        }

        double cum = 0;
        foreach (var run in poly.Segments)
        {
            bool ok = ProjectRun(run, p, tolerance, out double localT, out double localDist);
            if (ok && localDist < bestDist)
            {
                bestDist = localDist;
                t = (cum + localT * run.Length) / totalLen;
                projected = run.PointAtParameter(localT);
            }

            if (bestDist <= tolerance && bestDist < 1e-12)
            {
                break;
            }

            cum += run.Length;
        }

        return bestDist <= tolerance;
    }

    private static bool ProjectRun(IPathSegment run, Point2 p, double tolerance, out double localT, out double distance)
    {
        localT = 0;
        distance = double.PositiveInfinity;

        switch (run)
        {
            case LineSegment l:
            {
                double dx = l.EndPoint.X - l.StartPoint.X;
                double dy = l.EndPoint.Y - l.StartPoint.Y;
                double lenSq = dx * dx + dy * dy;
                if (lenSq <= double.Epsilon)
                {
                    break;
                }

                localT = Math.Clamp(((p.X - l.StartPoint.X) * dx + (p.Y - l.StartPoint.Y) * dy) / lenSq, 0, 1);
                distance = l.PointAtParameter(localT).DistanceTo(p);
                return distance <= tolerance;
            }

            case ArcSegment a:
            {
                double effSweep = Math.Abs(a.SweepRadians);
                if (effSweep <= 1e-12)
                {
                    break;
                }

                double ang = Math.Atan2(p.Y - a.Center.Y, p.X - a.Center.X);
                double delta = a.IsCounterClockwise
                    ? WrapPositive(ang - a.StartAngleRadians)
                    : WrapPositive(a.StartAngleRadians - ang);
                localT = Math.Clamp(delta / effSweep, 0, 1);
                distance = a.PointAtParameter(localT).DistanceTo(p);
                return distance <= tolerance;
            }

            default:
                return false;
        }

        return false;
    }

    /// <summary>Normalizes an angle difference into [0, 2π).</summary>
    private static double WrapPositive(double angle)
    {
        angle %= MathUtil.TwoPi;
        return angle < 0 ? angle + MathUtil.TwoPi : angle;
    }
}