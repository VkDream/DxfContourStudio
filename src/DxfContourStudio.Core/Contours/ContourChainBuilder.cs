#nullable enable

using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Core.Topology;

namespace DxfContourStudio.Core.Contours;

/// <summary>
/// Walks a <see cref="TopologyGraph"/> into chains:
///
/// - open chains start at every dangling node (degree 1) and follow the
///   unique incident edge, walking straight through degree-2 nodes until
///   they reach the next dangling node or a branch junction (degree &gt; 2);
/// - closed chains are the remaining unvisited loops: walk from an edge,
///   through degree-2 nodes, back to the start node;
/// - circles (no edges) become intrinsically closed circle chains.
///
/// The walk is fully independent of the input entity order — ordering comes
/// from the graph, not from the DXF file.
/// </summary>
public static class ContourChainBuilder
{
    /// <summary>Builds all chains of the graph, in stable order.</summary>
    public static IReadOnlyList<ContourChain> Build(TopologyGraph graph)
    {
        var chains = new List<ContourChain>();
        var visited = new bool[graph.EdgeCount];

        // ---- open chains from dangling nodes ----
        foreach (TopologyNode node in graph.Nodes)
        {
            if (!node.IsDangling || node.ConnectedEdges.Count == 0)
            {
                continue;
            }

            TopologyEdge edge = node.ConnectedEdges[0];
            if (visited[edge.Id])
            {
                continue;
            }

            chains.Add(Walk(graph, visited, node.Id, edge));
        }

        // ---- closed chains: any remaining unvisited loop ----
        for (int i = 0; i < graph.EdgeCount; i++)
        {
            if (visited[i])
            {
                continue;
            }

            TopologyEdge edge = graph.GetEdge(i);
            ContourChain chain = Walk(graph, visited, edge.StartNodeId, edge);
            if (!chain.IsClosed)
            {
                // A path that could not close (should not happen after the
                // dangling pass; keep it as an open chain so nothing is lost).
                chains.Add(chain);
            }
            else
            {
                chains.Add(chain);
            }
        }

        // ---- circles ----
        foreach (long circleId in graph.CircleEntityIds)
        {
            chains.Add(new ContourChain
            {
                Steps = [],
                IsClosed = true,
                IsCircle = true,
                CircleEntityId = circleId,
                Length = 0,
            });
        }

        return chains;
    }

    /// <summary>
    /// Walks from <paramref name="startNodeId"/> along <paramref name="firstEdge"/>,
    /// marking edges visited. Stops at a dangling node, a branch junction or
    /// when it returns to the start node (closed loop).
    /// </summary>
    private static ContourChain Walk(TopologyGraph graph, bool[] visited, int startNodeId, TopologyEdge firstEdge)
    {
        var steps = new List<ChainStep>();
        int curNode = startNodeId;
        TopologyEdge edge = firstEdge;
        bool forward = edge.StartNodeId == curNode;
        double length = 0.0;
        bool closed = false;
        int endNode = startNodeId;
        bool endsAtBranch = false;

        while (true)
        {
            visited[edge.Id] = true;
            steps.Add(new ChainStep(edge, forward));
            length += edge.Length;

            int nextNode = edge.OppositeNode(curNode);
            if (nextNode == startNodeId)
            {
                closed = true;
                endNode = startNodeId;
                break;
            }

            TopologyNode next = graph.GetNode(nextNode);
            if (next.Degree != 2)
            {
                // dangling end or branch junction — the chain ends here.
                closed = false;
                endNode = nextNode;
                endsAtBranch = next.IsBranch;
                break;
            }

            TopologyEdge? nextEdge = next.OtherEdge(edge);
            if (nextEdge is null || visited[nextEdge.Id])
            {
                closed = false;
                endNode = nextNode;
                break;
            }

            curNode = nextNode;
            edge = nextEdge;
            forward = edge.StartNodeId == curNode;
        }

        return new ContourChain
        {
            Steps = steps,
            IsClosed = closed,
            StartNodeId = startNodeId,
            EndNodeId = endNode,
            EndsAtBranch = endsAtBranch,
            Length = length,
        };
    }
}
