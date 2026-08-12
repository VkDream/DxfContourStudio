#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Selection;

/// <summary>
/// Pure viewport culling math used by the WPF renderer (Rendering 2.0,
/// docs/RENDERING-2.md): entities whose world-space bounding box does not
/// intersect the visible world rectangle (padded by a screen-pixel margin)
/// are skipped before any drawing calls, so pan/zoom scenes with thousands
/// of entities never rasterize off-screen geometry.
///
/// Kept framework-free so it is unit-testable headlessly.
/// </summary>
public static class RenderCulling
{
    /// <summary>The world-space rectangle currently visible in the viewport.</summary>
    public static Bounds WorldView(Viewport viewport, double viewWidthPx, double viewHeightPx)
    {
        double halfWidthWorld = viewport.PixelsToWorld(Math.Max(viewWidthPx, 1)) / 2;
        double halfHeightWorld = viewport.PixelsToWorld(Math.Max(viewHeightPx, 1)) / 2;
        Point2 c = viewport.Center;
        return new Bounds(c.X - halfWidthWorld, c.Y - halfHeightWorld, c.X + halfWidthWorld, c.Y + halfHeightWorld);
    }

    /// <summary>
    /// True when the entity's world bounds intersect the visible rectangle
    /// (or intersect within the given world-space margin, so strokes/pen
    /// widths near the edge are never wrongly culled).
    /// </summary>
    public static bool IsVisible(Bounds entityBounds, Bounds viewWorld, double marginWorld)
    {
        if (entityBounds.IsEmpty || viewWorld.IsEmpty)
        {
            return false;
        }

        return entityBounds.Intersects(viewWorld.Inflated(marginWorld));
    }
}