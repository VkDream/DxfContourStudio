#nullable enable

using DxfContourStudio.Application.Commands;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Node editing semantics (ADR-014): vertices/endpoints move to absolute
/// positions; arc endpoints keep the circle and re-splice their angle; the
/// entity keeps its id; undo restores the pristine geometry; out-of-range
/// nodes and degenerate arcs are refused.
/// </summary>
public class NodeEditEngineTests
{
    private static CadDocument Doc(params IGeometryEntity[] entities)
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            entities,
            [new LayerState("0", IsOn: true, IsFrozen: false, AciColorIndex: 7, IsColorByLayer: true)],
            null, null, null);
        return doc;
    }

    [Fact]
    public void MoveLineNode_RelocatesEndpoint()
    {
        var doc = Doc(new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 0)));
        var history = new CommandHistory();
        history.Execute(new MoveNodeCommand(doc, 1, 1, new Point2(15, 0)));

        var line = (LineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(new Point2(15, 0), line.EndPoint);
        Assert.Equal(new Point2(0, 0), line.StartPoint);
    }

    [Fact]
    public void MoveArcNode_KeepsCircle_ReSplicesAngle()
    {
        // Quarter arc 0°→90° center (0,0) r=10. Move the END node to (0,-10)
        // (270° on the same circle, clockwise wrap) — sweep stays CCW 270°?
        // No: wrap gives 270°; new arc = 0°→270°.
        var doc = Doc(new ArcGeometry(1, "0", new Point2(0, 0), 10, 0, Math.PI / 2));
        var history = new CommandHistory();
        history.Execute(new MoveNodeCommand(doc, 1, 1, new Point2(0, -10)));

        var arc = (ArcGeometry)doc.GetEntityById(1)!;
        Assert.Equal(new Point2(0, 0), arc.Center);
        Assert.Equal(10.0, arc.Radius);
        Assert.Equal(Math.PI * 1.5, arc.SweepRadians, 9);
        Assert.Equal(0.0, arc.StartAngleRadians, 9);
        Assert.Equal(0, arc.EndPoint.DistanceTo(new Point2(0, -10)), 9);
    }

    [Fact]
    public void MovePolylineInteriorVertex_ReAnchorsBothRuns()
    {
        var poly = new PolylineGeometry(1, "0",
        [
            new LineSegment(new Point2(0, 0), new Point2(10, 0)),
            new LineSegment(new Point2(10, 0), new Point2(20, 0)),
        ], isClosed: false);
        var doc = Doc(poly);
        var history = new CommandHistory();
        history.Execute(new MoveNodeCommand(doc, 1, 1, new Point2(10, 5)));

        var result = (PolylineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(2, result.Segments.Count);
        Assert.Equal(new Point2(10, 5), result.Segments[0].EndPoint);
        Assert.Equal(new Point2(10, 5), result.Segments[1].StartPoint);
        Assert.Equal(0.0, result.StartPoint.DistanceTo(Point2.Origin), 12);
        Assert.Equal(new Point2(20, 0), result.EndPoint);
    }

    [Fact]
    public void MovePolylineEndNode_MovesLastRunEnd()
    {
        var poly = new PolylineGeometry(1, "0",
        [
            new LineSegment(new Point2(0, 0), new Point2(10, 0)),
            new LineSegment(new Point2(10, 0), new Point2(20, 10)),
        ], isClosed: false);
        var doc = Doc(poly);
        var history = new CommandHistory();
        history.Execute(new MoveNodeCommand(doc, 1, 2, new Point2(20, 25)));

        var result = (PolylineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(2, result.Segments.Count);
        Assert.Equal(new Point2(20, 25), result.EndPoint);
        Assert.Equal(new Point2(10, 0), result.Segments[1].StartPoint);
    }

    [Fact]
    public void MoveClosedPolylineNode_KeepsClosed()
    {
        // Closed triangle-ish poly with 2 runs: (0,0)->(10,0)->(0,10) closed.
        var poly = new PolylineGeometry(1, "0",
        [
            new LineSegment(new Point2(0, 0), new Point2(10, 0)),
            new LineSegment(new Point2(10, 0), new Point2(0, 10)),
            new LineSegment(new Point2(0, 10), new Point2(0, 0)),
        ], isClosed: true);
        var doc = Doc(poly);
        var history = new CommandHistory();
        // Node 1 = vertex (10,0) shared by run 0 end and run 1 start.
        history.Execute(new MoveNodeCommand(doc, 1, 1, new Point2(12, 0)));

        var result = (PolylineGeometry)doc.GetEntityById(1)!;
        Assert.True(result.IsClosed);
        Assert.Equal(3, result.Segments.Count);
        Assert.Equal(new Point2(12, 0), result.Segments[0].EndPoint);
        Assert.Equal(new Point2(12, 0), result.Segments[1].StartPoint);
    }

    [Fact]
    public void MoveNode_UndoRedo_RestoresOriginal()
    {
        var doc = Doc(new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 0)));
        var history = new CommandHistory();
        history.Execute(new MoveNodeCommand(doc, 1, 1, new Point2(15, 0)));
        Assert.Equal(15.0, ((LineGeometry)doc.GetEntityById(1)!).EndPoint.X, 12);

        history.TryUndo();
        Assert.Equal(10.0, ((LineGeometry)doc.GetEntityById(1)!).EndPoint.X, 12);

        history.TryRedo();
        Assert.Equal(15.0, ((LineGeometry)doc.GetEntityById(1)!).EndPoint.X, 12);
    }

    [Fact]
    public void MoveNode_RejectsOutOfRange()
    {
        var doc = Doc(new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoveNodeCommand(doc, 1, 5, new Point2(0, 0)));
    }

    [Fact]
    public void NodeCount_LineArcPolyline()
    {
        Assert.Equal(2, NodeEditEngine.NodeCount(new LineGeometry(1, "0", Point2.Origin, new Point2(1, 1))));
        Assert.Equal(2, NodeEditEngine.NodeCount(new ArcGeometry(2, "0", Point2.Origin, 5, 0, Math.PI / 2)));
        var poly = new PolylineGeometry(3, "0", [new LineSegment(Point2.Origin, new Point2(5, 0))], isClosed: false);
        Assert.Equal(2, NodeEditEngine.NodeCount(poly));
        var closed = new PolylineGeometry(4, "0", [new LineSegment(Point2.Origin, new Point2(5, 0))], isClosed: true);
        Assert.Equal(1, NodeEditEngine.NodeCount(closed));
    }
}