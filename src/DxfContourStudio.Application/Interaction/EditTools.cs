#nullable enable

using System.Collections.Generic;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Interaction;

/// <summary>
/// Active editing mode of the canvas. Exactly one mode is active at a time;
/// every mode owns the mouse handling of its gesture. The toolbar binds to
/// this enum to render the active-tool toggle state.
/// </summary>
public enum ToolMode
{
    /// <summary>Click-selects entities; drag starts a window/rect selection.</summary>
    Select,

    /// <summary>Node editing (move grips) — D12.</summary>
    NodeEdit,

    /// <summary>Join the two picked entities (D13).</summary>
    Join,

    /// <summary>Break the picked entity at the clicked point (D14).</summary>
    Break,

    /// <summary>Trim a section between two boundaries (D15) — click selects the
    /// path, then a second click chooses which section to remove.</summary>
    Trim,

    /// <summary>Extend the picked entity's free end to the nearest boundary (D16).</summary>
    Extend,
}

/// <summary>What the tool is previewing on the overlay for the current cursor position.</summary>
public enum ToolPreviewKind
{
    /// <summary>Nothing to preview.</summary>
    None,

    /// <summary>Normal preview (e.g. path candidate / node grip).</summary>
    Normal,

    /// <summary>Preview shows what will be removed or altered destructively.</summary>
    Remove,

    /// <summary>Preview shows an extension (the stretch to the boundary).</summary>
    Extend,

    /// <summary>The prospective action is invalid here (tool refuses).</summary>
    Invalid,
}

/// <summary>
/// Immutable snapshot of what the active tool wants to overlay on the canvas
/// for the current cursor position. The WPF layer translates it into visuals
/// (highlight runs, markers); an <see cref="ToolOverlayState.Empty"/> state
/// clears the overlay. UI-only record — carries no business logic.
/// </summary>
/// <param name="Kind">What the preview represents (normal / removal / extension / invalid).</param>
/// <param name="HighlightRuns">Geometry to draw as the tool preview (removed
/// section, extension result, join connection …).</param>
/// <param name="Markers">Marker points in world coordinates (break point,
/// boundary points, picked endpoints …).</param>
/// <param name="HoverEntityId">Entity towards which the preview belongs
/// (used to dim/highlight the rest of that entity).</param>
public sealed record ToolOverlayState(
    ToolPreviewKind Kind,
    IReadOnlyList<IPathSegment> HighlightRuns,
    IReadOnlyList<Point2> Markers,
    long? HoverEntityId = null)
{
    /// <summary>Nothing to draw; the canvas clears the tool overlay.</summary>
    public static ToolOverlayState Empty { get; } = new(ToolPreviewKind.None, [], [], null);
}
