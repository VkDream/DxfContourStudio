#nullable enable

using DxfContourStudio.Application.Localization;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Number display format: 3 fixed decimals for lengths/coordinates/angles,
/// whole percent for zoom, invariant culture everywhere (a comma decimal
/// separator must never appear regardless of the machine's display language).
/// </summary>
public class DisplayFormatTests
{
    [Fact]
    public void Length_UsesThreeDecimalsAndMmSuffix()
    {
        Assert.Equal("25.000 mm", DisplayFormat.Length(25));
        Assert.Equal("0.500 mm", DisplayFormat.Length(0.5));
        Assert.Equal("-1.167 mm", DisplayFormat.Length(-1.1667));
        Assert.Equal("100.000 mm", DisplayFormat.Length(100));
    }

    [Fact]
    public void Coordinate_UsesThreeDecimalsAndMmSuffix()
    {
        Assert.Equal("-1.167 mm", DisplayFormat.Coordinate(-1.1667));
        Assert.Equal("0.000 mm", DisplayFormat.Coordinate(0));
    }

    [Fact]
    public void Point_CombinesBothAxes()
    {
        Assert.Equal("0.000 mm, 100.000 mm", DisplayFormat.Point(new Point2(0, 100)));
        Assert.Equal("50.000 mm, 25.000 mm", DisplayFormat.Point(new Point2(50, 25)));
    }

    [Fact]
    public void AngleDegrees_ConvertsRadiansToDegreesWithSuffix()
    {
        Assert.Equal("90.000°", DisplayFormat.AngleDegrees(Math.PI / 2));
        Assert.Equal("180.000°", DisplayFormat.AngleDegrees(Math.PI));
        Assert.Equal("0.000°", DisplayFormat.AngleDegrees(0));
    }

    [Fact]
    public void ZoomPercent_IsWholePercent()
    {
        Assert.Equal("405%", DisplayFormat.ZoomPercent(4.05));
        Assert.Equal("100%", DisplayFormat.ZoomPercent(1.0));
        Assert.Equal("150%", DisplayFormat.ZoomPercent(1.5));
    }

    [Fact]
    public void Count_And_ElapsedSeconds_Formats()
    {
        Assert.Equal("4", DisplayFormat.Count(4));
        Assert.Equal("0.125 s", DisplayFormat.ElapsedSeconds(0.125));
    }

    [Fact]
    public void Always_InvariantCulture_CommaNeverAppearsAsDecimalSeparator()
    {
        // Note: Point() uses a comma as a *separator* between the two
        // coordinates (allowed); the decimal separator itself must be a dot.
        string[] singleValueFormats =
        [
            DisplayFormat.Length(1234.5),
            DisplayFormat.Coordinate(-0.123),
            DisplayFormat.AngleDegrees(Math.PI / 3),
            DisplayFormat.ElapsedSeconds(1.5),
        ];

        Assert.All(singleValueFormats, f => Assert.DoesNotContain(",", f));
        Assert.DoesNotContain(",", DisplayFormat.ZoomPercent(4.05));
        string point = DisplayFormat.Point(new Point2(1234.5, 0.125));
        Assert.Contains(",", point); // separator between axes
        Assert.Contains("1234.500 mm", point);
        Assert.Contains("0.125 mm", point);
    }
}
