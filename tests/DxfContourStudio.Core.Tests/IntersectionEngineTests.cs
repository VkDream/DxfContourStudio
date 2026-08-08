#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Tests;

/// <summary>
/// Core tests for the line-line intersection engine: cross, touch at
/// endpoint, parallel, collinear overlap, near-tolerance, and axis-aligned
/// special cases.
/// </summary>
public class IntersectionEngineTests
{
    private static readonly GeometryTolerance Tol = GeometryTolerance.Default;

    private static LineSegmentIntersectionResult Intersect(Point2 a0, Point2 a1, Point2 b0, Point2 b1) =>
        IntersectionEngine.Intersect(a0, a1, b0, b1, Tol.PointEqualityTolerance);

    [Fact]
    public void Cross_Segments_XShape_ReturnsPoint()
    {
        // X crossing at (50,50).
        LineSegmentIntersectionResult r = Intersect(
            new Point2(0, 0), new Point2(100, 100),
            new Point2(0, 100), new Point2(100, 0));
        Assert.Equal(LineIntersectionKind.Point, r.Kind);
        Assert.Equal(50, r.Point.X, 6);
        Assert.Equal(50, r.Point.Y, 6);
    }

    [Fact]
    public void Cross_HorizontalVertical_ReturnsPoint()
    {
        LineSegmentIntersectionResult r = Intersect(
            new Point2(0, 25), new Point2(100, 25),
            new Point2(50, 0), new Point2(50, 100));
        Assert.Equal(LineIntersectionKind.Point, r.Kind);
        Assert.Equal(50, r.Point.X, 6);
        Assert.Equal(25, r.Point.Y, 6);
    }

    [Fact]
    public void Touch_SharedEndpoint_ReturnsPoint()
    {
        // A ends exactly where B starts: (100,0).
        LineSegmentIntersectionResult r = Intersect(
            new Point2(0, 0), new Point2(100, 0),
            new Point2(100, 0), new Point2(100, 60));
        Assert.Equal(LineIntersectionKind.Point, r.Kind);
        Assert.Equal(100, r.Point.X, 6);
        Assert.Equal(0, r.Point.Y, 6);
    }

    [Fact]
    public void Parallel_Disjoint_ReturnsParallel()
    {
        LineSegmentIntersectionResult r = Intersect(
            new Point2(0, 0), new Point2(100, 0),
            new Point2(0, 10), new Point2(100, 10));
        Assert.Equal(LineIntersectionKind.Parallel, r.Kind);
    }

    [Fact]
    public void Parallel_Diagonal_ReturnsParallel()
    {
        LineSegmentIntersectionResult r = Intersect(
            new Point2(0, 0), new Point2(50, 50),
            new Point2(10, 20), new Point2(60, 70));
        Assert.Equal(LineIntersectionKind.Parallel, r.Kind);
    }

    [Fact]
    public void Collinear_Overlap_ReturnsOverlapWithInterval()
    {
        LineSegmentIntersectionResult r = Intersect(
            new Point2(0, 0), new Point2(100, 0),
            new Point2(40, 0), new Point2(140, 0));
        Assert.Equal(LineIntersectionKind.CollinearOverlap, r.Kind);
        Assert.Equal(40, r.Point.X, 6);
        Assert.Equal(100, r.EndPoint.X, 6);
        Assert.Equal(0, r.Point.Y, 6);
    }

    [Fact]
    public void Collinear_Contained_ReturnsOverlap()
    {
        LineSegmentIntersectionResult r = Intersect(
            new Point2(0, 0), new Point2(100, 0),
            new Point2(20, 0), new Point2(80, 0));
        Assert.Equal(LineIntersectionKind.CollinearOverlap, r.Kind);
        Assert.Equal(20, r.Point.X, 6);
        Assert.Equal(80, r.EndPoint.X, 6);
    }

    [Fact]
    public void Collinear_JustTouching_ReturnsPoint()
    {
        LineSegmentIntersectionResult r = Intersect(
            new Point2(0, 0), new Point2(100, 0),
            new Point2(100, 0), new Point2(200, 0));
        Assert.Equal(LineIntersectionKind.Point, r.Kind);
        Assert.Equal(100, r.Point.X, 6);
    }

    [Fact]
    public void Collinear_Disjoint_ReturnsParallel()
    {
        LineSegmentIntersectionResult r = Intersect(
            new Point2(0, 0), new Point2(50, 0),
            new Point2(60, 0), new Point2(100, 0));
        Assert.Equal(LineIntersectionKind.Parallel, r.Kind);
    }

    [Fact]
    public void NearTolerance_AlmostTouching_ReturnsPointWithinEpsilon()
    {
        // Gap of 1e-9 mm: inside the point-equality epsilon, treated as touch.
        LineSegmentIntersectionResult r = Intersect(
            new Point2(0, 0), new Point2(100, 0),
            new Point2(100 + 1e-9, -10), new Point2(100 + 1e-9, 10));
        Assert.Equal(LineIntersectionKind.Point, r.Kind);
    }

    [Fact]
    public void Degenerate_ZeroLengthSegment_ReturnsNone()
    {
        LineSegmentIntersectionResult r = Intersect(
            new Point2(50, 50), new Point2(50, 50),
            new Point2(0, 0), new Point2(100, 100));
        Assert.Equal(LineIntersectionKind.None, r.Kind);
    }

    [Fact]
    public void NoCross_NearbyButSeparate_ReturnsNone()
    {
        LineSegmentIntersectionResult r = Intersect(
            new Point2(0, 0), new Point2(100, 0),
            new Point2(110, -10), new Point2(110, 10));
        Assert.Equal(LineIntersectionKind.None, r.Kind);
    }
}
