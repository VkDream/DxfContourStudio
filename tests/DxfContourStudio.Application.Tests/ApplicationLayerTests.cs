#nullable enable

using System.IO;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Imports;
using DxfContourStudio.Application.Selection;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Dxf.Infrastructure;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Tests for the Application layer: document model, import service and the
/// pure-math viewport. Everything is UI-free on purpose.
/// </summary>
public class ApplicationLayerTests
{
    [Fact]
    public void Viewport_RoundTrip_WorldScreenWorld_RestoresPoint()
    {
        var vp = new Viewport(5.0);
        var world = new Point2(150.25, -37.5);
        Point2 screen = vp.WorldToScreen(world, 1000, 800);
        Point2 back = vp.ScreenToWorld(screen, 1000, 800);

        Assert.Equal(world.X, back.X, 6);
        Assert.Equal(world.Y, back.Y, 6);
    }

    [Fact]
    public void Viewport_ZoomAt_MultipliesScale()
    {
        var vp = new Viewport(1.0);
        vp.ZoomAt(2.0);

        Assert.Equal(2.0, vp.PixelsPerWorld, 9);
    }

    [Fact]
    public void ZoomToFit_WithBounds_SetsCenterAndScale()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [new LineGeometry(1, "0", new Point2(0, 0), new Point2(100, 50))],
            [],
            null,
            null,
            null);

        var vp = new Viewport(1.0);
        vp.ZoomToFit(doc.OverallBounds!.Value, 1000, 800);

        Assert.True(vp.PixelsPerWorld > 0);
        Assert.Equal(50.0, vp.Center.X, 6);
        Assert.Equal(25.0, vp.Center.Y, 6);
    }

    [Fact]
    public void Document_Pick_HitsEntityAtCenter()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [new LineGeometry(1, "0", new Point2(0, 0), new Point2(100, 0))],
            [],
            null,
            null,
            null);

        var hits = doc.Pick(new Point2(50, 3), 10.0);
        Assert.Single(hits);
        Assert.Equal(1L, hits[0].Id);
    }

    [Fact]
    public void Document_OverallBounds_SpansAllVisibleEntities()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [
                new LineGeometry(1, "0", new Point2(0, 0), new Point2(100, 0)),
                new CircleGeometry(2, "0", new Point2(50, 25), 10),
            ],
            [],
            null,
            null,
            null);

        var bounds = doc.OverallBounds!.Value;
        Assert.Equal(0.0, bounds.MinX, 6);
        Assert.Equal(0.0, bounds.MinY, 6);
        Assert.Equal(100.0, bounds.MaxX, 6);
        Assert.Equal(35.0, bounds.MaxY, 6);
    }

    [Fact]
    public void ImportService_ReadsSampleDxf_StampsDocument()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "testdata/dxf/basic-scene.dxf");
        var doc = new CadDocument();
        var service = new DxfImportService(new AcadSharpDxfReader());

        var outcome = service.Import(path, doc);

        Assert.True(outcome.IsSuccess);
        Assert.NotNull(doc.SourceFilePath);
        Assert.True(doc.Entities.Count >= 5);
        Assert.True(doc.OverallBounds is not null);
    }
}