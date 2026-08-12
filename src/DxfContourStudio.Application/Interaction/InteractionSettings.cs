#nullable enable

namespace DxfContourStudio.Application.Interaction;

/// <summary>
/// Central owner of every interaction-feel constant of the CAD viewport
/// (v0.3.0 UX overhaul): pixel hit radii, snap acquire/release radii, drag
/// thresholds, zoom factor and zoom clamp, fit margin, grip sizes. All values
/// are in screen pixels unless a comment says otherwise; world-space
/// conversions happen at the call site via
/// <see cref="Selection.Viewport.PixelsToWorld"/> so feel stays constant at
/// every zoom level.
///
/// Defaults were chosen so the app behaves exactly like v0.2.0 where a value
/// existed before (e.g. snap acquire radius 8px, wheel factor 1.15, fit
/// margin 0.05) and fill obvious gaps elsewhere (zoom clamps, snap release
/// radius, drag threshold, grip hover size).
/// </summary>
public sealed class InteractionSettings
{
    /// <summary>
    /// The shared default instance. View-models and views read their values
    /// through this instance; tests may construct their own instance to
    /// probe edge cases.
    /// </summary>
    public static InteractionSettings Default { get; } = new();

    /// <summary>Entity hit-test radius in screen pixels (v0.2.0: 6px).</summary>
    public double HitTolerancePx { get; init; } = 6.0;

    /// <summary>
    /// Snap marker acquisition radius in screen pixels. The cursor must come
    /// within this distance (snapped to an allowed snap kind) for a snap
    /// marker to appear (v0.2.0 SnapSettings.PixelTolerance = 8px).
    /// </summary>
    public double SnapAcquireRadiusPx { get; init; } = 8.0;

    /// <summary>
    /// Snap marker release radius in screen pixels. Once a marker is shown it
    /// stays until the cursor moves beyond this (larger) radius — hysteresis
    /// that keeps the marker stable at the boundary instead of flickering.
    /// </summary>
    public double SnapReleaseRadiusPx { get; init; } = 12.0;

    /// <summary>
    /// Minimum pixel distance by which a new candidate must beat the current
    /// sticky snap candidate before the marker switches to it. Fights
    /// flickering between equal-priority markers (endpoint of two collinear
    /// lines meeting at one point).
    /// </summary>
    public double StickyMinDeltaPx { get; init; } = 1.0;

    /// <summary>
    /// Minimum pointer movement (screen px) before a press counts as a drag
    /// gesture instead of a click (v0.2.0: 3px).
    /// </summary>
    public double DragThresholdPx { get; init; } = 4.0;

    /// <summary>Grip pick-up radius in screen px (v0.2.0: 8px).</summary>
    public double GripPickupRadiusPx { get; init; } = 8.0;

    /// <summary>Grip square half-size in screen px when not hovered (v0.2.0: 3.5px).</summary>
    public double GripSizePx { get; init; } = 3.5;

    /// <summary>Grip square half-size in screen px when hovered (v0.2.0: 4.5px).</summary>
    public double GripHoverSizePx { get; init; } = 4.5;

    /// <summary>Zoom multiplier per wheel notch / zoom in-out command (v0.2.0: 1.15).</summary>
    public double ZoomFactorPerNotch { get; init; } = 1.15;

    /// <summary>Hard lower bound of the zoom factor (pixels per world unit).</summary>
    public double MinPixelsPerWorld { get; init; } = 1e-4;

    /// <summary>Hard upper bound of the zoom factor (pixels per world unit).</summary>
    public double MaxPixelsPerWorld { get; init; } = 1e6;

    /// <summary>Padding around the fitted bounds, as a ratio of the viewport (v0.2.0: 0.05).</summary>
    public double FitMarginRatio { get; init; } = 0.05;

    /// <summary>
    /// Radius (screen px) inside which the cursor position is treated as
    /// "unchanged" for gesture decisions like the trim section plan cache.
    /// </summary>
    public double ClickStableRadiusPx { get; init; } = 4.0;

    /// <summary>Minimum box size (screen px) before a box drag counts as a box selection.</summary>
    public double BoxSelectionMinSizePx { get; init; } = 4.0;
}