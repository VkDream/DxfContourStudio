#nullable enable

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Imports;
using DxfContourStudio.Application.Selection;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Dxf.Infrastructure;
using DxfContourStudio.Wpf.Views;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Off-screen (headless) rendering proof that the CadViewport renderer
/// actually produces non-blank pixels for the real test files. Runs on a STA
/// thread and renders the viewport into a RenderTargetBitmap — no window, no
/// GUI automation. This is the missing link between "math says visible" and
/// "the user's screen shows white".
/// </summary>
public class ViewportOffscreenRenderTests
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

    private static (int NonBackgroundPixels, int TotalPixels, double NonBgRatio) RenderOffscreen(
        CadDocument doc, string label)
    {
        int result = 0;
        int total = 0;
        double ratio = 0;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var viewport = new Viewport();
                var control = new CadViewport
                {
                    Document = doc,
                    Viewport = viewport,
                };

                const double w = 1180;
                const double h = 560;
                control.Measure(new Size(w, h));
                control.Arrange(new Rect(0, 0, w, h));
                control.UpdateLayout();

                Bounds? b = doc.OverallBounds;
                Assert.NotNull(b);

                // Step 1: render with the DEFAULT viewport (scale 1.0,
                // center 0,0) — mirrors the app before any fit ran.
                int defPixels = RenderPixels(control, viewport, w, h);

                // Step 2: render after ZoomToFit.
                viewport.ZoomToFit(b!.Value, w, h);
                int fitPixels = RenderPixels(control, viewport, w, h);

                Console.WriteLine($"[diag] {label}: defaultPixels={defPixels} fitPixels={fitPixels} ppw={viewport.PixelsPerWorld:F4} center=({viewport.Center.X:F4},{viewport.Center.Y:F4})");
                result = fitPixels;
                total = (int)w * (int)h;
                ratio = fitPixels / (double)total;
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA render thread timed out");
        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException($"Render of {label} failed: {failure}");
        }

        return (result, total, ratio);
    }

    private static int RenderPixels(CadViewport control, Viewport viewport, double w, double h)
    {
        var bmp = new RenderTargetBitmap((int)w, (int)h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(control);

        var pixels = new byte[(int)(w * h * 4)];
        bmp.CopyPixels(pixels, (int)w * 4, 0);

        int nonBg = 0;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte b0 = pixels[i];
            byte g = pixels[i + 1];
            byte r = pixels[i + 2];
            bool isBg = Math.Abs(r - 250) <= 2 && Math.Abs(g - 250) <= 2 && Math.Abs(b0 - 250) <= 2;
            if (!isBg)
            {
                nonBg++;
            }
        }

        return nonBg;
    }

    [Fact]
    public void BasicScene_OffscreenRender_ProducesInk()
    {
        var doc = Load("basic-scene.dxf");
        var (nonBg, total, ratio) = RenderOffscreen(doc, "basic-scene");
        Assert.True(nonBg > 50, $"basic-scene: only {nonBg}/{total} non-background pixels (ratio {ratio:F6})");
    }

    [Fact]
    public void OuterHole_OffscreenRender_ProducesInk()
    {
        var doc = Load("outer_hole.dxf");
        var (nonBg, total, ratio) = RenderOffscreen(doc, "outer_hole");
        Assert.True(nonBg > 50, $"outer_hole: only {nonBg}/{total} non-background pixels (ratio {ratio:F6})");
    }

    [Fact]
    public void SmallGap_OffscreenRender_ProducesInk()
    {
        var doc = Load("small_gap_003.dxf");
        var (nonBg, total, ratio) = RenderOffscreen(doc, "small_gap_003");
        Assert.True(nonBg > 50, $"small_gap_003: only {nonBg}/{total} non-background pixels (ratio {ratio:F6})");
    }
}
