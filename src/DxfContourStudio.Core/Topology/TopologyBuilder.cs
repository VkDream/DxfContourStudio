#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Topology;

/// <summary>
/// Builds a <see cref="TopologyGraph"/> from a flat entity list:
///
///   1. every LINE / ARC / POLYLINE run produces one endpoint pair and one
///      candidate edge (a POLYLINE with N segments produces N edges);
///   2. the <see cref="EndpointMatcher"/> merges coincident endpoints into
///      nodes;
///   3. edges are re-pointed at the merged nodes.
///
/// Circles never produce edges: they have no start/end pair and are returned
/// separately (<see cref="TopologyGraph.CircleEntityIds"/>) so the chain
/// builder can make them intrinsically closed contours.
///
/// Input expectation: the caller passes the entities that should participate
/// (the document's visible entities), and each entity id appears once.
/// </summary>
public static class TopologyBuilder
{
    /// <summary>Builds the graph for the given entities.</summary>
    public static TopologyGraph Build(IReadOnlyList<IGeometryEntity> entities)
    {
        var endpoints = new List<EndpointMatcher.GeometryEndpoint>();
        var edges = new List<TopologyEdge>();
        var circles = new List<long>();

        foreach (IGeometryEntity entity in entities)
        {
            switch (entity)
            {
                case LineGeometry line:
                    CollectEndpoints(endpoints, line.Id, 0, line.P0, line.P1);
                    edges.Add(new TopologyEdge
                    {
                        Id = 0, // fixed below
                        SourceEntityId = line.Id,
                        SourceEntity = line,
                        SegmentIndex = 0,
                        SegmentType = GeometryType.Line,
                        StartPoint = line.P0,
                        EndPoint = line.P1,
                        Length = line.Length,
                    });
                    break;

                case ArcGeometry arc:
                    CollectEndpoints(endpoints, arc.Id, 0, arc.StartPoint, arc.EndPoint);
                    edges.Add(new TopologyEdge
                    {
                        Id = 0,
                        SourceEntityId = arc.Id,
                        SourceEntity = arc,
                        SegmentIndex = 0,
                        SegmentType = GeometryType.Arc,
                        StartPoint = arc.StartPoint,
                        EndPoint = arc.EndPoint,
                        Length = arc.Length,
                    });
                    break;

                case PolylineGeometry polyline:
                    for (int i = 0; i < polyline.Segments.Count; i++)
                    {
                        IPathSegment segment = polyline.Segments[i];
                        CollectEndpoints(endpoints, polyline.Id, i, segment.StartPoint, segment.EndPoint);
                        edges.Add(new TopologyEdge
                        {
                            Id = 0,
                            SourceEntityId = polyline.Id,
                            SourceEntity = polyline,
                            SegmentIndex = i,
                            SegmentType = segment.GeometryType,
                            StartPoint = segment.StartPoint,
                            EndPoint = segment.EndPoint,
                            Length = segment.Length,
                        });
                    }

                    // A closed polyline carries the implicit closing run from
                    // its last vertex back to its first. When the file already
                    // repeats the first vertex (last segment ends where the
                    // first starts), nothing extra is needed; otherwise the
                    // closing run becomes a straight edge (SegmentIndex = -1).
                    if (polyline.IsClosed && polyline.Segments.Count > 0)
                    {
                        IPathSegment last = polyline.Segments[^1];
                        if (!last.EndPoint.IsCoincident(polyline.StartPoint, GeometryTolerance.Default.PointEqualityTolerance))
                        {
                            CollectEndpoints(endpoints, polyline.Id, -1, last.EndPoint, polyline.StartPoint);
                            edges.Add(new TopologyEdge
                            {
                                Id = 0,
                                SourceEntityId = polyline.Id,
                                SourceEntity = polyline,
                                SegmentIndex = -1,
                                SegmentType = GeometryType.Line,
                                StartPoint = last.EndPoint,
                                EndPoint = polyline.StartPoint,
                                Length = last.EndPoint.DistanceTo(polyline.StartPoint),
                            });
                        }
                    }

                    break;

                case CircleGeometry circle:
                    // intrinsically closed — no start/end pair, no edges.
                    circles.Add(circle.Id);
                    break;
            }
        }

        EndpointMatcher.NodeAssignment assignment = EndpointMatcher.Build(endpoints);

        var nodes = new List<TopologyNode>(assignment.Nodes.Count);
        foreach (TopologyNode node in assignment.Nodes)
        {
            nodes.Add(node);
        }

        for (int i = 0; i < edges.Count; i++)
        {
            TopologyEdge edge = edges[i];
            edge.Id = i;
            edge.StartNodeId = assignment.NodeIdOf(edge.SourceEntityId, edge.SegmentIndex, isStart: true);
            edge.EndNodeId = assignment.NodeIdOf(edge.SourceEntityId, edge.SegmentIndex, isStart: false);
        }

        // Build the node→edges adjacency in one pass (O(N+M)); a per-node
        // linear scan over all edges would be O(N·M) and blow up at 50k.
        var adjacency = new List<TopologyEdge>[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
        {
            adjacency[i] = [];
        }

        foreach (TopologyEdge edge in edges)
        {
            adjacency[edge.StartNodeId].Add(edge);
            if (edge.EndNodeId != edge.StartNodeId)
            {
                adjacency[edge.EndNodeId].Add(edge);
            }
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].ConnectedEdges = adjacency[i];
        }

        return new TopologyGraph
        {
            Nodes = nodes,
            Edges = edges,
            CircleEntityIds = circles,
        };
    }

    private static void CollectEndpoints(
        List<EndpointMatcher.GeometryEndpoint> endpoints,
        long entityId,
        int segmentIndex,
        Point2 start,
        Point2 end)
    {
        endpoints.Add(new EndpointMatcher.GeometryEndpoint(entityId, segmentIndex, IsStart: true, start));
        endpoints.Add(new EndpointMatcher.GeometryEndpoint(entityId, segmentIndex, IsStart: false, end));
    }
}
