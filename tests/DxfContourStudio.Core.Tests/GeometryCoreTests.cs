#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Tests;

/// <summary>
/// Geometry correctness tests for the Core primitives.
/// Every number below was derived by hand / by construction.
/// </summary>
public class PointVectorMathTests
{
    [Fact]
    public void Point2_DistanceTo_ComputesEuclidean()
    {
        Assert.Equal(5.0, new Point2(3, 4).DistanceTo(Point2.Origin), 12);
        Assert.Equal(0.0, new Point2(1, 1).DistanceTo(new Point2(1, 1)), 12);
        Assert.True(new Point2(1, 1).IsCoincident(new Point2(1, 1), 1e-9));
    }

    [Fact]
    public void Vector2_LengthAndNormalize()
    {
        var v = new Vector2(3, 4);
        Assert.Equal(5.0, v.Length, 12);
        var n = v.Normalized();
        Assert.Equal(1.0, n.Length, 12);
        Assert.Equal(0.6, n.X, 12);
    }

    [Fact]
    public void Vector2_DotAndCross()
    {
        var a = new Vector2(1, 0);
        var b = new Vector2(0, 1);
        Assert.Equal(0.0, a.Dot(b), 12);
        Assert.Equal(1.0, a.Cross(b), 12);
        Assert.Equal(-1.0, b.Cross(a), 12);
    }

    [Fact]
    public void MathUtil_AngularDifferenceHandlesWrap()
    {
        // 350° → 10°: smallest difference is 20° (and 340° the long way).
        Assert.Equal(MathUtil.Deg2Rad * 20.0, MathUtil.AngularDifference(350 * MathUtil.Deg2Rad, 10 * MathUtil.Deg2Rad), 9);
    }

    [Fact]
    public void MathUtil_SignedAreaAndOrientation()
    {
        // Simple CCW triangle (0,0)→(1,0)→(0,1) has area 0.5.
        Point2[] ccw = [new Point2(0, 0), new Point2(1, 0), new Point2(0, 1)];
        Assert.Equal(0.5, MathUtil.SignedArea2(ccw), 12);
        Assert.True(MathUtil.IsCounterClockwise(ccw));

        Point2[] cw = [new Point2(0, 0), new Point2(0, 1), new Point2(1, 0)];
        Assert.Equal(-0.5, MathUtil.SignedArea2(cw), 12);
        Assert.False(MathUtil.IsCounterClockwise(cw));
    }
}

/// <summary>
/// Correctness of the line/arc/circle/polyline primitives.
/// </summary>
public class PrimitiveGeometryTests
{
    [Fact]
    public void Line_DistanceToPoint_ClampsToSegment()
    {
        var line = new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 0));
        // Above the middle of the segment.
        Assert.Equal(5.0, line.DistanceToPoint(new Point2(5, 5)), 12);
        // Beyond the end: distance to endpoint (0,0) is 5, not 0.
        Assert.Equal(5.0, line.DistanceToPoint(new Point2(-5, 0)), 12);
        // On the segment.
        Assert.Equal(0.0, line.DistanceToPoint(new Point2(3, 0)), 12);
    }

    [Fact]
    public void Circle_Geometry_Metrics()
    {
        var circle = new CircleGeometry(1, "0", new Point2(10, 20), 5);
        Assert.Equal(2 * Math.PI * 5, circle.Length, 9);
        // (16,20) is 6 away from the center; radial distance to the rim is 1.
        Assert.Equal(1.0, circle.DistanceToPoint(new Point2(16, 20)), 12);
        Assert.Equal(new Bounds(5, 15, 15, 25), circle.Bounds);
        // StartPoint/EndPoint for a circle are the "east" point.
        Assert.Equal(new Point2(15, 20), circle.StartPoint);
    }

    [Fact]
    public void Arc_QuarterCcwFromZero_Bounds()
    {
        // Center at origin, radius 10, arc from 0° to 90° CCW.
        var arc = new ArcGeometry(1, "0", Point2.Origin, 10, 0, Math.PI / 2, isCounterClockwise: true);
        Assert.Equal(10.0 * Math.PI / 2, arc.Length, 12);
        Assert.Equal(new Point2(10, 0), arc.StartPoint);
        AssertEx.PointClose(new Point2(0, 10), arc.EndPoint, 1e-9);
        var b = arc.Bounds;
        Assert.Equal(0.0, b.MinX, 12);
        Assert.Equal(0.0, b.MinY, 12);
        Assert.Equal(10.0, b.MaxX, 12);
        Assert.Equal(10.0, b.MaxY, 12);
    }

    [Fact]
    public void Arc_CrossingZeroDegrees_BoundsIncludeEastExtreme()
    {
        // Arc from 315° to 45° CCW (sweep 90°) crosses the 0° line.
        var arc = new ArcGeometry(0, "0", Point2.Origin, 10, 315 * MathUtil.Deg2Rad, 90 * MathUtil.Deg2Rad);
        var b = arc.Bounds;
        // It reaches the east extreme (10, 0).
        Assert.Equal(10.0, b.MaxX, 9);
        Assert.True(b.MaxY > 7.0);
    }

    [Fact]
    public void Arc_Distance_RadialWhenInsideSweep()
    {
        // Semicircle CCW from 0° to 180°, radius 10, center origin.
        var arc = new ArcGeometry(0, "0", Point2.Origin, 10, 0, Math.PI);
        // Point directly "above" at (0, 15): radial distance 5.
        Assert.Equal(5.0, arc.DistanceToPoint(new Point2(0, 15)), 12);
        // Point at (15, 0) is within sweep 0..180 so radial dist 5 too.
        Assert.Equal(5.0, arc.DistanceToPoint(new Point2(15, 0)), 12);
        // Endpoint fallback: point at (0,-15) (angle 270°) is outside
        // the semicircle, so distance to nearest endpoint = |(0,-15)-(10,0)|.
        Assert.Equal(Math.Sqrt(100 + 225), arc.DistanceToPoint(new Point2(0, -15)), 9);
    }

    [Fact]
    public void Line_Clone_CopiesIdAndGeometry()
    {
        var a = new LineGeometry(7, "cut", new Point2(1, 2), new Point2(3, 4), isVisible: false);
        var b = (LineGeometry)a.Clone();
        Assert.NotSame(a, b);
        Assert.Equal(a.Id, b.Id);
        Assert.Equal(a.P0, b.P0);
        Assert.Equal(a.LayerName, b.LayerName);
        Assert.False(b.IsVisible);
    }
}

/// <summary>Helpers for tolerant point comparisons (record struct equality is exact).</summary>
internal static class AssertEx
{
    public static void PointClose(Point2 expected, Point2 actual, double tolerance)
    {
        Assert.True(expected.DistanceTo(actual) <= tolerance,
            $"Points not close: expected {expected}, actual {actual}, distance {expected.DistanceTo(actual)} > {tolerance}");
    }
}