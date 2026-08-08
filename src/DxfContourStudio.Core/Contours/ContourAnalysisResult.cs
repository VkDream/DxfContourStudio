#nullable enable

using DxfContourStudio.Core.Diagnostics;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Core.Topology;

namespace DxfContourStudio.Core.Contours;

/// <summary>
/// The full result of one contour analysis: the assembled contours, the gap
/// diagnostics, the geometry-level diagnostics and the topology census.
/// Everything the UI panels show is derived from this object.
/// </summary>
public sealed class ContourAnalysisResult
{
    /// <summary>All contours in analysis order (open chains included).</summary>
    public IReadOnlyList<Contour> Contours { get; internal set; } = [];

    /// <summary>All gap / open-chain findings (topology pass).</summary>
    public IReadOnlyList<GapDiagnostic> Diagnostics { get; internal set; } = [];

    /// <summary>All geometry-level findings (zero length, duplicates, self intersections).</summary>
    public IReadOnlyList<GeometryDiagnostic> GeometryDiagnostics { get; internal set; } = [];

    /// <summary>Topology census (nodes / edges / branches / dangling ends).</summary>
    public TopologyGraph Graph { get; internal set; } = null!;

    /// <summary>Number of closed contours.</summary>
    public int ClosedCount => Contours.Count(c => c.IsClosed);

    /// <summary>Number of open contours (chains that did not close).</summary>
    public int OpenCount => Contours.Count(c => !c.IsClosed);

    /// <summary>Closed contours, outermost first (nesting pass output).</summary>
    public IReadOnlyList<Contour> ClosedContours => Contours.Where(c => c.IsClosed).ToList();

    /// <summary>Outer boundaries (depth 0).</summary>
    public int OuterCount => ClosedContours.Count(c => c.Role == ContourRole.Outer);

    /// <summary>Holes (depth 1, 3, ...).</summary>
    public int HoleCount => ClosedContours.Count(c => c.Role == ContourRole.Hole);

    /// <summary>Islands inside holes (depth 2, 4, ...).</summary>
    public int IslandCount => ClosedContours.Count(c => c.Role == ContourRole.Island);

    /// <summary>Repairable small-gap findings.</summary>
    public int SmallGapCount => Diagnostics.Count(d => d.Kind == GapKind.SmallGap);

    /// <summary>Open ends without a repairable match.</summary>
    public int OpenEndCount => Diagnostics.Count(d => d.Kind == GapKind.OpenContourEnd);

    /// <summary>Branch junction findings.</summary>
    public int BranchCount => Diagnostics.Count(d => d.Kind == GapKind.BranchNode);

    /// <summary>Zero-length geometry findings.</summary>
    public int ZeroLengthCount => GeometryDiagnostics.Count(d => d.Kind == DiagnosticKind.ZeroLength);

    /// <summary>Very-small geometry findings.</summary>
    public int VerySmallCount => GeometryDiagnostics.Count(d => d.Kind == DiagnosticKind.VerySmall);

    /// <summary>Duplicate entity pairs.</summary>
    public int DuplicateCount => GeometryDiagnostics.Count(d => d.Kind == DiagnosticKind.Duplicate);

    /// <summary>Self-intersection findings.</summary>
    public int SelfIntersectionCount => GeometryDiagnostics.Count(d => d.Kind == DiagnosticKind.SelfIntersection);
}
