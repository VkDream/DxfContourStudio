#nullable enable

using DxfContourStudio.Application.Selection;
using DxfContourStudio.Core.Geometry;
using Xunit;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Pure-math tests for the cursor-anchored zoom gesture (Viewport.ZoomAtScreen):
/// the world point under the cursor must stay under the cursor across zooms,
/// the gesture must clamp to the configured zoom bounds, and the legacy
/// centre-anchored ZoomAt must keep working. All assertions are in world and
/// pixel space without any window or GUI automation.
/// </summary>
public class ZoomAnchorTests
{
    private const double ViewW = 1200;
    private const double ViewH = 700;

    private static Point2 Screen(double x, double y) => new(x, y);

    [Fact]
    public void ZoomAtScreen_AnchorsCursorWorldPoint()
    {
        var viewport = new Viewport();
        viewport.Pan(new Point2(50, 30));
        viewport.ZoomAt(2.0);

        Point2 cursor = Screen(800, 200);
        Point2 worldBefore = viewport.ScreenToWorld(cursor, ViewW, ViewH);

        viewport.ZoomAtScreen(1.5, cursor, ViewW, ViewH);

        Point2 worldAfter = viewport.ScreenToWorld(cursor, ViewW, ViewH);
        Assert.Equal(worldBefore.X, worldAfter.X, 9);
        Assert.Equal(worldBefore.Y, worldAfter.Y, 9);
        Assert.Equal(2.0 * 1.5, viewport.PixelsPerWorld, 9);
    }

    [Fact]
    public void ZoomAtScreen_OffCentreCursor_MovesCentreAlongTheDelta()
    {
        var viewport = new Viewport();
        

        Point2 cursor = Screen(ViewW / 2 + 300, ViewH / 2 - 100);
        Point2 worldBefore = viewport.ScreenToWorld(cursor, ViewW, ViewH);
        // A world point at the cursor: e.g. 300px right of centre at scale 1.0.
        Point2 anchorWorld = worldBefore;

        viewport.ZoomAtScreen(2.0, cursor, ViewW, ViewH);

        Point2 worldAfter = viewport.ScreenToWorld(cursor, ViewW, ViewH);
        Assert.Equal(anchorWorld.X, worldAfter.X, 9);
        Assert.Equal(anchorWorld.Y, worldAfter.Y, 9);
        // Cursor sits 300px right of centre; the world point under it must
        // stay put, so the centre shifts +150 world (scale doubled).
        Assert.Equal(150.0, viewport.Center.X, 6);
        Assert.Equal(50.0, viewport.Center.Y, 6);
    }

    [Fact]
    public void ZoomAtScreen_ClampsToMaxZoom()
    {
        var viewport = new Viewport(1.0, 1e-4, 1e6);
        viewport.ZoomAtScreen(1e9, Screen(600, 350), ViewW, ViewH);

        Assert.Equal(1e6, viewport.PixelsPerWorld, 6);
    }

    [Fact]
    public void ZoomAtScreen_ClampsToMinZoom()
    {
        var viewport = new Viewport(1.0, 1e-4, 1e6);
        viewport.ZoomAtScreen(1e-12, Screen(600, 350), ViewW, ViewH);

        Assert.Equal(1e-4, viewport.PixelsPerWorld, 9);
    }

    [Fact]
    public void ZoomAtScreen_NoOpWhenZoopClamped_DoesNotMoveCentre()
    {
        var viewport = new Viewport(1e6, 1e-4, 1e6);
        Point2 centreBefore = viewport.Center;

        viewport.ZoomAtScreen(2.0, Screen(900, 100), ViewW, ViewH);

        Assert.Equal(1e6, viewport.PixelsPerWorld, 6);
        Assert.Equal(centreBefore.X, viewport.Center.X, 9);
        Assert.Equal(centreBefore.Y, viewport.Center.Y, 9);
    }

    [Fact]
    public void ZoomAtScreen_NonPositiveFactor_IsIgnored()
    {
        var viewport = new Viewport();
        viewport.ZoomAt(2.0);
        Point2 centreBefore = viewport.Center;

        viewport.ZoomAtScreen(0.0, Screen(600, 350), ViewW, ViewH);
        viewport.ZoomAtScreen(-1.0, Screen(600, 350), ViewW, ViewH);

        Assert.Equal(2.0, viewport.PixelsPerWorld, 9);
        Assert.Equal(centreBefore.X, viewport.Center.X, 9);
        Assert.Equal(centreBefore.Y, viewport.Center.Y, 9);
    }

    [Fact]
    public void ZoomAt_ClampsToConfiguredBounds()
    {
        var viewport = new Viewport(1.0, 0.5, 8.0);

        viewport.ZoomAt(100.0);
        Assert.Equal(8.0, viewport.PixelsPerWorld, 9);

        viewport.ZoomAt(1e-9);
        Assert.Equal(0.5, viewport.PixelsPerWorld, 9);
    }

    [Fact]
    public void ZoomAtScreen_AnchorHoldsAcrossMultipleSteps()
    {
        var viewport = new Viewport();
        viewport.Pan(new Point2(10, -20));
        viewport.ZoomAt(3.0);
        Point2 cursor = Screen(400, 600);
        Point2 worldBefore = viewport.ScreenToWorld(cursor, ViewW, ViewH);

        viewport.ZoomAtScreen(1.15, cursor, ViewW, ViewH);
        viewport.ZoomAtScreen(1.15, cursor, ViewW, ViewH);
        viewport.ZoomAtScreen(0.9, cursor, ViewW, ViewH);

        Point2 worldAfter = viewport.ScreenToWorld(cursor, ViewW, ViewH);
        Assert.Equal(worldBefore.X, worldAfter.X, 6);
        Assert.Equal(worldBefore.Y, worldAfter.Y, 6);
        Assert.Equal(3.0 * 1.15 * 1.15 * 0.9, viewport.PixelsPerWorld, 9);
    }
}