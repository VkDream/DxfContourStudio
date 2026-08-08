#nullable enable

using System;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Core.Topology;

namespace DxfContourStudio.Core.Contours;

/// <summary>
/// Derives the gap diagnostics from the open chains:
///
/// - every open chain ends at a dangling node (degree 1); two dangling ends
///   closer than <see cref="GeometryTolerance.EndpointSnapTolerance"/> form a
///   <see cref="GapKind.SmallGap"/> finding (repairable, the repair command
///   moves both endpoints to their midpoint);
/// - a dangling end without any matching end nearby is an
///   <see cref="GapKind.OpenContourEnd"/> finding (not auto-repairable);
/// - open chains that stop at a branch junction produce one
///   <see cref="GapKind.BranchNode"/> finding per junction.
///
/// Pairing is greedy nearest-neighbour, which is deterministic for the small
/// drawings the tool targets (a correct general matching is out of scope).
/// </summary>
public static class GapDiagnosticsBuilder
{
    private sealed class ChainEnd
    {
        public required ContourChain Chain { get; init; }

        public required int NodeId { get; init; }

        public required Point2 Position { get; init; }

        public required long EntityId { get; init; }

        public required int SegmentIndex { get; init; }

        public required bool IsStart { get; init; }

        public bool Paired { get; set; }
    }

    /// <summary>
    /// Builds the diagnostics for the given open chains (graph is used to
    /// resolve node positions).
    /// </summary>
    public static IReadOnlyList<GapDiagnostic> Build(TopologyGraph graph, IReadOnlyList<ContourChain> openChains)
    {
        var diagnostics = new List<GapDiagnostic>();
        var ends = new List<ChainEnd>();
        var branchIds = new HashSet<int>();

        foreach (ContourChain chain in openChains)
        {
            if (chain.Steps.Count == 0)
            {
                continue;
            }

            // start end (always a dangling node)
            ChainStep first = chain.Steps[0];
            TopologyNode startNode = graph.GetNode(chain.StartNodeId);
            bool startForward = first.Edge.StartNodeId == chain.StartNodeId;
            ends.Add(new ChainEnd
            {
                Chain = chain,
                NodeId = chain.StartNodeId,
                Position = startNode.Position,
                EntityId = first.Edge.SourceEntityId,
                SegmentIndex = first.Edge.SegmentIndex,
                IsStart = startForward,
            });

            // end end: dangling → gap candidate; branch → branch diagnostic
            if (chain.EndsAtBranch)
            {
                branchIds.Add(chain.EndNodeId);
                continue;
            }

            TopologyNode endNode = graph.GetNode(chain.EndNodeId);
            if (!endNode.IsDangling)
            {
                continue;
            }

            ChainStep last = chain.Steps[^1];
            bool lastForward = last.Edge.EndNodeId == chain.EndNodeId;
            ends.Add(new ChainEnd
            {
                Chain = chain,
                NodeId = chain.EndNodeId,
                Position = endNode.Position,
                EntityId = last.Edge.SourceEntityId,
                SegmentIndex = last.Edge.SegmentIndex,
                IsStart = !lastForward,
            });
        }

        // greedy nearest-neighbour pairing of dangling ends. A spatial hash
        // (cells of size = tolerance) keeps this near-linear instead of O(n²):
        // only candidates in the 3x3 cell neighbourhood of each end are probed.
        // All ends are inserted first, then every end finds its nearest
        // neighbour among ALL others (not just earlier ones), so the pairing
        // is symmetric and matches the previous O(n²) semantics.
        double tol = GeometryTolerance.Default.EndpointSnapTolerance;
        var cellIndex = new Dictionary<(long, long), List<int>>();

        for (int i = 0; i < ends.Count; i++)
        {
            var (cx, cy) = CellOf(ends[i].Position, tol);
            if (!cellIndex.TryGetValue((cx, cy), out List<int>? bucket))
            {
                bucket = [];
                cellIndex[(cx, cy)] = bucket;
            }

            bucket.Add(i);
        }

        int[] nearest = new int[ends.Count];
        double[] bestDist = new double[ends.Count];
        for (int i = 0; i < ends.Count; i++)
        {
            nearest[i] = -1;
            bestDist[i] = double.MaxValue;
            Point2 p = ends[i].Position;
            var (cx, cy) = CellOf(p, tol);
            for (long x = cx - 1; x <= cx + 1; x++)
            {
                for (long y = cy - 1; y <= cy + 1; y++)
                {
                    if (!cellIndex.TryGetValue((x, y), out List<int>? bucket))
                    {
                        continue;
                    }

                    foreach (int j in bucket)
                    {
                        if (i == j)
                        {
                            continue;
                        }

                        double d = p.DistanceTo(ends[j].Position);
                        if (d < bestDist[i])
                        {
                            bestDist[i] = d;
                            nearest[i] = j;
                        }
                    }
                }
            }
        }

        for (int i = 0; i < ends.Count; i++)
        {
            if (ends[i].Paired)
            {
                continue;
            }

            int best = nearest[i];
            double bestD = bestDist[i];
            if (best < 0 || ends[best].Paired || bestD > tol)
            {
                // The precomputed nearest is taken or out of range: fall back
                // to scanning this end's cell neighbourhood for the nearest
                // still-unpaired candidate (buckets stay small, so this keeps
                // the pass near-linear).
                best = -1;
                bestD = double.MaxValue;
                Point2 p = ends[i].Position;
                var (cx, cy) = CellOf(p, tol);
                for (long x = cx - 1; x <= cx + 1; x++)
                {
                    for (long y = cy - 1; y <= cy + 1; y++)
                    {
                        if (!cellIndex.TryGetValue((x, y), out List<int>? bucket))
                        {
                            continue;
                        }

                        foreach (int j in bucket)
                        {
                            if (i == j || ends[j].Paired)
                            {
                                continue;
                            }

                            double d = p.DistanceTo(ends[j].Position);
                            if (d < bestD)
                            {
                                bestD = d;
                                best = j;
                            }
                        }
                    }
                }

                if (best < 0 || bestD > tol)
                {
                    // No candidate end found at all (best < 0): the open end
                    // has no measurable distance — flag HasDistance=false so
                    // the UI shows "no matching endpoint" instead of a
                    // double.MaxValue sentinel leaking into the display.
                    // With a candidate beyond tolerance, the distance is real.
                    diagnostics.Add(new GapDiagnostic
                    {
                        Kind = GapKind.OpenContourEnd,
                        EntityIdA = ends[i].EntityId,
                        SegmentIndexA = ends[i].SegmentIndex,
                        IsStartA = ends[i].IsStart,
                        PositionA = ends[i].Position,
                        Distance = best < 0 ? 0 : bestD,
                        HasDistance = best >= 0 && double.IsFinite(bestD),
                        CanAutoRepair = false,
                        TypeKey = LocalizationKeysDiag.OpenEnd,
                    });
                    continue;
                }
            }

            ends[i].Paired = true;
            ends[best].Paired = true;
            diagnostics.Add(new GapDiagnostic
            {
                Kind = GapKind.SmallGap,
                EntityIdA = ends[i].EntityId,
                SegmentIndexA = ends[i].SegmentIndex,
                IsStartA = ends[i].IsStart,
                EntityIdB = ends[best].EntityId,
                SegmentIndexB = ends[best].SegmentIndex,
                IsStartB = ends[best].IsStart,
                PositionA = ends[i].Position,
                PositionB = ends[best].Position,
                Distance = bestD,
                HasDistance = true,
                CanAutoRepair = true,
                TypeKey = LocalizationKeysDiag.SmallGap,
            });
        }

        foreach (int branchId in branchIds)
        {
            diagnostics.Add(new GapDiagnostic
            {
                Kind = GapKind.BranchNode,
                PositionA = graph.GetNode(branchId).Position,
                CanAutoRepair = false,
                BranchNodeId = branchId,
                TypeKey = LocalizationKeysDiag.Branch,
            });
        }

        return diagnostics;
    }

    private static (long Cx, long Cy) CellOf(Point2 p, double tol) =>
        ((long)Math.Floor(p.X / tol), (long)Math.Floor(p.Y / tol));
}

/// <summary>
/// Localization keys used by the diagnostics builder. Kept separate from the
/// Application-layer key table so Core stays dependency-free (the Application
/// layer forwards these keys into its own table).
/// </summary>
internal static class LocalizationKeysDiag
{
    public const string SmallGap = "Diag.SmallGap";
    public const string OpenEnd = "Diag.OpenEnd";
    public const string Branch = "Diag.Branch";
}
