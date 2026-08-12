#nullable enable

using System;

namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// The kind of relationship found between two line segments.
/// </summary>
public enum LineIntersectionKind
{
    /// <summary>The segments do not touch (or are degenerate and ignored).</summary>
    None,

    /// <summary>The segments cross or touch at exactly one point.</summary>
    Point,

    /// <summary>The segments are parallel (possibly collinear). No single point.</summary>
    Parallel,

    /// <summary>The segments are collinear and overlap (shared interval).</summary>
    CollinearOverlap,
}

/// <summary>
/// Result of intersecting two line segments. For <see cref="LineIntersectionKind.Point"/>
/// <see cref="Point"/> is the intersection; for <see cref="LineIntersectionKind.CollinearOverlap"/>
/// <see cref="Point"/> is the start of the shared interval (toward segment A's
/// direction) and <see cref="Point2"/> the end.
/// </summary>
public readonly record struct LineSegmentIntersectionResult(
    LineIntersectionKind Kind,
    Point2 Point,
    Point2 EndPoint)
{
    public static LineSegmentIntersectionResult None => new(LineIntersectionKind.None, Point2.Origin, Point2.Origin);

    public static LineSegmentIntersectionResult At(Point2 p) =>
        new(LineIntersectionKind.Point, p, p);

    public static LineSegmentIntersectionResult Parallel() =>
        new(LineIntersectionKind.Parallel, Point2.Origin, Point2.Origin);

    public static LineSegmentIntersectionResult Overlap(Point2 a, Point2 b) =>
        new(LineIntersectionKind.CollinearOverlap, a, b);
}

/// <summary>
/// Pure math for intersecting two finite 2D line segments. No WPF, no DXF —
/// this is the single source of truth for "do these two runs cross?" used by
/// the self-intersection analyzer and (later) line-arc / arc-arc variants.
///
/// The implementation is the standard parametric segment-segment test with an
/// epsilon guard derived from <see cref="GeometryTolerance.PointEqualityTolerance"/>
/// so that touching endpoints count as a <see cref="LineIntersectionKind.Point"/>
/// only when the caller asks for it (the self-intersection analyzer explicitly
/// excludes adjacent-shared endpoints).
/// </summary>
public static partial class IntersectionEngine
{
    /// <summary>
    /// Intersects segment [<paramref name="a0"/>, <paramref name="a1"/>] with
    /// [<paramref name="b0"/>, <paramref name="b1"/>] under the given tolerance.
    /// </summary>
    public static LineSegmentIntersectionResult Intersect(
        Point2 a0, Point2 a1, Point2 b0, Point2 b1,
        double tolerance)
    {
        double eps = Math.Max(tolerance, 1e-12);

        double rX = a1.X - a0.X;
        double rY = a1.Y - a0.Y;
        double sX = b1.X - b0.X;
        double sY = b1.Y - b0.Y;

        double denom = rX * sY - rY * sX;
        double qX = b0.X - a0.X;
        double qY = b0.Y - a0.Y;

        // Degenerate inputs: a zero-length segment cannot produce a crossing.
        double lenA2 = rX * rX + rY * rY;
        double lenB2 = sX * sX + sY * sY;
        if (lenA2 <= eps * eps || lenB2 <= eps * eps)
        {
            return LineSegmentIntersectionResult.None;
        }

        if (Math.Abs(denom) <= eps * Math.Max(lenA2, lenB2))
        {
            // Parallel (or collinear). Check collinearity of the endpoints.
            double crossQ = qX * rY - qY * rX;
            if (Math.Abs(crossQ) > eps * Math.Sqrt(lenA2))
            {
                return LineSegmentIntersectionResult.Parallel();
            }

            // Collinear: project b onto the line of a and find overlap.
            double t0 = ((b0.X - a0.X) * rX + (b0.Y - a0.Y) * rY) / lenA2;
            double t1 = ((b1.X - a0.X) * rX + (b1.Y - a0.Y) * rY) / lenA2;
            if (t0 > t1)
            {
                (t0, t1) = (t1, t0);
            }

            // Overlap exists when the projected interval intersects [0,1].
            double lo = Math.Max(t0, 0.0);
            double hi = Math.Min(t1, 1.0);
            if (lo > hi + eps)
            {
                return LineSegmentIntersectionResult.Parallel();
            }

            if (hi - lo <= eps)
            {
                // Touching at a single point along the collinear line.
                return LineSegmentIntersectionResult.At(new Point2(a0.X + lo * rX, a0.Y + lo * rY));
            }

            Point2 start = new(a0.X + lo * rX, a0.Y + lo * rY);
            Point2 end = new(a0.X + hi * rX, a0.Y + hi * rY);
            return LineSegmentIntersectionResult.Overlap(start, end);
        }

        // Non-parallel: solve for the intersection parameters.
        double t = (qX * sY - qY * sX) / denom;
        double u = (qX * rY - qY * rX) / denom;

        double pad = eps;
        if (t >= -pad && t <= 1.0 + pad && u >= -pad && u <= 1.0 + pad)
        {
            double tc = Math.Clamp(t, 0.0, 1.0);
            return LineSegmentIntersectionResult.At(new Point2(a0.X + tc * rX, a0.Y + tc * rY));
        }

        return LineSegmentIntersectionResult.None;
    }
}
