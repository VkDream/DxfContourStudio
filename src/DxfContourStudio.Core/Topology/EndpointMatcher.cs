#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Topology;

/// <summary>
/// Collapses coincident geometry endpoints into topology nodes.
///
/// Tolerance policy (single source of truth: <see cref="GeometryTolerance"/>):
/// endpoints that coincide within <see cref="GeometryTolerance.PointEqualityTolerance"/>
/// (1e-6 mm) share one node — they are the same drawing point. Small *gaps*
/// (up to <see cref="GeometryTolerance.EndpointSnapTolerance"/>, 0.05 mm)
/// must NOT be merged here: they stay separate nodes so the gap diagnostics
/// can see them and offer a repair. A 0.02 mm gap only closes through the
/// explicit repair command, never silently inside the matcher.
///
/// Matching is done with a spatial hash over cells of size = tolerance; only
/// the 3x3 cell neighbourhood of each point is probed, which keeps the build
/// near-linear while guaranteeing that any two points within the tolerance
/// are considered.
/// </summary>
public static class EndpointMatcher
{
    /// <summary>
    /// Builds nodes from the given endpoints and returns, per endpoint, the
    /// node it was merged into. A node's position is the position of the
    /// first endpoint that created it (stable, deterministic).
    /// </summary>
    public static NodeAssignment Build(
        IReadOnlyList<GeometryEndpoint> endpoints,
        double tolerance = 0.0)
    {
        double tol = tolerance <= 0.0 ? GeometryTolerance.Default.PointEqualityTolerance : tolerance;
        var nodes = new List<TopologyNode>();
        var map = new Dictionary<EndpointRefKey, int>(endpoints.Count);
        var cellIndex = new Dictionary<(long, long), List<int>>();
        var assignedNodeOfEndpoint = new int[endpoints.Count];

        for (int i = 0; i < endpoints.Count; i++)
        {
            Point2 p = endpoints[i].Position;
            var cell = CellOf(p, tol);
            int? foundNode = null;

            for (long cx = cell.Cx - 1; cx <= cell.Cx + 1 && foundNode is null; cx++)
            {
                for (long cy = cell.Cy - 1; cy <= cell.Cy + 1 && foundNode is null; cy++)
                {
                    if (!cellIndex.TryGetValue((cx, cy), out List<int>? candidates))
                    {
                        continue;
                    }

                    foreach (int j in candidates)
                    {
                        if (p.DistanceSquaredTo(endpoints[j].Position) <= tol * tol)
                        {
                            foundNode = assignedNodeOfEndpoint[j];
                            break;
                        }
                    }
                }
            }

            int nodeId;
            if (foundNode is { } n)
            {
                nodeId = n;
            }
            else
            {
                nodeId = nodes.Count;
                nodes.Add(new TopologyNode { Id = nodeId, Position = p });
            }

            assignedNodeOfEndpoint[i] = nodeId;
            if (!cellIndex.TryGetValue(cell, out List<int>? bucket))
            {
                bucket = [];
                cellIndex[cell] = bucket;
            }

            bucket.Add(i);
            GeometryEndpoint ep = endpoints[i];
            map[new EndpointRefKey(ep.EntityId, ep.SegmentIndex, ep.IsStart)] = nodeId;
        }

        return new NodeAssignment(nodes, map);
    }

    private static (long Cx, long Cy) CellOf(Point2 p, double tol) =>
        ((long)Math.Floor(p.X / tol), (long)Math.Floor(p.Y / tol));

    /// <summary>Result of <see cref="Build"/>: the created nodes plus the endpoint→node map.</summary>
    public sealed class NodeAssignment
    {
        public NodeAssignment(IReadOnlyList<TopologyNode> nodes, IReadOnlyDictionary<EndpointRefKey, int> map)
        {
            Nodes = nodes;
            Map = map;
        }

        /// <summary>Created nodes (ids 0..n-1).</summary>
        public IReadOnlyList<TopologyNode> Nodes { get; }

        /// <summary>Endpoint → node id lookup.</summary>
        public IReadOnlyDictionary<EndpointRefKey, int> Map { get; }

        /// <summary>Node id of the given entity endpoint (throws when unknown).</summary>
        public int NodeIdOf(long entityId, int segmentIndex, bool isStart)
            => Map[new EndpointRefKey(entityId, segmentIndex, isStart)];
    }

    /// <summary>Key of the endpoint→node map: one endpoint of one entity run.</summary>
    public readonly record struct EndpointRefKey(long EntityId, int SegmentIndex, bool IsStart);

    /// <summary>One endpoint candidate handed to the matcher.</summary>
    public readonly record struct GeometryEndpoint(long EntityId, int SegmentIndex, bool IsStart, Point2 Position);
}
