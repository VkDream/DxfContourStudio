#nullable enable

using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Localization;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Properties panel data: single-selection detail rows (per entity kind),
/// multi-selection aggregates and the empty-selection case.
/// Rows are keyed (NameKey/ValueKey) and grouped (GroupKey); values are
/// formatted by DisplayFormat. Labels are resolved by the UI layer, so these
/// tests assert on keys and formatted values, never on localized text.
/// </summary>
public class EntityPropertyBuilderTests
{
    [Fact]
    public void NoSelection_ReturnsNoRows()
    {
        var doc = TestDocs.SceneDocument();

        var rows = EntityPropertyBuilder.Build(doc, selected: null);

        Assert.Empty(rows);
    }

    [Fact]
    public void SingleLine_ExposesBasicGeometryAndBoundsRows()
    {
        var doc = TestDocs.LineDocument();
        var rows = EntityPropertyBuilder.Build(doc, [doc.GetEntityById(1)!]);

        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyType && r.ValueKey == LocalizationKeys.TypeLine);
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyLayer && r.Value == "0");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyVisible && r.ValueKey == LocalizationKeys.CommonYes);
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyStartX && r.Value == "0.000 mm");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyStartY && r.Value == "0.000 mm");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyEndX && r.Value == "100.000 mm");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyLength && r.Value == "100.000 mm");
        Assert.All(rows, r => Assert.True(
            r.GroupKey == LocalizationKeys.GroupBasic ||
            r.GroupKey == LocalizationKeys.GroupGeometry ||
            r.GroupKey == LocalizationKeys.GroupBounds,
            $"unexpected group {r.GroupKey} for {r.NameKey}"));
    }

    [Fact]
    public void SingleCircle_IncludesRadiusDiameterCircumferenceAndCenter()
    {
        var doc = TestDocs.SceneDocument();
        var rows = EntityPropertyBuilder.Build(doc, [doc.GetEntityById(2)!]);

        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyType && r.ValueKey == LocalizationKeys.TypeCircle);
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyRadius && r.Value == "10.000 mm");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyDiameter && r.Value == "20.000 mm");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyCircumference &&
                                   r.Value == $"{2 * Math.PI * 10:0.000} mm");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyCenterX && r.Value == "50.000 mm");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyCenterY && r.Value == "25.000 mm");
    }

    [Fact]
    public void SingleArc_IncludesAnglesAndArcLength()
    {
        var doc = TestDocs.SceneDocument();
        var rows = EntityPropertyBuilder.Build(doc, [doc.GetEntityById(3)!]);

        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertySweepAngle && r.Value == "90.000°");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyStartAngle);
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyEndAngle);
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyArcLength);
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyCenterX && r.Value == "10.000 mm");
    }

    [Fact]
    public void SinglePolyline_IncludesClosedAndSegments()
    {
        var doc = TestDocs.SceneDocument();
        var rows = EntityPropertyBuilder.Build(doc, [doc.GetEntityById(4)!]);

        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyType && r.ValueKey == LocalizationKeys.TypePolyline);
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyClosed && r.ValueKey == LocalizationKeys.CommonYes);
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertySegments && r.Value == "3.000 mm");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyLength);
    }

    [Fact]
    public void MultiSelection_AggregatesCountAndTotalLength()
    {
        var doc = TestDocs.SceneDocument();
        var rows = EntityPropertyBuilder.Build(doc, [doc.GetEntityById(1)!, doc.GetEntityById(2)!]);

        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyCount && r.Value == "2" &&
                                   r.GroupKey == LocalizationKeys.GroupMulti);
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyTotalLength &&
                                   r.GroupKey == LocalizationKeys.GroupMulti);
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.PropertyType &&
                                   r.ValueKey is not null &&
                                   r.ValueKey.Contains(LocalizationKeys.TypeLine) &&
                                   r.ValueKey.Contains(LocalizationKeys.TypeCircle));
    }
}
