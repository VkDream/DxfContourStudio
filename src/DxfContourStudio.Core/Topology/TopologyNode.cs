#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Topology;

/// <summary>
/// A junction point of the topology graph: one or more geometry endpoints
/// that coincide (within <see cref="GeometryTolerance.PointEqualityTolerance"/>)
/// are collapsed into a single node. Nodes are the unit the chain builder
/// walks on, so a rectangle drawn as four separate LINE entities becomes
/// one closed contour only because all four corners are shared nodes.
///
/// Degree = number of incident edges. Degree 1 = dangling end (potential
/// gap / open chain end), degree &gt; 2 = branch junction.
/// </summary>
public sealed class TopologyNode
{
    /// <summary>Globally unique node id (assigned by the builder).</summary>
    public int Id { get; internal set; }

    /// <summary>Merged position of the coincident endpoints (average).</summary>
    public Point2 Position { get; internal set; }

    /// <summary>Number of incident edges.</summary>
    public int Degree => ConnectedEdges.Count;

    /// <summary>Edges incident to this node (stable order).</summary>
    public IReadOnlyList<TopologyEdge> ConnectedEdges { get; internal set; } = [];

    /// <summary>True when exactly one edge starts/ends here (a dangling end).</summary>
    public bool IsDangling => Degree == 1;

    /// <summary>True when three or more edges meet here (a branch junction).</summary>
    public bool IsBranch => Degree > 2;

    /// <summary>
    /// Returns the edge incident to this node that is not
    /// <paramref name="excluding"/>, or null when there is no other.
    /// </summary>
    public TopologyEdge? OtherEdge(TopologyEdge excluding)
    {
        foreach (TopologyEdge edge in ConnectedEdges)
        {
            if (edge != excluding)
            {
                return edge;
            }
        }

        return null;
    }
}
