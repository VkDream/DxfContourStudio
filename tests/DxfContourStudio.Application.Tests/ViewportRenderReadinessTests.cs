#nullable enable

using System.IO;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Imports;
using DxfContourStudio.Application.Selection;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Dxf.Infrastructure;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Static render-readiness checks for the viewport pipeline (regression for
/// GUI smell "DXF loaded but the cad viewport is blank"): the document must
/// expose the visible entities and finite bounds, and the pure-math zoom-to-fit
/// must map the scene into a real 1200×700 pixel viewport.
/// </summary>
public class ViewportRenderReadinessTests
{
    private static string SamplePath(string relative) =>
        Path.Combine(AppContext.BaseDirectory, relative);

    private static CadDocument Load(string fileName)
    {
        var doc = new CadDocument();
        var service = new DxfImportService(new AcadSharpDxfReader());
        Assert.True(service.Import(SamplePath("testdata/dxf/" + fileName), doc).IsSuccess);
        return doc;
    }

    [Fact]
    public void OuterHole_VisibleEntitiesAndBoundsAreFinite()
    {
        CadDocument doc = Load("outer_hole.dxf");

        // The user's failure: the CAD viewport stayed blank even though the
        // import succeeded. The document side must be fully prepared here.
        Assert.Equal(2, doc.Entities.Count);
        Assert.Equal(2, doc.VisibleEntities.Count);

        Bounds? bounds = doc.OverallBounds;
        Assert.NotNull(bounds);
        Assert.True(double.IsFinite(bounds.Value.MinX));
        Assert.True(double.IsFinite(bounds.Value.MinY));
        Assert.True(double.IsFinite(bounds.Value.MaxX));
        Assert.True(double.IsFinite(bounds.Value.MaxY));
        Assert.True(bounds.Value.Width > 0);
        Assert.True(bounds.Value.Height > 0);
        Assert.Equal(200, bounds.Value.Width, 6);
        Assert.Equal(150, bounds.Value.Height, 6);
    }

    [Fact]
    public void OuterHole_ZoomToFitMapsSceneInto1200x700Viewport()
    {
        CadDocument doc = Load("outer_hole.dxf");
        Bounds bounds = doc.OverallBounds!.Value;
        var viewport = new Viewport();

        const double vw = 1200;
        const double vh = 700;
        viewport.ZoomToFit(bounds, vw, vh);

        // A valid rendered framing: finite zoom of 0.95 * min(vw/W, vh/H) and
        // the scene centre on the screen centre.
        Assert.True(viewport.PixelsPerWorld > 0);
        Assert.True(double.IsFinite(viewport.PixelsPerWorld));

        Point2 centerOnScreen = viewport.WorldToScreen(bounds.Center, vw, vh);
        AssertInViewport(centerOnScreen, vw, vh, nameof(centerOnScreen));

        // every entity must map inside the viewport (with the 5% fit padding a
        // margin of a few pixels each side is acceptable).
        foreach (IGeometryEntity entity in doc.VisibleEntities)
        {
            Bounds e = entity.Bounds;
            Point2 a = viewport.WorldToScreen(new Point2(e.MinX, e.MinY), vw, vh);
            Point2 b = viewport.WorldToScreen(new Point2(e.MaxX, e.MaxY), vw, vh);
            double xL = Math.Min(a.X, b.X);
            double xR = Math.Max(a.X, b.X);
            double yT = Math.Min(a.Y, b.Y);
            double yB = Math.Max(a.Y, b.Y);
            Assert.True(xL < vw && xR > 0 && yT < vh && yB > 0,
                $"entity bounds screen box out of viewport: [{xL},{xR}]x[{yT},{yB}]");
        }
    }

    private static void AssertInViewport(Point2 p, double w, double h, string what)
    {
        Assert.True(p.X >= -0.001 && p.X <= w + 0.001, $"{what}.X={p.X} outside 0..{w}");
        Assert.True(p.Y >= -0.001 && p.Y <= h + 0.001, $"{what}.Y={p.Y} outside 0..{h}");
    }
}