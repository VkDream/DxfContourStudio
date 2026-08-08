#nullable enable

using DxfContourStudio.Core.Diagnostics;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Core.Topology;

namespace DxfContourStudio.Core.Contours;

/// <summary>
/// Top-level contour analysis entry point. Pure geometry — no WPF, no DXF,
/// no document knowledge. Pipeline:
///
///   entities ─► TopologyBuilder (nodes/edges)
///            ─► ContourChainBuilder (open + closed chains, circles)
///            ─► ContourAssembler (measured contours)
///            ─► GapDiagnosticsBuilder (small gaps / open ends / branches)
///            ─► NestingAnalyzer (Outer / Hole / Island)
///            ─► GeometryDiagnosticAnalyzer (zero length / duplicate /
///                self intersection) + contour validity tagging
/// </summary>
public static class ContourAnalyzer
{
    /// <summary>Runs the full pipeline for the given entities.</summary>
    public static ContourAnalysisResult Analyze(IReadOnlyList<IGeometryEntity> entities)
    {
        return Analyze(entities, GeometryTolerance.Default);
    }

    /// <summary>Runs the full pipeline with a custom tolerance policy.</summary>
    public static ContourAnalysisResult Analyze(
        IReadOnlyList<IGeometryEntity> entities,
        GeometryTolerance tolerance)
    {
        TopologyGraph graph = TopologyBuilder.Build(entities);
        IReadOnlyList<ContourChain> chains = ContourChainBuilder.Build(graph);

        var entityById = entities.ToDictionary(e => e.Id);
        var closedContours = new List<Contour>();
        var openContours = new List<Contour>();
        var openChains = new List<ContourChain>();
        foreach (ContourChain chain in chains)
        {
            Contour contour = ContourAssembler.Assemble(chain, entityById);
            if (chain.IsClosed)
            {
                closedContours.Add(contour);
            }
            else
            {
                openContours.Add(contour);
                openChains.Add(chain);
            }
        }

        // Assign unique contour ids BEFORE nesting: NestingAnalyzer keys its
        // containment bookkeeping by id and records parent links by id. The
        // final numbering is therefore fixed here and never rewritten.
        int id = 0;
        foreach (Contour c in closedContours)
        {
            c.Id = ++id;
        }

        foreach (Contour c in openContours)
        {
            c.Id = ++id;
        }

        IReadOnlyList<GapDiagnostic> diagnostics = GapDiagnosticsBuilder.Build(graph, openChains);

        IReadOnlyList<Contour> nested = NestingAnalyzer.Analyze(closedContours);

        var all = new List<Contour>(nested.Count + openContours.Count);
        all.AddRange(nested);
        all.AddRange(openContours);

        // Geometry-level diagnostics (degenerate / duplicate / self-intersection).
        var geometryDiagnostics = new List<GeometryDiagnostic>(GeometryDiagnosticAnalyzer.Analyze(entities, tolerance));
        var selfIntersections = GeometryDiagnosticAnalyzer.AnalyzeSelfIntersections(
            nested.Select(c => c.EntityIds).ToList(),
            id => entityById[id],
            tolerance);
        geometryDiagnostics.AddRange(selfIntersections);

        // Tag every contour with its validity summary and diagnostic kinds.
        foreach (Contour c in all)
        {
            TagValidity(c, selfIntersections, diagnostics, tolerance);
        }

        return new ContourAnalysisResult
        {
            Contours = all,
            Diagnostics = diagnostics,
            GeometryDiagnostics = geometryDiagnostics,
            Graph = graph,
        };
    }

    private static void TagValidity(
        Contour contour,
        IReadOnlyList<GeometryDiagnostic> selfIntersections,
        IReadOnlyList<GapDiagnostic> gapDiagnostics,
        GeometryTolerance tolerance)
    {
        var kinds = new List<DiagnosticKind>();

        if (!contour.IsClosed)
        {
            // Open chain: repairable when it ends in a small gap.
            bool touchesSmallGap = gapDiagnostics.Any(d => d.Kind == GapKind.SmallGap &&
                (contour.EntityIds.Contains(d.EntityIdA) || contour.EntityIds.Contains(d.EntityIdB)));
            contour.Validity = touchesSmallGap ? ContourValidity.GapRepairable : ContourValidity.Open;
            kinds.Add(touchesSmallGap ? DiagnosticKind.SmallGap : DiagnosticKind.OpenEndpoint);
            contour.DiagnosticKinds = kinds;
            return;
        }

        // Closed contour: self-intersecting? degenerate?
        bool selfIntersects = selfIntersections.Any(d =>
            contour.EntityIds.Contains(d.EntityIdA) && contour.EntityIds.Contains(d.EntityIdB));
        bool degenerate = contour.SignedArea is { } area && Math.Abs(area) <= tolerance.MinimumAreaTolerance;

        if (selfIntersects)
        {
            contour.Validity = ContourValidity.SelfIntersecting;
            kinds.Add(DiagnosticKind.SelfIntersection);
        }
        else if (degenerate)
        {
            contour.Validity = ContourValidity.Degenerate;
            kinds.Add(DiagnosticKind.VerySmall);
        }
        else
        {
            contour.Validity = ContourValidity.Valid;
        }

        contour.DiagnosticKinds = kinds;
    }
}
