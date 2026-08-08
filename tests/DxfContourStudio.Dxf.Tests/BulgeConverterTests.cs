#nullable enable

using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Dxf.Infrastructure;

namespace DxfContourStudio.Dxf.Tests;

/// <summary>
/// Numerical tests of the bulge→arc conversion. All numbers were verified
/// against AutoCAD's documented behavior: bulge = tan(θ/4).
/// </summary>
public class BulgeConverterTests
{
    [Fact]
    public void Bulge_Positive_ProducesCcwSemicircle()
    {
        // bulge=1 means a 180° CCW semicircle.
        (Point2 Center, double Radius, double StartAngle, double Sweep)? arc =
            BulgeConverter.TryConvert(new Point2(0, 0), new Point2(10, 0), 1.0);
        Assert.NotNull(arc);
        Assert.Equal(5.0, arc.Value.Radius, 12);
        Assert.Equal(new Point2(5, 0), arc.Value.Center);
        Assert.Equal(Math.PI, Math.Abs(arc.Value.Sweep), 9);
        Assert.True(arc.Value.Sweep > 0);
    }

    [Fact]
    public void Bulge_Negative_ProducesClockwiseArc()
    {
        (Point2 Center, double Radius, double StartAngle, double Sweep)? arc =
            BulgeConverter.TryConvert(new Point2(0, 0), new Point2(10, 0), -1.0);
        Assert.NotNull(arc);
        Assert.Equal(5.0, arc.Value.Radius, 12);
        AssertExPoint.PointClose(new Point2(5, 0), arc.Value.Center, 1e-9);
        Assert.True(arc.Value.Sweep < 0);
    }

    [Fact]
    public void Bulge_Zero_ReturnsNull()
    {
        (Point2 Center, double Radius, double StartAngle, double Sweep)? arc =
            BulgeConverter.TryConvert(new Point2(0, 0), new Point2(10, 0), 0.0);
        Assert.Null(arc);
    }

    [Fact]
    public void Bulge_QuarterArc_RadiusMatchesFormula()
    {
        // bulge = tan(90°/4) = tan(22.5°) ≈ 0.4142 over a 10 mm chord.
        double bulge = Math.Tan(22.5 * MathUtil.Deg2Rad);
        (Point2 Center, double Radius, double StartAngle, double Sweep)? arc =
            BulgeConverter.TryConvert(new Point2(0, 0), new Point2(10, 0), bulge);
        Assert.NotNull(arc);
        // r = c/2 * (1+b^2)/(2b) ... for 90° included angle sagitta computes
        // to r = c/(2·sin(45°)) = 10/1.4142 ≈ 7.07.
        double expected = 10.0 / Math.Sqrt(2);
        Assert.Equal(expected, arc.Value.Radius, 6);
        Assert.Equal(Math.PI / 2, Math.Abs(arc.Value.Sweep), 9);
    }

    [Fact]
    public void Bulge_StartEndPoint_Conistency()
    {
        // Arc endpoints must match the input points.
        var a = new Point2(3, 4);
        var b = new Point2(15, 20);
        (Point2 Center, double Radius, double StartAngle, double Sweep)? arc =
            BulgeConverter.TryConvert(a, b, 0.5);
        Assert.NotNull(arc);

        Point2 start = new(
            arc.Value.Center.X + arc.Value.Radius * Math.Cos(arc.Value.StartAngle),
            arc.Value.Center.Y + arc.Value.Radius * Math.Sin(arc.Value.StartAngle));
        AssertExPoint.PointClose(a, start, 1e-6);
    }
}

internal static class AssertExPoint
{
    public static void PointClose(Point2 expected, Point2 actual, double tolerance)
    {
        Assert.True(expected.DistanceTo(actual) <= tolerance,
            $"Points not close: expected {expected}, actual {actual}, distance {expected.DistanceTo(actual)} > {tolerance}");
    }
}