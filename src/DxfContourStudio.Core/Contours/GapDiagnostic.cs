#nullable enable

using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Core.Topology;

namespace DxfContourStudio.Core.Contours;

/// <summary>
/// One finding of the gap / open-chain diagnostics pass. Every finding maps
/// back to concrete entity endpoints so the UI can locate it and (for small
/// gaps) repair it.
/// </summary>
public sealed class GapDiagnostic
{
    /// <summary>Kind of the finding.</summary>
    public GapKind Kind { get; internal set; }

    /// <summary>Entity endpoint at side A (entity id + run index + which end).</summary>
    public long EntityIdA { get; internal set; }

    public int SegmentIndexA { get; internal set; }

    public bool IsStartA { get; internal set; }

    /// <summary>Entity endpoint at side B (only for small gaps).</summary>
    public long EntityIdB { get; internal set; }

    public int SegmentIndexB { get; internal set; }

    public bool IsStartB { get; internal set; }

    /// <summary>World position of side A (and of the branch node for branch findings).</summary>
    public Point2 PositionA { get; internal set; }

    /// <summary>World position of side B (only for small gaps).</summary>
    public Point2 PositionB { get; internal set; }

    /// <summary>
    /// Distance between the two sides in millimeters (small gaps only, always
    /// a finite measured value). For open ends this holds the distance to the
    /// nearest candidate end when one exists; when no candidate end was found
    /// at all it is meaningless and <see cref="HasDistance"/> is false.
    /// </summary>
    public double Distance { get; internal set; }

    /// <summary>
    /// True when <see cref="Distance"/> is a real measured value (finite).
    /// False for open ends without any candidate nearby — the UI must then
    /// show "no matching endpoint" instead of a number. Never true for
    /// branch nodes.
    /// </summary>
    public bool HasDistance { get; internal set; }

    /// <summary>True when this finding can be closed by the repair command.</summary>
    public bool CanAutoRepair { get; internal set; }

    /// <summary>Branch node id (branch findings only).</summary>
    public int? BranchNodeId { get; internal set; }

    /// <summary>Localization key of the human-readable type name.</summary>
    public string TypeKey { get; internal set; } = "";
}

/// <summary>Kinds of gap/open-chain findings.</summary>
public enum GapKind
{
    /// <summary>Two open ends closer than the repair tolerance — repairable.</summary>
    SmallGap,

    /// <summary>An open end without a matching end nearby — not auto-repairable.</summary>
    OpenContourEnd,

    /// <summary>A branch junction where three or more edges meet.</summary>
    BranchNode,
}
