#nullable enable

using System;
using System.Collections.Generic;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Diagnostics;

/// <summary>
/// Detects self intersections inside closed contours: pairs of non-adjacent
/// segments that genuinely cross. Adjacent segments sharing an endpoint are
/// never flagged (that is a normal corner), and the closing segment is
/// adjacent to the first one, so a normal closed loop produces no findings.
///
/// Current scope (phase 1): segment pairs are tested with
/// <see cref="IntersectionEngine"/> which handles line-line only; collinear
/// overlaps are reported as <see cref="LineIntersectionKind.CollinearOverlap"/>
/// and counted as intersections (the overlap is a real geometry defect).
/// Line-arc and arc-arc crossings are not yet evaluated — the finding is
/// limited to straight runs for now (NOT_SUPPORTED_YET for curves).
/// </summary>
public static class SelfIntersectionAnalyzer
{
    /// <summary>
    /// Analyzes every contour (given as an ordered list of entity ids) for
    /// self intersections among its straight segments.
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
        // Collect all straight runs in traversal order, with their source
        // (entity id, segment index) so adjacent runs can be excluded.
        var runs = new List<(long EntityId, int SegmentIndex, Point2 P0, Point2 P1)>();
        foreach (long id in ids)
        {
            if (entityById(id) is not { } e)
            {
                continue;
            }

            switch (e)
            {
                case LineGeometry line:
                    runs.Add((id, 0, line.P0, line.P1));
                    break;
                case PolylineGeometry poly:
                    for (int i = 0; i < poly.Segments.Count; i++)
                    {
                        if (poly.Segments[i] is LineSegment seg)
                        {
                            runs.Add((id, i, seg.StartPoint, seg.EndPoint));
                        }
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

                LineSegmentIntersectionResult x = IntersectionEngine.Intersect(
                    runs[i].P0, runs[i].P1, runs[j].P0, runs[j].P1,
                    tolerance.PointEqualityTolerance);

                switch (x.Kind)
                {
                    case LineIntersectionKind.Point:
                        result.Add(NewFinding(runs[i], runs[j], x.Point, x.Point));
                        break;
                    case LineIntersectionKind.CollinearOverlap:
                        result.Add(NewFinding(runs[i], runs[j], x.Point, x.EndPoint));
                        break;
                }
            }
        }
    }

    private static bool IsAdjacent(
        IReadOnlyList<(long EntityId, int SegmentIndex, Point2 P0, Point2 P1)> runs,
        int i, int j)
    {
        if (Math.Abs(i - j) == 1)
        {
            return true;
        }

        // Closing segment (last) is adjacent to the first.
        return (i == 0 && j == runs.Count - 1) || (j == 0 && i == runs.Count - 1);
    }

    private static GeometryDiagnostic NewFinding(
        (long EntityId, int SegmentIndex, Point2 P0, Point2 P1) a,
        (long EntityId, int SegmentIndex, Point2 P0, Point2 P1) b,
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
