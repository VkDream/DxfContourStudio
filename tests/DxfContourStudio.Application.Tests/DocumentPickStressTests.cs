#nullable enable

using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// D11: CadDocument.Pick backed by the spatial index must stay correct at
/// 100k entities — same document semantics as the linear scan (nearest
/// within tolerance, visibility honored, index invalidated on mutation),
/// without a multi-second rebuild per pick.
/// </summary>
public class DocumentPickStressTests
{
    private const int Count = 100_000;

    private static CadDocument Document()
    {
        List<IGeometryEntity> entities = new(Count);
        for (int i = 0; i < Count; i++)
        {
            double x = i % 1000;
            double y = (i / 1000) % 100;
            entities.Add(new LineGeometry(i + 1, "0", new Point2(x, y), new Point2(x + 0.5, y)));
        }

        var doc = new CadDocument();
        doc.ReplaceContent(
            entities,
            [new LayerState("0", IsOn: true, IsFrozen: false, AciColorIndex: 7, IsColorByLayer: true)],
            null, null, null);
        return doc;
    }

    [Fact]
    public void Pick_At100k_FindsExactNearest()
    {
        var doc = Document();

        var picks = doc.Pick(new Point2(10.25, 10.0), 0.5);
        var hit = Assert.Single(picks);
        Assert.Equal(10011L, hit.Id);

        Assert.Empty(doc.Pick(new Point2(10.75, 10.0), 0.1));
    }

    [Fact]
    public void Pick_At100k_EmptyZoneAndBoundary()
    {
        var doc = Document();
        Assert.Empty(doc.Pick(new Point2(500.75, 50), 0.01));

        var picks = doc.Pick(new Point2(0.5, 0.0), 0.3);
        Assert.Single(picks, e => e.Id == 1);
    }

    [Fact]
    public void Pick_At100k_ObeyVisibility()
    {
        var doc = Document();
        doc.SetLayerVisible("0", visible: false);
        Assert.Empty(doc.Pick(new Point2(10.25, 10.0), 0.5));
    }

    [Fact]
    public void Pick_At100k_InvalidatesAfterMutation()
    {
        var doc = Document();
        var original = Assert.Single(doc.Pick(new Point2(10.25, 10.0), 0.5));
        Assert.Equal(10011L, original.Id);

        doc.RemoveEntity(10011L);
        Assert.Empty(doc.Pick(new Point2(10.25, 10.0), 0.5));

        // Re-adding a nearby entity must be visible to the next pick.
        doc.AddEntity(new LineGeometry(200_001, "0", new Point2(10.25, 10.0), new Point2(10.6, 10.0)));
        var rerault = Assert.Single(doc.Pick(new Point2(10.3, 10.0), 0.3));
        Assert.Equal(200_001L, rerault.Id);
    }
}