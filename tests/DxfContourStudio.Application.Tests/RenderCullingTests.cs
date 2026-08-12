#nullable enable

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Selection;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Wpf.Views;
using CoreLineGeometry = DxfContourStudio.Core.Geometry.LineGeometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Rendering 2.0 (docs/RENDERING-2.md):
/// - pure headless unit tests for the viewport culling math
///   (<see cref="RenderCulling"/>);
/// - a headless off-screen render proof that culling still produces ink in a
///   scene whose visible region is a small window on a huge world (entities
///   far outside the viewport are skipped, the visible ones are drawn).
/// </summary>
public class RenderCullingTests
{
    private static Viewport Viewport(double scale = 1.0) => new(scale);

    [Fact]
    public void Culling_VisibleEntity_NotCulled()
    {
        var vp = Viewport();
        Bounds view = RenderCulling.WorldView(vp, 1000, 500);
        Assert.True(RenderCulling.IsVisible(new Bounds(-10, -10, 10, 10), view, 0));
    }

    [Fact]
    public void Culling_FarEntity_Culled()
    {
        var vp = Viewport();
        Bounds view = RenderCulling.WorldView(vp, 1000, 500);
        Assert.False(RenderCulling.IsVisible(new Bounds(10_000, 10_000, 10_100, 10_100), view, 0));
    }

    [Fact]
    public void Culling_MarginKeepsAdjacentEntities()
    {
        // View = [-500..500] x [-250..250] (scale 1, 1000x500). Entity just
        // past the edge stays visible within the margin, culled beyond.
        var vp = Viewport();
        Bounds view = RenderCulling.WorldView(vp, 1000, 500);
        Assert.True(RenderCulling.IsVisible(new Bounds(500, 0, 501, 1), view, 1));
        Assert.False(RenderCulling.IsVisible(new Bounds(600, 0, 601, 1), view, 10));
    }

    [Fact]
    public void Culling_ZoomOut_WidensWorldView()
    {
        // pixelsPerWorld 0.5 → world rect twice as wide (±1000).
        var vp = Viewport(0.5);
        Bounds view = RenderCulling.WorldView(vp, 1000, 500);
        Assert.True(RenderCulling.IsVisible(new Bounds(-100, -50, -90, -40), view, 0));
        Assert.False(RenderCulling.IsVisible(new Bounds(-2000, -50, -1990, -40), view, 0));
    }

    [Fact]
    public void Culling_EmptyBounds_NeverVisible()
    {
        var vp = Viewport();
        Bounds view = RenderCulling.WorldView(vp, 1000, 500);
        Assert.False(RenderCulling.IsVisible(Bounds.Empty, view, 10));
    }

    // ---- headless offscreen proof that culling + rendering cooperate ----

    [Fact]
    public void Offscreen_FarApartRegions_RenderInkWhenFocused()
    {
        var doc = new CadDocument();
        var entities = new System.Collections.Generic.List<IGeometryEntity>();
        for (int i = 0; i < 200; i++)
        {
            double x = 0 + i % 20;
            double y = i / 20;
            entities.Add(new CoreLineGeometry(i + 1, "0", new Point2(x, y), new Point2(x + 0.9, y)));
        }

        for (int i = 0; i < 200; i++)
        {
            double x = 100_000 + i % 20;
            double y = 100_000 + i / 20;
            entities.Add(new CoreLineGeometry(i + 201, "0", new Point2(x, y), new Point2(x + 0.9, y)));
        }

        doc.ReplaceContent(entities, [new LayerState("0", IsOn: true, IsFrozen: false, AciColorIndex: 7, IsColorByLayer: true)], null, null, null);

double regionAlpha = RenderRegionInk(doc, new Bounds(-10, -10, 110, 20));
        double regionBeta = RenderRegionInk(doc, new Bounds(99_990, 99_990, 100_110, 100_020));
        Console.WriteLine($"[diag] alpha={regionAlpha} beta={regionBeta}");
        Assert.True(regionAlpha > 200, $"cluster A rendered only {regionAlpha} ink pixels");
        Assert.True(regionBeta > 200, $"cluster B rendered only {regionBeta} ink pixels");
        Assert.True(regionAlpha + regionBeta > 400, "region clusters suspiciously low");
    }

    private static int RenderRegionInk(CadDocument doc, Bounds region)
    {
        int ink = 0;
        var thread = new Thread(() =>
        {
            try
            {
                var viewport = new Viewport();
                var control = new CadViewport { Document = doc, Viewport = viewport };
                const double w = 900;
                const double h = 500;
                control.Measure(new Size(w, h));
                control.Arrange(new Rect(0, 0, w, h));
                control.UpdateLayout();
                viewport.ZoomToFit(region, w, h);

                var bmp = new RenderTargetBitmap((int)w, (int)h, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(control);
                var pixels = new byte[(int)(w * h * 4)];
                bmp.CopyPixels(pixels, (int)w * 4, 0);
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    bool isBg = Math.Abs(pixels[i] - 250) <= 2 && Math.Abs(pixels[i + 1] - 250) <= 2 && Math.Abs(pixels[i + 2] - 250) <= 2;
                    if (!isBg)
                    {
                        ink++;
                    }
                }
            }
            catch
            {
                ink = int.MinValue;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA render thread timed out");
        Assert.True(ink >= 0, "offscreen render threw");
        return ink;
    }
}

