#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Contours;

/// <summary>Walking direction of a closed contour (computed from the signed area).</summary>
public enum ContourOrientation
{
    /// <summary>Counter-clockwise (mathematical convention, Y up).</summary>
    CounterClockwise,

    /// <summary>Clockwise.</summary>
    Clockwise,
}

/// <summary>Nesting classification of a closed contour (filled by <see cref="NestingAnalyzer"/>).</summary>
public enum ContourRole
{
    /// <summary>Not yet classified (analysis did not run).</summary>
    Unknown,

    /// <summary>Outermost boundary of the part (depth 0).</summary>
    Outer,

    /// <summary>First-level void inside the part (depth 1).</summary>
    Hole,

    /// <summary>Material island inside a hole (depth 2, then 3, ... alternate).</summary>
    Island,
}

/// <summary>
/// Primary validity status of a contour. The primary status is a coarse
/// summary; finer problems live in the contour's diagnostic collection so no
/// information is lost by collapsing everything into one enum value.
/// </summary>
public enum ContourValidity
{
    /// <summary>Closed, no known defects.</summary>
    Valid,

    /// <summary>The chain did not close (open contour).</summary>
    Open,

    /// <summary>Contains at least one self intersection.</summary>
    SelfIntersecting,

    /// <summary>Contains a branch junction on its path.</summary>
    Branched,

    /// <summary>Degenerate (zero area / very small / NaN geometry).</summary>
    Degenerate,

    /// <summary>Gap-repairable (the contour is open but the ends can be closed by repair).</summary>
    GapRepairable,
}

/// <summary>
/// A single contour (chain) of the drawing: the ordered edge walk plus its
/// derived measures (length, bounds, signed area, orientation) and, after
/// <see cref="NestingAnalyzer"/>, its containment classification.
///
/// A contour is "open" when its chain did not close; open contours never get
/// area/orientation and are reported through the gap diagnostics instead.
/// </summary>
public sealed class Contour
{
    /// <summary>Stable contour id (1-based, analysis order).</summary>
    public int Id { get; internal set; }

    /// <summary>Ordered steps of the traversal.</summary>
    public IReadOnlyList<ChainStep> Steps { get; internal set; } = [];

    /// <summary>True when the chain is a closed loop or an intrinsically closed circle.</summary>
    public bool IsClosed { get; internal set; }

    /// <summary>True for a circle contour (no steps, radius below).</summary>
    public bool IsCircle { get; internal set; }

    /// <summary>Circle radius in millimeters (only when <see cref="IsCircle"/>).</summary>
    public double? CircleRadius { get; internal set; }

    /// <summary>Circle center (only when <see cref="IsCircle"/>).</summary>
    public Point2? CircleCenter { get; internal set; }

    /// <summary>Total path length in millimeters.</summary>
    public double Length { get; internal set; }

    /// <summary>Axis-aligned bounds of the contour.</summary>
    public Bounds Bounds { get; internal set; }

    /// <summary>Signed area (shoelace incl. arc correction); null for open contours.</summary>
    public double? SignedArea { get; internal set; }

    /// <summary>Walking orientation; null for open contours.</summary>
    public ContourOrientation? Orientation { get; internal set; }

    /// <summary>Warnings attached to this contour (localization keys, empty when clean).</summary>
    public IReadOnlyList<string> Warnings { get; internal set; } = [];

    /// <summary>Containment role (filled by the nesting pass).</summary>
    public ContourRole Role { get; internal set; } = ContourRole.Unknown;

    /// <summary>Containment depth: 0 = outermost, 1 = hole, 2 = island, 3 = hole, ...</summary>
    public int Depth { get; internal set; }

    /// <summary>Id of the smallest containing contour, or null when outermost.</summary>
    public int? ParentContourId { get; internal set; }

    /// <summary>Primary validity status (see <see cref="ContourValidity"/>).</summary>
    public ContourValidity Validity { get; internal set; } = ContourValidity.Valid;

    /// <summary>Diagnostic kind flags attached to this contour (may be several).</summary>
    public IReadOnlyList<Diagnostics.DiagnosticKind> DiagnosticKinds { get; internal set; } = [];

    /// <summary>Entity ids this contour touches (for selection/zoom-to-fit).</summary>
    public IReadOnlyList<long> EntityIds =>
        Steps.Select(s => s.Edge.SourceEntityId)
             .Concat(IsCircle && CircleEntityId is { } circleId ? [circleId] : [])
             .Distinct()
             .ToList();

    /// <summary>Circle source entity id, or null.</summary>
    public long? CircleEntityId { get; internal set; }
}
