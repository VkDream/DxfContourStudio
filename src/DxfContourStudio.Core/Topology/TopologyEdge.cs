#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Topology;

/// <summary>
/// One elementary connection of the topology graph: a single straight or
/// arc run of a source entity, mapped onto two topology nodes.
///
/// - LINE            → one edge (segment index 0)
/// - ARC             → one edge (segment index 0)
/// - POLYLINE        → one edge per IPathSegment (segment index = index)
/// - CIRCLE          → never an edge; it is intrinsically closed and is
///                     turned directly into a closed contour by the chain
///                     builder (a circle has no start/end pair, so faking
///                     one would create a fake node).
///
/// An edge is orientation-agnostic: <see cref="StartPoint"/>/<see cref="EndPoint"/>
/// are the *source* direction; a traversal can reverse it (CanReverse).
/// </summary>
public sealed class TopologyEdge
{
    /// <summary>Globally unique edge id (assigned by the builder).</summary>
    public int Id { get; internal set; }

    /// <summary>Id of the source entity that contributed this run.</summary>
    public long SourceEntityId { get; internal set; }

    /// <summary>The source entity itself (needed to resolve arc parameters).</summary>
    public IGeometryEntity SourceEntity { get; internal set; } = null!;

    /// <summary>
    /// Index of the segment inside the source entity (0 for LINE/ARC,
    /// polyline segment index for POLYLINE). Used to locate the run for
    /// repair and rendering.
    /// </summary>
    public int SegmentIndex { get; internal set; }

    /// <summary>Whether this run is straight or an arc.</summary>
    public GeometryType SegmentType { get; internal set; }

    /// <summary>Start point in the source direction (millimeters).</summary>
    public Point2 StartPoint { get; internal set; }

    /// <summary>End point in the source direction (millimeters).</summary>
    public Point2 EndPoint { get; internal set; }

    /// <summary>Path length of this run (millimeters).</summary>
    public double Length { get; internal set; }

    /// <summary>Node id the source-direction start maps to.</summary>
    public int StartNodeId { get; internal set; }

    /// <summary>Node id the source-direction end maps to.</summary>
    public int EndNodeId { get; internal set; }

    /// <summary>Always true in the current model (LINE/ARC/POLYLINE runs are reversible).</summary>
    public bool CanReverse => true;

    /// <summary>
    /// Returns the endpoint of this edge at the side of the given node,
    /// honoring the traversal direction (used by the chain builder).
    /// </summary>
    public Point2 EndpointAt(int nodeId, bool forward)
    {
        if (forward)
        {
            return nodeId == StartNodeId ? StartPoint : EndPoint;
        }

        return nodeId == EndNodeId ? EndPoint : StartPoint;
    }

    /// <summary>
    /// Returns the node id at the *other* side of this edge, given the node
    /// we are standing on.
    /// </summary>
    public int OppositeNode(int nodeId) => nodeId == StartNodeId ? EndNodeId : StartNodeId;
}
