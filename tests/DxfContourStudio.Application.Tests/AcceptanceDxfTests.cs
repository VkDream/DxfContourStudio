#nullable enable

using System.IO;
using DxfContourStudio.Application.Commands;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Imports;
using DxfContourStudio.Core.Contours;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Dxf.Infrastructure;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Static acceptance of the shipment acceptance samples (ADR acceptance
/// chain). These run against the real signed-in testdata files through the
/// real ACadSharp reader 鈥?same data the UI smoke opens 鈥?and assert the
/// expected geometry results without needing any interactive UI.
/// </summary>
public class AcceptanceDxfTests
{
    private static string SamplePath(string relative) =>
        Path.Combine(AppContext.BaseDirectory, relative);

    private static CadDocument Load(string fileName)
    {
        var doc = new CadDocument();
        var reader = new AcadSharpDxfReader();
        var service = new DxfImportService(reader);
        DxfImportOutcome outcome = service.Import(SamplePath("testdata/dxf/" + fileName), doc);
        Assert.True(outcome.IsSuccess, "import failed: " + (outcome.ErrorMessage ?? "unknown"));
        Assert.NotEmpty(doc.Entities);
        return doc;
    }

    // ---- small_gap_003.dxf: two LINEs 0.030 mm apart on one axis ----
    // Topology contract: this is an OPEN CHAIN with an internal 0.030 mm gap.
    // Repair joins the chain (open count 2 -> 1) but NEVER closes it.
    [Fact]
    public void SmallGap_RealFile_AutoRepairableInternalGap()
    {
        CadDocument doc = Load("small_gap_003.dxf");

        Assert.Equal(2, doc.Entities.Count);
        ContourAnalysisResult result = ContourAnalyzer.Analyze(doc.Entities);

        Assert.Equal(1, result.SmallGapCount);
        Assert.Equal(0, result.ClosedCount);
        Assert.Equal(2, result.OpenCount);
        GapDiagnostic gap = Assert.Single(result.Diagnostics, d => d.Kind == GapKind.SmallGap);
        Assert.True(gap.CanAutoRepair);
        Assert.Equal(0.03, gap.Distance, 9);
        Assert.True(gap.HasDistance);

        var command = new RepairGapCommand(doc, gap);
        command.Execute();

        ContourAnalysisResult repaired = ContourAnalyzer.Analyze(doc.Entities);
        Assert.Equal(0, repaired.SmallGapCount);
        Assert.Equal(1, repaired.OpenCount);   // one continuous chain
        Assert.Equal(0, repaired.ClosedCount); // still an open chain
    }

    // ---- outer_hole.dxf: one outer rectangle plus one inner rectangle ----
    [Fact]
    public void OuterHole_RealFileAnalysis_DetectsOneOuterAndOneHole()
    {
        CadDocument doc = Load("outer_hole.dxf");

        ContourAnalysisResult result = ContourAnalyzer.Analyze(doc.Entities);

        Assert.Equal(2, result.ClosedCount);
        Assert.Equal(0, result.OpenCount);
        Assert.Equal(1, result.OuterCount);
        Assert.Equal(1, result.HoleCount);
        Assert.Equal(0, result.IslandCount);
    }

    // ---- basic-scene.dxf: the acceptance "5 visible entities" plan ----
    [Fact]
    public void BasicScene_RealFile_HoldsFiveEntities()
    {
        CadDocument doc = Load("basic-scene.dxf");
        // GUI acceptance expects exactly these 5 entities visible and renderable.
        Assert.Equal(5, doc.Entities.Count);
        Assert.Equal(5, doc.VisibleEntities.Count);
        Assert.NotNull(doc.OverallBounds);
    }
}
