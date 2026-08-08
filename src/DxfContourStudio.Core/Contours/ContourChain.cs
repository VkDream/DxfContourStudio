#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Contours;

/// <summary>
/// One oriented step of a contour traversal: an edge plus the direction the
/// walk crossed it. <see cref="Forward"/> = source direction (StartPoint →
/// EndPoint); otherwise the walk reversed the edge.
/// </summary>
public readonly record struct ChainStep(Topology.TopologyEdge Edge, bool Forward);

/// <summary>
/// The raw result of walking the topology graph: either a closed loop or an
/// open chain. Open chains start at a dangling node (degree 1) and stop at
/// the next dangling node, a branch junction, or — for circles — the circle
/// is turned into a closed chain without any steps.
/// </summary>
public sealed class ContourChain
{
    /// <summary>Ordered steps of the walk (empty for a circle chain).</summary>
    public IReadOnlyList<ChainStep> Steps { get; init; } = [];

    /// <summary>True when the walk returned to its start node.</summary>
    public bool IsClosed { get; init; }

    /// <summary>True for an intrinsically closed circle (no steps).</summary>
    public bool IsCircle { get; init; }

    /// <summary>Circle source entity id (only when <see cref="IsCircle"/>).</summary>
    public long CircleEntityId { get; init; }

    /// <summary>Node id the walk started at (open chains: the dangling end).</summary>
    public int StartNodeId { get; init; }

    /// <summary>Node id the walk stopped at (open chains only).</summary>
    public int EndNodeId { get; init; }

    /// <summary>True when the open chain ends at a branch junction.</summary>
    public bool EndsAtBranch { get; init; }

    /// <summary>Accumulated path length in millimeters.</summary>
    public double Length { get; init; }
}
