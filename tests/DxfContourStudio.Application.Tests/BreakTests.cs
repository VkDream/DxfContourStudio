#nullable enable

using DxfContourStudio.Application.Commands;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Break semantics (ADR-012): a path entity splits at a curve point into two
/// same-kind pieces; the left piece keeps the original id; undo restores the
/// pristine entity at its original position; endpoint / off-curve cuts are
/// refused.
/// </summary>
public class BreakEntityCommandTests
{
    private static LineGeometry Line(long id, double x0, double y0, double x1, double y1) =>
        new(id, "0", new Point2(x0, y0), new Point2(x1, y1));

    private static ArcGeometry Arc(long id, Point2 center, double radius, double start, double sweep) =>
        new(id, "0", center, radius, start, sweep);

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
    public void Break_Line_AtMidpoint_SplitsInTwo()
    {
        var doc = Doc(Line(1, 0, 0, 100, 0));
        var history = new CommandHistory();
        history.Execute(new BreakEntityCommand(doc, 1, new Point2(25, 0), 0.05));

        Assert.Equal(2, doc.Entities.Count);
        var left = (LineGeometry)doc.GetEntityById(1)!;
        var right = (LineGeometry)doc.GetEntityById(2)!;
        Assert.Equal(new Point2(0, 0), left.StartPoint);
        Assert.Equal(new Point2(25, 0), left.EndPoint);
        Assert.Equal(new Point2(25, 0), right.StartPoint);
        Assert.Equal(new Point2(100, 0), right.EndPoint);
    }

    [Fact]
    public void Break_Arc_AtHalfSweep_SplitsTwoArcs()
    {
        // CCW quarter arc 0°→90°, cut at 45°.
        var doc = Doc(Arc(1, new Point2(0, 0), 10, 0, Math.PI / 2));
        var history = new CommandHistory();
        var cut = new Point2(10 * Math.Cos(Math.PI / 4), 10 * Math.Sin(Math.PI / 4));
        history.Execute(new BreakEntityCommand(doc, 1, cut, 0.05));

        Assert.Equal(2, doc.Entities.Count);
        var left = (ArcGeometry)doc.GetEntityById(1)!;
        var right = (ArcGeometry)doc.GetEntityById(2)!;
        Assert.Equal(Math.PI / 4, left.SweepRadians, 9);
        Assert.Equal(Math.PI / 4, right.SweepRadians, 9);
        Assert.Equal(0.0, left.StartAngleRadians, 9);
        Assert.Equal(0, left.EndPoint.DistanceTo(right.StartPoint), 12);
    }

    [Fact]
    public void Break_Polyline_InSecondRun_KeepsRunTypes()
    {
        // Two-run poly: line (0,0)->(10,0) then arc quarter 0°→90° center (10,0).
        var poly = new PolylineGeometry(1, "0",
        [
            new LineSegment(new Point2(0, 0), new Point2(10, 0)),
            new ArcSegment(new Point2(10, 0), 10, Math.PI, Math.PI / 2, true),
        ], isClosed: false);
        var doc = Doc(poly);
        var history = new CommandHistory();
        // Cut at the middle of the arc (180°+22.5°).
        var cut = new Point2(10 + 10 * Math.Cos(202.5 * Math.PI / 180), 10 * Math.Sin(202.5 * Math.PI / 180));
        history.Execute(new BreakEntityCommand(doc, 1, cut, 0.05));

        Assert.Equal(2, doc.Entities.Count);
        var left = (PolylineGeometry)doc.GetEntityById(1)!;
        var right = (PolylineGeometry)doc.GetEntityById(2)!;
        Assert.Equal(2, left.Segments.Count); // line run + first arc half
        Assert.Single(right.Segments); // second arc half only
        Assert.Equal(GeometryType.Arc, left.Segments[1].GeometryType);
        Assert.Equal(GeometryType.Arc, right.Segments[0].GeometryType);
        Assert.Equal(0, left.EndPoint.DistanceTo(right.StartPoint), 12);
        Assert.False(left.IsClosed);
        Assert.False(right.IsClosed);
    }

    [Fact]
    public void Break_UndoRedo_RestoresOriginal()
    {
        var doc = Doc(Line(1, 0, 0, 100, 0));
        var history = new CommandHistory();
        history.Execute(new BreakEntityCommand(doc, 1, new Point2(25, 0), 0.05));

        history.TryUndo();
        Assert.Single(doc.Entities);
        var restored = (LineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(new Point2(0, 0), restored.StartPoint);
        Assert.Equal(new Point2(100, 0), restored.EndPoint);

        history.TryRedo();
        Assert.Equal(2, doc.Entities.Count);
        Assert.NotNull(doc.GetEntityById(1));
        Assert.NotNull(doc.GetEntityById(2));
    }

    [Fact]
    public void Break_Rejects_PointOffCurve()
    {
        var doc = Doc(Line(1, 0, 0, 100, 0));
        Assert.Throws<ArgumentException>(() =>
            new BreakEntityCommand(doc, 1, new Point2(50, 5), 0.05));
    }

    [Fact]
    public void Break_Rejects_EndpointCut()
    {
        var doc = Doc(Line(1, 0, 0, 100, 0));
        // Start endpoint: project succeeds (t=0) but splitting at an endpoint
        // is refused.
        Assert.Throws<ArgumentException>(() =>
            new BreakEntityCommand(doc, 1, new Point2(0, 0), 0.05));
    }
}