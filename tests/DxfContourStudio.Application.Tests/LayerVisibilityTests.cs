#nullable enable

using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Layer visibility is view state: hiding a layer must not delete entities,
/// but must exclude them from rendering, picking and selection.
/// </summary>
public class LayerVisibilityTests
{
    [Fact]
    public void EntityCountOnLayer_CountsOnlyThatLayer()
    {
        var doc = TestDocs.SceneDocument();
        Assert.Equal(1, doc.EntityCountOnLayer("0"));
        Assert.Equal(2, doc.EntityCountOnLayer("cut"));
        Assert.Equal(1, doc.EntityCountOnLayer("mark"));
        Assert.Equal(0, doc.EntityCountOnLayer("missing"));
    }

    [Fact]
    public void HideLayer_ExcludesFromVisibleEntities()
    {
        var doc = TestDocs.SceneDocument();
        Assert.Equal(4, doc.VisibleEntities.Count);

        doc.SetLayerVisible("cut", false);

        Assert.Equal(2, doc.VisibleEntities.Count);
        Assert.DoesNotContain(doc.VisibleEntities, e => e.LayerName == "cut");
    }

    [Fact]
    public void HideLayer_ExcludesFromPick()
    {
        var doc = TestDocs.SceneDocument();
        doc.SetLayerVisible("cut", false);

        var hits = doc.Pick(new Point2(50, 15), 10);

        Assert.DoesNotContain(hits, e => e.LayerName == "cut");
    }

    [Fact]
    public void ShowLayer_AfterHide_RestoresVisibility()
    {
        var doc = TestDocs.SceneDocument();
        doc.SetLayerVisible("cut", false);
        Assert.Equal(2, doc.VisibleEntities.Count);

        doc.SetLayerVisible("cut", true);

        Assert.Equal(4, doc.VisibleEntities.Count);
    }

    [Fact]
    public void ShowAllLayers_UnhidesEverything()
    {
        var doc = TestDocs.SceneDocument();
        doc.SetLayerVisible("cut", false);
        doc.SetLayerVisible("mark", false);
        // Only the line on layer "0" is still visible.
        Assert.Single(doc.VisibleEntities);

        doc.ShowAllLayers();

        Assert.Equal(4, doc.VisibleEntities.Count);
    }

    [Fact]
    public void HideAllLayers_HidesEveryKnownLayer()
    {
        var doc = TestDocs.SceneDocument();
        doc.HideAllLayers();
        Assert.Empty(doc.VisibleEntities);
        Assert.False(doc.IsLayerVisible("0"));
        Assert.False(doc.IsLayerVisible("cut"));
    }

    [Fact]
    public void HideAllLayers_DoesNotDeleteEntities()
    {
        var doc = TestDocs.SceneDocument();
        doc.HideAllLayers();

        Assert.Equal(4, doc.Entities.Count);
    }

    [Fact]
    public void OverallBounds_OnlySpansVisibleLayers()
    {
        var doc = TestDocs.SceneDocument();
        // Hide everything except the line on "0": only the line remains.
        doc.SetLayerVisible("cut", false);
        doc.SetLayerVisible("mark", false);

        var b = doc.OverallBounds!.Value;

        Assert.Equal(0.0, b.MinY, 6);
        Assert.Equal(0.0, b.MaxY, 6);
    }

    [Fact]
    public void ReplaceContent_ResetsLayerVisibility()
    {
        var doc = TestDocs.SceneDocument();
        doc.SetLayerVisible("cut", false);
        Assert.Equal(2, doc.VisibleEntities.Count);

        doc.ReplaceContent(doc.Entities.ToList(), doc.Layers, null, null, null);

        Assert.Equal(4, doc.VisibleEntities.Count);
    }
}
