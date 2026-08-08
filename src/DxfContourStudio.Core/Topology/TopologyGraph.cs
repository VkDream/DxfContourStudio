#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Topology;

/// <summary>
/// The immutable result of topology building: the node/edge graph plus the
/// census that the diagnostics panel shows. Built once per analysis by
/// <see cref="TopologyBuilder"/>; consumers (chain builder, gap diagnostics)
/// only read it.
/// </summary>
public sealed class TopologyGraph
{
    /// <summary>All nodes (stable order, ids 0..n-1).</summary>
    public IReadOnlyList<TopologyNode> Nodes { get; internal set; } = [];

    /// <summary>All edges (stable order, ids 0..m-1).</summary>
    public IReadOnlyList<TopologyEdge> Edges { get; internal set; } = [];

    /// <summary>Source entity ids that are circles (no edge, intrinsically closed).</summary>
    public IReadOnlyList<long> CircleEntityIds { get; internal set; } = [];

    /// <summary>Total number of topology nodes.</summary>
    public int NodeCount => Nodes.Count;

    /// <summary>Total number of topology edges.</summary>
    public int EdgeCount => Edges.Count;

    /// <summary>Nodes where three or more edges meet.</summary>
    public int BranchNodeCount => Nodes.Count(n => n.IsBranch);

    /// <summary>Nodes where exactly one edge ends (open ends, potential gaps).</summary>
    public int DanglingNodeCount => Nodes.Count(n => n.IsDangling);

    /// <summary>Returns the node with the given id (throws when unknown).</summary>
    public TopologyNode GetNode(int id) => Nodes[id];

    /// <summary>Returns the edge with the given id (throws when unknown).</summary>
    public TopologyEdge GetEdge(int id) => Edges[id];

    /// <summary>True when the graph holds at least one edge.</summary>
    public bool IsEmpty => Edges.Count == 0;
}
