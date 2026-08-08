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

    public Viewport(double initialScale = 1.0)
    {
        PixelsPerWorld = initialScale;
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

    public void ZoomAt(double factor)
    {
        if (factor > 0)
        {
            PixelsPerWorld = Math.Max(PixelsPerWorld * factor, 1e-9);
            Changed?.Invoke();
        }
    }

    public void ZoomToFit(in Bounds bounds, double viewportWidthPx, double viewportHeightPx)
    {
        double w = bounds.Width;
        double h = bounds.Height;
        double scaleX = w > 1e-12 ? viewportWidthPx / w : 1.0;
        double scaleY = h > 1e-12 ? viewportHeightPx / h : 1.0;
        // 5% padding so geometry is not glued to the edges.
        double scale = Math.Min(scaleX, scaleY) * 0.95;
        PixelsPerWorld = scale > 1e-9 ? scale : 1.0;
        Center = new Point2(bounds.Center.X, bounds.Center.Y);
        Changed?.Invoke();
    }
}
