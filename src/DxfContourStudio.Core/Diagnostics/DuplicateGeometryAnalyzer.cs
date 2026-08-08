#nullable enable

using System;
using System.Collections.Generic;
using DxfContourStudio.Core.Geometry;
namespace DxfContourStudio.Core.Diagnostics;

/// <summary>
/// Detects entities that describe the same geometry within
/// <see cref="GeometryTolerance.DuplicateTolerance"/>: identical lines
/// (either direction), identical circles and identical arcs. Findings are
/// diagnostics only — nothing is deleted here. Reversed lines count as
/// duplicates (the geometry is the same set of points).
/// </summary>
public static class DuplicateGeometryAnalyzer
{
    /// <summary>
    /// Returns one diagnostic per duplicate pair (the later entity is reported
    /// against the earlier one). A line and its reversed copy yield exactly
    /// one finding. Candidates are narrowed with a spatial hash over BOTH
    /// endpoints so reversed duplicates (different start points) are still
    /// found; the pass stays near-linear on large disjoint drawings.
    /// </summary>
    public static IReadOnlyList<GeometryDiagnostic> FindDuplicates(
        IReadOnlyList<IGeometryEntity> entities,
        GeometryTolerance tolerance)
    {
        var result = new List<GeometryDiagnostic>();
        double tol = Math.Max(tolerance.DuplicateTolerance, 1e-9);
        var cellIndex = new Dictionary<(long, long), List<int>>();

        // Index every endpoint of every entity so reversed lines (whose start
        // differs from the original's start) share neighbourhood cells.
        for (int i = 0; i < entities.Count; i++)
        {
            foreach (Point2 p in EndpointsOf(entities[i]))
            {
                var (cx, cy) = CellOf(p, tol);
                if (!cellIndex.TryGetValue((cx, cy), out List<int>? bucket))
                {
                    bucket = [];
                    cellIndex[(cx, cy)] = bucket;
                }

                bucket.Add(i);
            }
        }

        for (int i = 0; i < entities.Count; i++)
        {
            var probed = new HashSet<int>();
            foreach (Point2 key in EndpointsOf(entities[i]))
            {
                var (cx, cy) = CellOf(key, tol);
                for (long x = cx - 1; x <= cx + 1; x++)
                {
                    for (long y = cy - 1; y <= cy + 1; y++)
                    {
                        if (cellIndex.TryGetValue((x, y), out List<int>? bucket))
                        {
                            foreach (int j in bucket)
                            {
                                probed.Add(j);
                            }
                        }
                    }
                }
            }

            foreach (int j in probed)
            {
                if (j >= i || !IsDuplicate(entities[i], entities[j], tol))
                {
                    continue;
                }

                result.Add(new GeometryDiagnostic(
                    DiagnosticKind.Duplicate,
                    DiagnosticSeverity.Warning,
                    DiagnosticKeys.Duplicate,
                    entityIdA: entities[i].Id,
                    entityIdB: entities[j].Id,
                    positionA: entities[i].StartPoint));
            }
        }

        return result;
    }

    private static IEnumerable<Point2> EndpointsOf(IGeometryEntity e) => e switch
    {
        LineGeometry l => [l.P0, l.P1],
        CircleGeometry c => [c.Center],
        ArcGeometry a => [a.StartPoint, a.EndPoint, a.Center],
        _ => [e.StartPoint],
    };

    private static (long Cx, long Cy) CellOf(Point2 p, double tol) =>
        ((long)Math.Floor(p.X / tol), (long)Math.Floor(p.Y / tol));

    /// <summary>
    /// True when <paramref name="a"/> and <paramref name="b"/> describe the
    /// same geometry within <paramref name="tol"/>.
    /// </summary>
    public static bool IsDuplicate(IGeometryEntity a, IGeometryEntity b, double tol)
    {
        if (a.GeometryType != b.GeometryType)
        {
            return false;
        }

        return a switch
        {
            LineGeometry la when b is LineGeometry lb => SameLine(la, lb, tol),
            CircleGeometry ca when b is CircleGeometry cb => SameCircle(ca, cb, tol),
            ArcGeometry aa when b is ArcGeometry ab => SameArc(aa, ab, tol),
            _ => false,
        };
    }

    private static bool SameLine(LineGeometry a, LineGeometry b, double tol)
    {
        return (Near(a.P0, b.P0, tol) && Near(a.P1, b.P1, tol)) ||
               (Near(a.P0, b.P1, tol) && Near(a.P1, b.P0, tol));
    }

    private static bool SameCircle(CircleGeometry a, CircleGeometry b, double tol)
    {
        return Near(a.Center, b.Center, tol) && Math.Abs(a.Radius - b.Radius) <= tol;
    }

    private static bool SameArc(ArcGeometry a, ArcGeometry b, double tol)
    {
        return Near(a.Center, b.Center, tol) &&
               Math.Abs(a.Radius - b.Radius) <= tol &&
               Math.Abs(MathUtil.AngularDifference(a.StartAngleRadians, b.StartAngleRadians)) <= tol &&
               Math.Abs(Math.Abs(a.SweepRadians) - Math.Abs(b.SweepRadians)) <= tol;
    }

    private static bool Near(Point2 a, Point2 b, double tol) => a.DistanceTo(b) <= tol;
}
