#nullable enable

using DxfContourStudio.Application.Commands;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Join semantics (ADR-011, golden cases D9/D10):
/// - a unique endpoint-adjacent pair merges into a single mixed polyline;
/// - the primary entity's id + layer survive, the secondary is removed;
/// - exactly one matching endpoint pair; ambiguity/not-connected/cross-layer
///   are refused; undo/redo restores both originals exactly.
/// </summary>
public class JoinEntitiesCommandTests
{
    private static LineGeometry Line(long id, double x0, double y0, double x1, double y1) =>
        new(id, "0", new Point2(x0, y0), new Point2(x1, y1));

    private static ArcGeometry Arc(long id, Point2 center, double radius, double start, double sweep, bool ccw = true) =>
        new(id, "0", center, radius, start, sweep, ccw);

    private static CadDocument Doc(params IGeometryEntity[] entities)
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            entities,
            [new LayerState("0", IsOn: true, IsFrozen: false, AciColorIndex: 7, IsColorByLayer: true)],
            null, null, null);
        return doc;
    }

    private static JoinAttempt TryJoin(CadDocument doc, long a, long b) =>
        JoinEngine.TryJoin(doc.GetEntityById(a)!, doc.GetEntityById(b)!, a, GeometryTolerance.Default);

    // D9: chain Line + Arc + Line merged into one mixed polyline.
    [Fact]
    public void Golden_D9_LineArcLine_JoinsAlongChain()
    {
        // L1: (0,0)->(10,0); A: CCW quarter arc center (10,10) r=10 from (0,10) to (10,0)
        // (start at (0,10), sweep +90° ends at (10,0)); L2: (0,10)->(0,15).
        var doc = Doc(
            Line(1, 0, 0, 10, 0),
            Arc(2, new Point2(10, 10), 10, Math.PI, Math.PI / 2),
            Line(3, 0, 10, 0, 15));

        var history = new CommandHistory();
        history.Execute(new JoinEntitiesCommand(doc, 1, 2, GeometryTolerance.Default));
        history.Execute(new JoinEntitiesCommand(doc, 1, 3, GeometryTolerance.Default));

        Assert.Single(doc.Entities);
        var joined = (PolylineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(3, joined.Segments.Count);
        Assert.Equal("0", joined.LayerName);
        Assert.Equal(0.0, joined.StartPoint.X, 12);
        Assert.Equal(0.0, joined.StartPoint.Y, 12);
        Assert.Equal(0.0, joined.EndPoint.X, 12);
        Assert.Equal(15.0, joined.EndPoint.Y, 12);
        Assert.Equal(GeometryType.Line, joined.Segments[0].GeometryType);
        Assert.Equal(GeometryType.Arc, joined.Segments[1].GeometryType);
        Assert.Equal(GeometryType.Line, joined.Segments[2].GeometryType);
    }

    // D10: undo/redo restores both original entities with exact geometry & ids.
    [Fact]
    public void Golden_D10_UndoRedo_RestoresExactGeometry()
    {
        var doc = Doc(
            Line(1, 0, 0, 10, 0),
            Arc(2, new Point2(10, 10), 10, Math.PI, Math.PI / 2));
        var history = new CommandHistory();
        history.Execute(new JoinEntitiesCommand(doc, 1, 2, GeometryTolerance.Default));

        Assert.Single(doc.Entities);
        Assert.NotNull(doc.GetEntityById(1));
        Assert.Null(doc.GetEntityById(2));

        history.TryUndo();
        Assert.Equal(2, doc.Entities.Count);
        var line = (LineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(0.0, line.StartPoint.X, 12);
        Assert.Equal(10.0, line.EndPoint.X, 12);
        var arc = (ArcGeometry)doc.GetEntityById(2)!;
        Assert.Equal(Math.PI / 2, arc.SweepRadians, 12);

        history.TryRedo();
        Assert.Single(doc.Entities);
        Assert.NotNull(doc.GetEntityById(1));
        Assert.Null(doc.GetEntityById(2));
        var joined = (PolylineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(2, joined.Segments.Count);
    }

    // Arc + Arc whose secondary must be reversed (end-to-end join).
    [Fact]
    public void Join_ArcPlusReversedArc_ReverseRunsCorrectly()
    {
        // a: CCW quarter center (0,0) r=10 from (10,0) to (0,10) (0°→90°).
        var a = Arc(1, new Point2(0, 0), 10, 0, Math.PI / 2);
        // b: CCW quarter center (0,20) r=10 from (-10,20) to (0,10) (180°→270°):
        // its END touches a's end, so the joining run list must be reversed.
        var b = Arc(2, new Point2(0, 20), 10, Math.PI, Math.PI / 2);
        var doc = Doc(a, b);

        var attempt = TryJoin(doc, 1, 2);
        Assert.True(attempt.IsValid);
        Assert.Equal(JoinRejectReason.None, attempt.Reason);
        Assert.Equal(2, attempt.Joined!.Segments.Count);
        var revB = attempt.Joined.Segments[1];
        Assert.Equal(GeometryType.Arc, revB.GeometryType);
        Assert.False(((ArcSegment)revB).IsCounterClockwise);
        Assert.Equal(-Math.PI / 2, ((ArcSegment)revB).SweepRadians, 12);
        Assert.Equal(0.0, attempt.Joined.Segments[0].EndPoint.DistanceTo(revB.StartPoint), 12);
        Assert.Equal(10.0, attempt.Joined.Segments[0].StartPoint.X, 12);
        Assert.Equal(-10.0, attempt.Joined.EndPoint.X, 12);

        var history = new CommandHistory();
        history.Execute(new JoinEntitiesCommand(doc, 1, 2, GeometryTolerance.Default));
        Assert.Single(doc.Entities);
    }

    [Fact]
    public void Join_Rejects_NotConnected_Gap()
    {
        var doc = Doc(Line(1, 0, 0, 10, 0), Line(2, 10.1, 0, 20, 0));
        var attempt = TryJoin(doc, 1, 2);
        Assert.False(attempt.IsValid);
        Assert.Equal(JoinRejectReason.NotConnected, attempt.Reason);
    }

    [Fact]
    public void Join_Rejects_Ambiguous_IdenticalLines()
    {
        var doc = Doc(Line(1, 0, 0, 10, 0), Line(2, 0, 0, 10, 0));
        var attempt = TryJoin(doc, 1, 2);
        Assert.False(attempt.IsValid);
        Assert.Equal(JoinRejectReason.Ambiguous, attempt.Reason);
    }

    [Fact]
    public void Join_Rejects_DifferentLayers()
    {
        var doc = Doc(Line(1, 0, 0, 10, 0), new LineGeometry(2, "1", new Point2(10, 0), new Point2(20, 0)));
        var attempt = TryJoin(doc, 1, 2);
        Assert.False(attempt.IsValid);
        Assert.Equal(JoinRejectReason.DifferentLayers, attempt.Reason);
    }

    [Fact]
    public void Join_PolylinePlusLine_MergesRunsTogether()
    {
        var poly = new PolylineGeometry(1, "0", [new LineSegment(new Point2(0, 0), new Point2(5, 0))], isClosed: false);
        var doc = Doc(poly, Line(2, 5, 0, 10, 0));

        var history = new CommandHistory();
        history.Execute(new JoinEntitiesCommand(doc, 1, 2, GeometryTolerance.Default));
        var joined = (PolylineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(2, joined.Segments.Count);
        Assert.Equal(10.0, joined.EndPoint.X, 12);
        Assert.Equal(new Point2(0, 0), joined.StartPoint);
    }

    [Fact]
    public void Join_NestedTwice_DocumentStaysConsistent()
    {
        var doc = Doc(Line(1, 0, 0, 10, 0), Line(2, 10, 0, 20, 0));
        var history = new CommandHistory();
        history.Execute(new JoinEntitiesCommand(doc, 1, 2, GeometryTolerance.Default));
        Assert.Single(doc.Entities);
        history.TryUndo();
        Assert.Equal(2, doc.Entities.Count);
        Assert.Equal(1L, doc.Entities[0].Id);
        Assert.Equal(2L, doc.Entities[1].Id);
    }
}