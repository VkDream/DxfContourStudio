#nullable enable

using System;
using System.Collections.Generic;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Diagnostics;

/// <summary>
/// Detects self intersections inside closed contours: pairs of non-adjacent
/// runs that genuinely cross. Adjacent runs sharing an endpoint are never
/// flagged (that is a normal corner), and the closing run is adjacent to the
/// first one, so a normal closed loop produces no findings.
///
/// Supported run kinds (milestone D2): straight runs (LineGeometry and the
/// LineSegment runs of a polyline) AND arc runs (ArcGeometry and the
/// ArcSegment runs of a bulged polyline). Every pair is tested with the
/// IntersectionEngine 2.0 semantics:
/// - <see cref="CurveIntersectionKind.Point"/> / TwoPoints — a real crossing
///   point; reported as an Error finding.
/// - <see cref="CurveIntersectionKind.Overlap"/> / CollinearOverlap — a shared
///   interval; reported as an Error finding spanning the overlap.
/// - <see cref="CurveIntersectionKind.Coincident"/> — identical geometry;
///   reported as a finding.
/// - <see cref="CurveIntersectionKind.Tangent"/> — a geometric tangency is a
///   touch, not a crossing; deliberately NOT reported (a tangent does not
///   split the loop into a degenerate self-intersecting polygon).
/// - Parallel / None — no contact, no finding.
/// </summary>
public static class SelfIntersectionAnalyzer
{
    /// <summary>
    /// Analyzes every contour (given as an ordered list of entity ids) for
    /// self intersections among its straight and arc runs.
    /// </summary>
    public static IReadOnlyList<GeometryDiagnostic> Analyze(
        IReadOnlyList<IReadOnlyList<long>> contoursInOrder,
        Func<long, IGeometryEntity> entityById,
        GeometryTolerance tolerance)
    {
        var result = new List<GeometryDiagnostic>();
        foreach (IReadOnlyList<long> contourIds in contoursInOrder)
        {
            AnalyzeContour(contourIds, entityById, tolerance, result);
        }

        return result;
    }

    private static void AnalyzeContour(
        IReadOnlyList<long> ids,
        Func<long, IGeometryEntity> entityById,
        GeometryTolerance tolerance,
        List<GeometryDiagnostic> result)
    {
        // Collect all runs (line and arc) in traversal order, with their
        // source (entity id, segment index) so adjacent runs can be excluded.
        var runs = new List<(long EntityId, int SegmentIndex, IPathSegment Segment)>();
        foreach (long id in ids)
        {
            if (entityById(id) is not { } e)
            {
                continue;
            }

            switch (e)
            {
                case LineGeometry line:
                    runs.Add((id, 0, new LineSegment(line.StartPoint, line.EndPoint)));
                    break;
                case ArcGeometry arc:
                    runs.Add((id, 0,
                        new ArcSegment(arc.Center, arc.Radius, arc.StartAngleRadians, arc.SweepRadians, arc.IsCounterClockwise)));
                    break;
                case PolylineGeometry poly:
                    for (int i = 0; i < poly.Segments.Count; i++)
                    {
                        runs.Add((id, i, poly.Segments[i]));
                    }

                    break;
            }
        }

        int n = runs.Count;
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                if (IsAdjacent(runs, i, j))
                {
                    continue;
                }

                CurveIntersectionResult x = IntersectionEngine.IntersectRuns(
                    runs[i].Segment, runs[j].Segment, tolerance);

                switch (x.Kind)
                {
                    case CurveIntersectionKind.Point:
                        result.Add(NewFinding(runs[i], runs[j], x.Point1, x.Point1));
                        break;
                    case CurveIntersectionKind.TwoPoints:
                        result.Add(NewFinding(runs[i], runs[j], x.Point1, x.Point2));
                        break;
                    case CurveIntersectionKind.Overlap:
                        result.Add(NewFinding(runs[i], runs[j], x.Point1, x.Point2));
                        break;
                    case CurveIntersectionKind.Coincident:
                        result.Add(NewFinding(runs[i], runs[j], x.Point1, x.Point2));
                        break;
                    // Tangent / Parallel / None / Degenerate: no finding.
                }
            }
        }
    }

    private static bool IsAdjacent(
        IReadOnlyList<(long EntityId, int SegmentIndex, IPathSegment Segment)> runs,
        int i, int j)
    {
        if (Math.Abs(i - j) == 1)
        {
            return true;
        }

        // Closing run (last) is adjacent to the first.
        return (i == 0 && j == runs.Count - 1) || (j == 0 && i == runs.Count - 1);
    }

    private static GeometryDiagnostic NewFinding(
        (long EntityId, int SegmentIndex, IPathSegment Segment) a,
        (long EntityId, int SegmentIndex, IPathSegment Segment) b,
        Point2 point, Point2 end)
    {
        return new GeometryDiagnostic(
            DiagnosticKind.SelfIntersection,
            DiagnosticSeverity.Error,
            DiagnosticKeys.SelfIntersection,
            entityIdA: a.EntityId,
            entityIdB: b.EntityId,
            positionA: point,
            positionB: end);
    }
}