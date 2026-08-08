#nullable enable

using DxfContourStudio.Application.Localization;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Guard: NaN / Infinity / double.MaxValue / double.MinValue must never be
/// formatted as a user-visible "millimeters distance". They render as the
/// localized "not available" placeholder instead.
/// </summary>
public class DisplayFormatSafetyTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    public void Length_NonFiniteOrSentinel_NeverShowsNumber(double value)
    {
        string text = DisplayFormat.Length(value);
        Assert.DoesNotContain("179769313486232", text);
        Assert.DoesNotContain("NaN", text);
        Assert.DoesNotContain("Infinity", text);
        Assert.DoesNotContain("1.79769313", text);
        Assert.DoesNotContain("mm", text); // no numeric suffix for n/a
        Assert.False(text.Contains('.'), $"unexpected numeric text: {text}");
    }

    [Fact]
    public void Length_FiniteValue_FormatsNormally()
    {
        Assert.Equal("0.030 mm", DisplayFormat.Length(0.03));
        Assert.Equal("100.015 mm", DisplayFormat.Length(100.015));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.MaxValue)]
    public void Coordinate_NonFinite_NeverShowsNumber(double value)
    {
        string text = DisplayFormat.Coordinate(value);
        Assert.DoesNotContain("179769313486232", text);
        Assert.DoesNotContain("mm", text);
    }

    [Fact]
    public void Point_WithNonFiniteComponent_RendersNotAvailable()
    {
        var bad = new Point2(double.MaxValue, 5);
        string text = DisplayFormat.Point(bad);
        Assert.DoesNotContain("179769313486232", text);
        Assert.DoesNotContain("mm", text);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    public void AngleDegrees_NonFinite_NeverShowsNumber(double value)
    {
        string text = DisplayFormat.AngleDegrees(value);
        Assert.DoesNotContain("°", text);
        Assert.DoesNotContain("1.79769313", text);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    public void ZoomPercent_NonFinite_NeverShowsNumber(double value)
    {
        string text = DisplayFormat.ZoomPercent(value);
        Assert.DoesNotContain("%", text);
        Assert.DoesNotContain("1.79769313", text);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.MaxValue)]
    public void ElapsedSeconds_NonFinite_NeverShowsNumber(double value)
    {
        string text = DisplayFormat.ElapsedSeconds(value);
        Assert.DoesNotContain(" s", text);
    }

    [Fact]
    public void IsDisplayable_RejectsSentinels()
    {
        Assert.False(DisplayFormat.IsDisplayable(double.NaN));
        Assert.False(DisplayFormat.IsDisplayable(double.PositiveInfinity));
        Assert.False(DisplayFormat.IsDisplayable(double.MaxValue));
        Assert.False(DisplayFormat.IsDisplayable(double.MinValue));
        Assert.True(DisplayFormat.IsDisplayable(0.03));
        Assert.True(DisplayFormat.IsDisplayable(100.015));
    }
}
