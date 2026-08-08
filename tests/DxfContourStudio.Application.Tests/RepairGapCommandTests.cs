#nullable enable

using DxfContourStudio.Application.Commands;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Localization;
using DxfContourStudio.Core.Contours;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Gap repair behavior (ADR-007): both gap ends move to their midpoint,
/// executed and undone through <see cref="RepairGapCommand"/>, also covering
/// the same-entity (polyline open ends) case.
/// </summary>
public class RepairGapCommandTests
{
    private static LineGeometry Line(long id, double x0, double y0, double x1, double y1) =>
        new(id, "0", new Point2(x0, y0), new Point2(x1, y1));

    private static GapDiagnostic FindGap(CadDocument doc)
    {
        ContourAnalysisResult result = ContourAnalyzer.Analyze(doc.Entities);
        return result.Diagnostics.Single(d => d.Kind == GapKind.SmallGap);
    }

    [Fact]
    public void Repair_TwoEntities_MovesBothEndsToMidpoint()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [Line(1, 0, 0, 100, 0), Line(2, 100.02, 0, 200, 0)],
            [new LayerState("0", IsOn: true, IsFrozen: false, AciColorIndex: 7, IsColorByLayer: true)],
            null, null, null);

        var gap = FindGap(doc);
        Assert.True(gap.CanAutoRepair);
        Assert.Equal(0.02, gap.Distance, 9);

        var command = new RepairGapCommand(doc, gap);
        command.Execute();

        var a = (LineGeometry)doc.GetEntityById(1)!;
        var b = (LineGeometry)doc.GetEntityById(2)!;
        Assert.Equal(100.01, a.EndPoint.X, 9);
        Assert.Equal(0.0, a.EndPoint.Y, 9);
        Assert.Equal(100.01, b.StartPoint.X, 9);
        Assert.Equal(0.0, b.StartPoint.Y, 9);

        // after repair the gap is gone; the chain is still open at its far ends
        ContourAnalysisResult repaired = ContourAnalyzer.Analyze(doc.Entities);
        Assert.Equal(0, repaired.SmallGapCount);
        Assert.Equal(2, repaired.OpenEndCount);
    }

    [Fact]
    public void Repair_Undo_Redo_RestoresOriginalGeometry()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [Line(1, 0, 0, 100, 0), Line(2, 100.02, 0, 200, 0)],
            [new LayerState("0", IsOn: true, IsFrozen: false, AciColorIndex: 7, IsColorByLayer: true)],
            null, null, null);

        var history = new CommandHistory();
        history.Execute(new RepairGapCommand(doc, FindGap(doc)));

        Assert.Equal(100.01, ((LineGeometry)doc.GetEntityById(1)!).EndPoint.X, 9);

        history.TryUndo();
        Assert.Equal(100.0, ((LineGeometry)doc.GetEntityById(1)!).EndPoint.X, 9);
        Assert.Equal(100.02, ((LineGeometry)doc.GetEntityById(2)!).StartPoint.X, 9);

        history.TryRedo();
        Assert.Equal(100.01, ((LineGeometry)doc.GetEntityById(1)!).EndPoint.X, 9);
        Assert.Equal(100.01, ((LineGeometry)doc.GetEntityById(2)!).StartPoint.X, 9);
    }

    [Fact]
    public void Repair_SameEntityOpenPolyline_ClosesBothEnds()
    {
        // open polyline whose last segment ends 0.02 mm from its first start
        var polyline = new PolylineGeometry(
            1, "0",
            [
                new LineSegment(new Point2(0, 0), new Point2(100, 0)),
                new LineSegment(new Point2(100, 0), new Point2(100, 100)),
                new LineSegment(new Point2(100, 100), new Point2(0.02, 0)),
            ],
            isClosed: false);

        var doc = new CadDocument();
        doc.ReplaceContent(
            [polyline],
            [new LayerState("0", IsOn: true, IsFrozen: false, AciColorIndex: 7, IsColorByLayer: true)],
            null, null, null);

        var gap = FindGap(doc);
        Assert.Equal(polyline.Id, gap.EntityIdA);
        Assert.Equal(polyline.Id, gap.EntityIdB);

        var command = new RepairGapCommand(doc, gap);
        command.Execute();

        var repaired = (PolylineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(0.01, repaired.Segments[0].StartPoint.X, 9);
        Assert.Equal(0.0, repaired.Segments[0].StartPoint.Y, 9);
        Assert.Equal(0.01, repaired.Segments[^1].EndPoint.X, 9);
        Assert.Equal(0.0, repaired.Segments[^1].EndPoint.Y, 9);

        command.Undo();
        var restored = (PolylineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(0.0, restored.Segments[0].StartPoint.X, 9);
        Assert.Equal(0.02, restored.Segments[^1].EndPoint.X, 9);
    }

    [Fact]
    public void Repair_NonAutoRepairableGap_Throws()
    {
        // 1 mm apart: far beyond the 0.05 mm repair tolerance
        var doc = new CadDocument();
        doc.ReplaceContent(
            [Line(1, 0, 0, 100, 0), Line(2, 101, 0, 200, 0)],
            [new LayerState("0", IsOn: true, IsFrozen: false, AciColorIndex: 7, IsColorByLayer: true)],
            null, null, null);

        ContourAnalysisResult result = ContourAnalyzer.Analyze(doc.Entities);
        Assert.Equal(0, result.SmallGapCount);
        GapDiagnostic openEnd = result.Diagnostics.First(d => d.Kind == GapKind.OpenContourEnd);
        Assert.False(openEnd.CanAutoRepair);

        Assert.Throws<ArgumentException>(() => new RepairGapCommand(doc, openEnd));
    }
}
