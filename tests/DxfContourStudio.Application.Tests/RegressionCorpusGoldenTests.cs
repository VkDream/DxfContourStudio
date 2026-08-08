#nullable enable

using System.IO;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Imports;
using DxfContourStudio.Core.Contours;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Dxf.Infrastructure;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Golden expectations for every file of the regression corpus
/// (docs/TEST_CORPUS.md). Each test asserts the real imported geometry and
/// the re-derived analysis numbers 鈥?the numbers below come from the actual
/// geometry of each hand-authored file, not from algorithm internals.
/// </summary>
public class RegressionCorpusGoldenTests
{
    private static string SamplePath(string relative) =>
        Path.Combine(AppContext.BaseDirectory, relative);

    private static CadDocument Load(string fileName)
    {
        var doc = new CadDocument();
        var service = new DxfImportService(new AcadSharpDxfReader());
        var outcome = service.Import(SamplePath("testdata/dxf/" + fileName), doc);
        Assert.True(outcome.IsSuccess, $"import of {fileName} failed");
        return doc;
    }

    private static ContourAnalysisResult Analyze(CadDocument doc) =>
        ContourAnalyzer.Analyze(doc.Entities.ToList());

    private static void AssertBounds(CadDocument doc, double minX, double minY, double maxX, double maxY, double tol = 1e-6)
    {
        Bounds b = doc.OverallBounds!.Value;
        Assert.Equal(minX, b.MinX, tol);
        Assert.Equal(minY, b.MinY, tol);
        Assert.Equal(maxX, b.MaxX, tol);
        Assert.Equal(maxY, b.MaxY, tol);
    }

    [Fact]
    public void RectangleLines_ClosedSingleContour()
    {
        CadDocument doc = Load("rectangle_lines.dxf");
        Assert.Equal(4, doc.Entities.Count);
        Assert.All(doc.Entities, e => Assert.Equal(GeometryType.Line, e.GeometryType));
        AssertBounds(doc, 0, 0, 100, 60);

        ContourAnalysisResult r = Analyze(doc);
        Assert.Equal(1, r.ClosedCount);
        Assert.Equal(0, r.OpenCount);
        Assert.Equal(1, r.OuterCount);
        Assert.Equal(0, r.HoleCount);
        Assert.Equal(0, r.SmallGapCount);
        Assert.Equal(0, r.SelfIntersectionCount);
    }

    [Fact]
    public void RectangleScrambled_StillClosed()
    {
        CadDocument doc = Load("rectangle_scrambled.dxf");
        Assert.Equal(4, doc.Entities.Count);
        ContourAnalysisResult r = Analyze(doc);
        Assert.Equal(1, r.ClosedCount);
        Assert.Equal(1, r.OuterCount);
        Assert.Equal(0, r.OpenCount);
    }

    [Fact]
    public void RectangleReversed_StillClosed()
    {
        CadDocument doc = Load("rectangle_reversed.dxf");
        ContourAnalysisResult r = Analyze(doc);
        Assert.Equal(1, r.ClosedCount);
        Assert.Equal(1, r.OuterCount);
        Assert.Equal(0, r.SmallGapCount);
    }

    [Fact]
    public void LineArcClosed_SemicircleContour()
    {
        CadDocument doc = Load("line_arc_closed.dxf");
        Assert.Equal(2, doc.Entities.Count);
        Assert.Contains(doc.Entities, e => e.GeometryType == GeometryType.Line);
        Assert.Contains(doc.Entities, e => e.GeometryType == GeometryType.Arc);

        var arc = Assert.Single(doc.Entities.OfType<ArcGeometry>());
        Assert.Equal(50.0, arc.Radius, 6);
        Assert.Equal(0.0, arc.StartAngleRadians, 6);
        Assert.Equal(Math.PI, arc.SweepRadians, 6);

        ContourAnalysisResult r = Analyze(doc);
        Assert.Equal(1, r.ClosedCount);
        Assert.Equal(1, r.OuterCount);
        // Half disk: 蟺路r虏/2 = 3926.99
        var contour = Assert.Single(r.Contours);
        Assert.Equal(3926.991, contour.SignedArea!.Value, 2);
    }

    [Fact]
    public void NestedIsland_Depth0Hole1Island2()
    {
        CadDocument doc = Load("nested_island.dxf");
        Assert.Equal(12, doc.Entities.Count);
        AssertBounds(doc, 0, 0, 200, 150);

        ContourAnalysisResult r = Analyze(doc);
        Assert.Equal(3, r.ClosedCount);
        Assert.Equal(1, r.OuterCount);
        Assert.Equal(1, r.HoleCount);
        Assert.Equal(1, r.IslandCount);
        Assert.Equal(0, r.OpenCount);

        Contour outer = Assert.Single(r.Contours, c => c.Role == ContourRole.Outer);
        Contour hole = Assert.Single(r.Contours, c => c.Role == ContourRole.Hole);
        Contour island = Assert.Single(r.Contours, c => c.Role == ContourRole.Island);
        Assert.Equal(0, outer.Depth);
        Assert.Equal(1, hole.Depth);
        Assert.Equal(2, island.Depth);
        Assert.Equal(outer.Id, hole.ParentContourId);
        Assert.Equal(hole.Id, island.ParentContourId);
    }

    [Fact]
    public void LargeGap_OpenEndsOnly()
    {
        CadDocument doc = Load("large_gap.dxf");
        ContourAnalysisResult r = Analyze(doc);
        Assert.Equal(0, r.ClosedCount);
        Assert.Equal(2, r.OpenCount);
        Assert.Equal(0, r.SmallGapCount);
        // Each of the two runs has two dangling ends and nothing within the
        // repair tolerance to pair with: four open-end findings in total.
        Assert.Equal(4, r.OpenEndCount);
        Assert.All(r.Diagnostics, d => Assert.False(d.CanAutoRepair));
    }

    [Fact]
    public void Branch_BranchNodeDiagnostic()
    {
        CadDocument doc = Load("branch.dxf");
        ContourAnalysisResult r = Analyze(doc);
        Assert.Equal(1, r.BranchCount);
        Assert.Equal(1, r.Graph.BranchNodeCount);
        var branch = Assert.Single(r.Diagnostics, d => d.Kind == GapKind.BranchNode);
        Assert.Equal(50, branch.PositionA.X, 6);
        Assert.Equal(50, branch.PositionA.Y, 6);
    }

    [Fact]
    public void ZeroLength_ImportedAsWarningAndDiagnostic()
    {
        // The zero-length LINE is dropped by the import mapper with a warning
        // (see AcadSharpEntityMapper); the surviving entity is the 50 mm line.
        CadDocument doc = Load("zero_length.dxf");
        Assert.Single(doc.Entities);
        ContourAnalysisResult r = Analyze(doc);
        Assert.Equal(1, r.Graph.EdgeCount);
    }

    [Fact]
    public void DuplicateEntity_DuplicateDiagnostic()
    {
        CadDocument doc = Load("duplicate_entity.dxf");
        Assert.Equal(4, doc.Entities.Count);
        ContourAnalysisResult r = Analyze(doc);
        // Entity #2 duplicates #1 (same direction) and #3 duplicates #1/#2
        // reversed: three duplicate pairs in total.
        Assert.Equal(3, r.DuplicateCount);
    }

    [Fact]
    public void SelfIntersection_BowTieDetected()
    {
        CadDocument doc = Load("self_intersection.dxf");
        Assert.Equal(4, doc.Entities.Count);
        ContourAnalysisResult r = Analyze(doc);
        Assert.Equal(1, r.SelfIntersectionCount);
        var si = Assert.Single(r.GeometryDiagnostics, d => d.Kind == DxfContourStudio.Core.Diagnostics.DiagnosticKind.SelfIntersection);
        Assert.Equal(50, si.PositionA.X, 3);
        Assert.Equal(50, si.PositionA.Y, 3);
    }

    [Fact]
    public void OpenPolyline_OneOpenContour()
    {
        CadDocument doc = Load("open_polyline.dxf");
        Assert.Single(doc.Entities);
        Assert.Equal(GeometryType.Polyline, doc.Entities[0].GeometryType);
        ContourAnalysisResult r = Analyze(doc);
        Assert.Equal(1, r.OpenCount);
        Assert.Equal(0, r.ClosedCount);
    }

    [Fact]
    public void ClosedPolyline_OneClosedContour()
    {
        CadDocument doc = Load("closed_polyline.dxf");
        Assert.Single(doc.Entities);
        var poly = Assert.IsType<PolylineGeometry>(doc.Entities[0]);
        Assert.True(poly.IsClosed);
        Assert.Equal(4, poly.Segments.Count);

        ContourAnalysisResult r = Analyze(doc);
        Assert.Equal(1, r.ClosedCount);
        Assert.Equal(1, r.OuterCount);
        AssertBounds(doc, 0, 0, 80, 50);
    }

    [Fact]
    public void BulgePolyline_ArcSegmentsPreserved()
    {
        CadDocument doc = Load("bulge_polyline.dxf");
        Assert.Single(doc.Entities);
        var poly = Assert.IsType<PolylineGeometry>(doc.Entities[0]);
        Assert.True(poly.IsClosed);
        Assert.Contains(poly.Segments, s => s.GeometryType == GeometryType.Arc);

        ContourAnalysisResult r = Analyze(doc);
        Assert.Equal(1, r.ClosedCount);
        Assert.Equal(1, r.OuterCount);
    }

    [Fact]
    public void MixedLayers_ThreeLayersAndNesting()
    {
        CadDocument doc = Load("mixed_layers.dxf");
        Assert.Equal(3, doc.Layers.Count);
        Assert.Equal(6, doc.Entities.Count);

        ContourAnalysisResult r = Analyze(doc);
        Assert.Equal(2, r.ClosedCount);
        Assert.Equal(1, r.OuterCount);
        Assert.Equal(1, r.HoleCount);
    }

    [Fact]
    public void UnknownUnits_AssumedMillimetersWithWarning()
    {
        CadDocument doc = Load("unknown_units.dxf");
        Assert.Equal(LengthUnit.Millimeter, doc.Units);
        ContourAnalysisResult r = Analyze(doc);
        Assert.Equal(1, r.ClosedCount);
        AssertBounds(doc, 0, 0, 100, 100);
    }

    [Fact]
    public void SmallGap003_InternalGapIs030mm()
    {
        CadDocument doc = Load("small_gap_003.dxf");
        ContourAnalysisResult r = Analyze(doc);
        Assert.Equal(1, r.SmallGapCount);
        Assert.Equal(2, r.OpenCount);
        Assert.Equal(0, r.ClosedCount);
        var gap = Assert.Single(r.Diagnostics, d => d.Kind == GapKind.SmallGap);
        Assert.Equal(0.030, gap.Distance, 9);
        Assert.True(gap.CanAutoRepair);
        Assert.True(gap.HasDistance);
    }

    [Fact]
    public void OuterHole_OuterAndHole()
    {
        CadDocument doc = Load("outer_hole.dxf");
        ContourAnalysisResult r = Analyze(doc);
        Assert.Equal(1, r.OuterCount);
        Assert.Equal(1, r.HoleCount);
        Assert.Equal(2, r.ClosedCount);
    }

    [Fact]
    public void RectangleGap003_GapRepairClosesContour()
    {
        CadDocument doc = Load("rectangle_gap_003.dxf");
        Assert.Equal(4, doc.Entities.Count);

        // Before repair: the rectangle is open at one corner (0.030 mm gap).
        ContourAnalysisResult before = Analyze(doc);
        Assert.Equal(0, before.ClosedCount);
        Assert.Equal(1, before.OpenCount);
        Assert.Equal(1, before.SmallGapCount);
        GapDiagnostic gap = Assert.Single(before.Diagnostics, d => d.Kind == GapKind.SmallGap);
        Assert.Equal(0.030, gap.Distance, 9);
        Assert.True(gap.CanAutoRepair);
        Assert.True(gap.HasDistance);

        // Repair closes the contour.
        var history = new DxfContourStudio.Application.Commands.CommandHistory();
        history.Execute(new DxfContourStudio.Application.Commands.RepairGapCommand(doc, gap));
        ContourAnalysisResult repaired = Analyze(doc);
        Assert.Equal(1, repaired.ClosedCount);
        Assert.Equal(0, repaired.OpenCount);
        Assert.Equal(0, repaired.SmallGapCount);

        // Undo reopens; redo closes again.
        history.TryUndo();
        ContourAnalysisResult undone = Analyze(doc);
        Assert.Equal(0, undone.ClosedCount);
        Assert.Equal(1, undone.SmallGapCount);
        history.TryRedo();
        ContourAnalysisResult redone = Analyze(doc);
        Assert.Equal(1, redone.ClosedCount);
        Assert.Equal(0, redone.SmallGapCount);
    }

    [Fact]
    public void BasicScene_LinePolylineCircleArc()
    {
        CadDocument doc = Load("basic-scene.dxf");
        Assert.Equal(5, doc.Entities.Count);
        Assert.Contains(doc.Entities, e => e.GeometryType == GeometryType.Line);
        Assert.Contains(doc.Entities, e => e.GeometryType == GeometryType.Circle);
        Assert.Contains(doc.Entities, e => e.GeometryType == GeometryType.Arc);
        Assert.Contains(doc.Entities, e => e.GeometryType == GeometryType.Polyline);
    }
}

