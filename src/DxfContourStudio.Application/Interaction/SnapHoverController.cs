#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Interaction;

/// <summary>
/// Hysteresis + sticky layer on top of the raw snap engine (v0.3.0 UX
/// overhaul). The engine answers the question "what is the best candidate
/// right now"; this controller answers "what should the UI keep showing so
/// the marker does not flicker":
///
/// - Acquire: a marker appears only when a candidate comes within the acquire
///   radius (SnapSettings.PixelTolerance is the engine tolerance; the
///   controller re-checks the same radius in screen space).
/// - Release: once shown, the marker stays until the cursor leaves the
///   (larger) release radius — hysteresis at the boundary.
/// - Sticky: while the marker is held, equal-priority candidates must beat it
///   by StickyMinDeltaPx before the marker switches (two collinear endpoints
///   at the same spot stay put instead of alternating). A strictly
///   higher-priority candidate (endpoint over midpoint etc.) switches
///   immediately, matching the engine's kind priority.
///
/// Pure math, no WPF types: distances are converted to screen pixels by the
/// caller via the viewport scale.
/// </summary>
public sealed class SnapHoverController
{
    /// <summary>Snap kind priority, mirroring SnapEngine's evaluation order.</summary>
    private static readonly SnapKind[] KindPriority =
    [
        SnapKind.Endpoint, SnapKind.Intersection, SnapKind.Center, SnapKind.Midpoint, SnapKind.Nearest,
    ];

    private readonly InteractionSettings _settings;

    public SnapHoverController(InteractionSettings? settings = null)
    {
        _settings = settings ?? InteractionSettings.Default;
    }

    /// <summary>The marker the UI should currently display (null = no marker).</summary>
    public SnapResult? Current { get; private set; }

    /// <summary>
    /// Feeds one engine result. <paramref name="cursorWorld"/> and
    /// <paramref name="pixelsPerWorld"/> allow the controller to re-derive the
    /// current on-screen distance of the held marker, which the engine result
    /// alone cannot provide after the cursor moved.
    /// </summary>
    public void Update(Point2 cursorWorld, SnapResult engineResult, double pixelsPerWorld)
    {
        if (!engineResult.IsValid)
        {
            // No candidate this frame: keep the held marker while it is still
            // within the release radius, otherwise let go.
            if (Current is { } held && HeldScreenDistance(cursorWorld, held, pixelsPerWorld) <= _settings.SnapReleaseRadiusPx)
            {
                return;
            }

            Current = null;
            return;
        }

        double candidateScreen = engineResult.DistanceWorld * pixelsPerWorld;
        if (Current is not { } sticky)
        {
            Current = candidateScreen <= _settings.SnapAcquireRadiusPx ? engineResult : null;
            return;
        }

        double stickyScreen = HeldScreenDistance(cursorWorld, sticky, pixelsPerWorld);
        if (stickyScreen > _settings.SnapReleaseRadiusPx)
        {
            // The held marker dropped out of range — the next candidate is
            // treated as a fresh acquisition.
            Current = candidateScreen <= _settings.SnapAcquireRadiusPx ? engineResult : null;
            return;
        }

        int newRank = Rank(engineResult.Kind);
        int stickyRank = Rank(sticky.Kind);
        if (newRank < stickyRank)
        {
            // Strictly higher priority wins immediately (endpoint beats
            // midpoint at the same pixel).
            Current = engineResult;
            return;
        }

        if (newRank == stickyRank && candidateScreen + _settings.StickyMinDeltaPx < stickyScreen)
        {
            // Equal priority: only switch when the new candidate is clearly
            // closer than the held one (sticky threshold).
            Current = engineResult;
        }
    }

    /// <summary>Forgets the held marker (Esc, tool cancel, document change, master switch off).</summary>
    public void Reset() => Current = null;

    private static double HeldScreenDistance(Point2 cursorWorld, SnapResult held, double pixelsPerWorld)
        => cursorWorld.DistanceTo(held.WorldPoint) * pixelsPerWorld;

    private static int Rank(SnapKind kind)
    {
        for (int i = 0; i < KindPriority.Length; i++)
        {
            if (KindPriority[i] == kind)
            {
                return i;
            }
        }

        return KindPriority.Length;
    }
}