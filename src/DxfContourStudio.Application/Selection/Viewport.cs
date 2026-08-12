#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Selection;

/// <summary>
/// The result of a spatial pick on a document: the closest hit wins unless
/// additive selection is requested, in which case every hit is returned.
/// </summary>
public readonly record struct PickResult(IGeometryEntity? Closest, IReadOnlyList<IGeometryEntity> AllHits);

/// <summary>
/// Pure math viewport: maps world (millimeters) to screen pixels and vice
/// versa + zoom-to-fit. No WPF types are used so this is unit testable on its
/// own.
///
/// Hit-testing is defined in pixels so that selection feels the same at every
/// zoom level: the caller converts a fixed pixel tolerance into world units
/// via <see cref="PixelsToWorld"/> before measuring distances.
/// </summary>
public sealed class Viewport
{
    /// <summary>World point at the centre of the view.</summary>
    public Point2 Center { get; private set; } = Point2.Origin;

    /// <summary>Pixels per world unit (zoom factor); larger = more zoomed in.</summary>
    public double PixelsPerWorld { get; private set; } = 1.0;

    /// <summary>
    /// Raised whenever the camera state (center/zoom) changes, e.g. from the
    /// wheel gesture or a menu command. The UI subscribes to repaint.
    /// </summary>
    public event Action? Changed;

    /// <summary>Hard lower bound of the zoom factor (pixels per world unit).</summary>
    private readonly double _minPixelsPerWorld;

    /// <summary>Hard upper bound of the zoom factor (pixels per world unit).</summary>
    private readonly double _maxPixelsPerWorld;

    public Viewport(double initialScale = 1.0, double minPixelsPerWorld = 1e-4, double maxPixelsPerWorld = 1e6)
    {
        PixelsPerWorld = Math.Clamp(initialScale, minPixelsPerWorld, maxPixelsPerWorld);
        _minPixelsPerWorld = minPixelsPerWorld;
        _maxPixelsPerWorld = maxPixelsPerWorld;
    }

    public Point2 WorldToScreen(Point2 world, double viewWidthPx, double viewHeightPx) =>
        new(
            viewWidthPx / 2 + (world.X - Center.X) * PixelsPerWorld,
            viewHeightPx / 2 - (world.Y - Center.Y) * PixelsPerWorld);

    public Point2 ScreenToWorld(Point2 screen, double viewportWidthPx, double viewportHeightPx) =>
        new(
            Center.X + (screen.X - viewportWidthPx / 2) / PixelsPerWorld,
            Center.Y - (screen.Y - viewportHeightPx / 2) / PixelsPerWorld);

    /// <summary>
    /// Converts a screen-pixel distance into the equivalent world distance at
    /// the current zoom. Keeps hit-testing and snapping constant in pixels.
    /// </summary>
    public double PixelsToWorld(double pixels) => pixels / PixelsPerWorld;

    public void Pan(Point2 deltaWorld)
    {
        Center = new Point2(Center.X + deltaWorld.X, Center.Y + deltaWorld.Y);
        Changed?.Invoke();
    }

    /// <summary>
    /// Zooms by <paramref name="factor"/> around the viewport centre, clamped
    /// to the configured zoom bounds. The factor is a multiplier of the
    /// pixels-per-world scale (1.15 per wheel notch).
    /// </summary>
    public void ZoomAt(double factor)
    {
        if (factor > 0)
        {
            PixelsPerWorld = Math.Clamp(PixelsPerWorld * factor, _minPixelsPerWorld, _maxPixelsPerWorld);
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Zooms by <paramref name="factor"/> while keeping the world point under
    /// <paramref name="screenPoint"/> fixed on screen (cursor-anchored wheel
    /// zoom). Pure math so the gesture is unit-testable; the view calls this
    /// with the cursor position and viewport size.
    /// </summary>
    public void ZoomAtScreen(double factor, Point2 screenPoint, double viewportWidthPx, double viewportHeightPx)
    {
        if (factor <= 0)
        {
            return;
        }

        double oldScale = PixelsPerWorld;
        double newScale = Math.Clamp(oldScale * factor, _minPixelsPerWorld, _maxPixelsPerWorld);
        if (newScale == oldScale)
        {
            return;
        }

        // The world point under the cursor must stay under the cursor, so the
        // centre moves by the world delta of the cursor position.
        Point2 anchor = ScreenToWorld(screenPoint, viewportWidthPx, viewportHeightPx);
        PixelsPerWorld = newScale;
        Point2 anchorAfter = ScreenToWorld(screenPoint, viewportWidthPx, viewportHeightPx);
        Center = new Point2(Center.X + anchor.X - anchorAfter.X, Center.Y + anchor.Y - anchorAfter.Y);
        Changed?.Invoke();
    }

    /// <summary>
    /// Pans the view by a screen-pixel delta (drag gesture) so the content
    /// follows the pointer 1:1. The Y axis is flipped because screen y grows
    /// downwards while world y grows upwards.
    /// </summary>
    public void PanByScreen(double deltaXScreenPx, double deltaYScreenPx)
    {
        Pan(new Point2(-deltaXScreenPx / PixelsPerWorld, deltaYScreenPx / PixelsPerWorld));
    }

    public void ZoomToFit(in Bounds bounds, double viewportWidthPx, double viewportHeightPx, double marginRatio = 0.05)
    {
        double w = bounds.Width;
        double h = bounds.Height;
        const double Eps = 1e-12;
        // A degenerate dimension (vertical line / horizontal line / point)
        // cannot constrain the scale — ignore it instead of falling back to a
        // wrong scale of 1.0.
        double scaleX = w > Eps ? viewportWidthPx / w : double.PositiveInfinity;
        double scaleY = h > Eps ? viewportHeightPx / h : double.PositiveInfinity;
        double margin = Math.Clamp(marginRatio, 0.0, 0.9);
        double scale = Math.Min(scaleX, scaleY) * (1.0 - margin);
        PixelsPerWorld = scale is double.PositiveInfinity || scale < _minPixelsPerWorld
            ? 1.0
            : Math.Min(scale, _maxPixelsPerWorld);
        Center = new Point2(bounds.Center.X, bounds.Center.Y);
        Changed?.Invoke();
    }
}
