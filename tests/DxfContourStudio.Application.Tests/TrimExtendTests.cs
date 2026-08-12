#nullable enable

using DxfContourStudio.Application.Commands;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Trim/extend semantics (ADR-013): one end of a path entity moves to its
/// crossing with a boundary entity (kept end stays put); extension happens
/// along the path direction; no usable crossing is refused; undo restores
/// the original.
/// </summary>
public class TrimExtendCommandTests
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
    public void Trim_LineToBoundary_KeepsStart()
    {
        // Horizontal line (0,0)->(20,0) trimmed by vertical line x=15.
        var doc = Doc(Line(1, 0, 0, 20, 0), Line(2, 15, -5, 15, 5));
        var history = new CommandHistory();
        history.Execute(new TrimExtendCommand(doc, 1, 2, TrimSide.KeepStart, 0.05));

        var line = (LineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(0.0, line.StartPoint.X, 12);
        Assert.Equal(15.0, line.EndPoint.X, 12);
    }

    [Fact]
    public void Trim_Line_KeepsEnd_RepeatedTrim()
    {
        var doc = Doc(Line(1, 0, 0, 20, 0), Line(2, 5, -5, 5, 5));
        var history = new CommandHistory();
        history.Execute(new TrimExtendCommand(doc, 1, 2, TrimSide.KeepEnd, 0.05));

        var line = (LineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(5.0, line.StartPoint.X, 12);
        Assert.Equal(20.0, line.EndPoint.X, 12);
    }

    [Fact]
    public void Extend_Line_ToBoundary()
    {
        var doc = Doc(Line(1, 0, 0, 10, 0), Line(2, 15, -5, 15, 5));
        var history = new CommandHistory();
        history.Execute(new TrimExtendCommand(doc, 1, 2, TrimSide.KeepStart, 0.05));

        var line = (LineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(15.0, line.EndPoint.X, 12);
        Assert.Equal(0.0, line.EndPoint.Y, 12);
    }

    [Fact]
    public void Extend_Arc_PastItsSweep()
    {
        // CCW arc (0,10)->(-10,0) (90°→180°, sweep 90°); boundary is a
        // horizontal line at y=-10 whose crossing with the arc's circle lies
        // at (0,-10) — 90° beyond the arc end, so the arc extends.
        var doc = Doc(
            Arc(1, new Point2(0, 0), 10, Math.PI / 2, Math.PI / 2),
            Line(2, -20, -10, 20, -10));
        var history = new CommandHistory();
        history.Execute(new TrimExtendCommand(doc, 1, 2, TrimSide.KeepStart, 0.05));

        var arc = (ArcGeometry)doc.GetEntityById(1)!;
        Assert.Equal(Math.PI, arc.SweepRadians, 9);
        Assert.Equal(Math.PI / 2, arc.StartAngleRadians, 9);
        Assert.Equal(0, arc.EndPoint.DistanceTo(new Point2(0, -10)), 9);
    }

    [Fact]
    public void Trim_Arc_ToMidSweepBoundary()
    {
        // Arc 0°→90°, boundary vertical line x=5 cuts the arc circle at
        // ~ (+5, +8.66) inside the sweep → trimmed.
        var doc = Doc(
            Arc(1, new Point2(0, 0), 10, 0, Math.PI / 2),
            Line(2, 5, -10, 5, 10));
        var history = new CommandHistory();
        history.Execute(new TrimExtendCommand(doc, 1, 2, TrimSide.KeepStart, 0.05));

        var arc = (ArcGeometry)doc.GetEntityById(1)!;
        Assert.Equal(Math.Acos(0.5), arc.SweepRadians, 9); // 60°
        Assert.Equal(0.0, arc.StartAngleRadians, 9);
    }

    [Fact]
    public void TouchingBoundary_IsUnchanged()
    {
        var doc = Doc(Line(1, 0, 0, 15, 0), Line(2, 15, -5, 15, 5));
        var history = new CommandHistory();
        history.Execute(new TrimExtendCommand(doc, 1, 2, TrimSide.KeepStart, 0.05));

        var line = (LineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(15.0, line.EndPoint.X, 12);
    }

    [Fact]
    public void Trim_UndoRedo_RestoresOriginal()
    {
        var doc = Doc(Line(1, 0, 0, 20, 0), Line(2, 15, -5, 15, 5));
        var history = new CommandHistory();
        history.Execute(new TrimExtendCommand(doc, 1, 2, TrimSide.KeepStart, 0.05));
        Assert.Equal(15.0, ((LineGeometry)doc.GetEntityById(1)!).EndPoint.X, 12);

        history.TryUndo();
        Assert.Equal(20.0, ((LineGeometry)doc.GetEntityById(1)!).EndPoint.X, 12);

        history.TryRedo();
        Assert.Equal(15.0, ((LineGeometry)doc.GetEntityById(1)!).EndPoint.X, 12);
    }

    [Fact]
    public void Trim_RejectsParallelBoundary()
    {
        var doc = Doc(Line(1, 0, 0, 20, 0), Line(2, 0, 5, 20, 5));
        Assert.Throws<ArgumentException>(() =>
            new TrimExtendCommand(doc, 1, 2, TrimSide.KeepStart, 0.05));
    }

    [Fact]
    public void Extend_Polyline_LastRun()
    {
        // Poly: (0,0)->(10,0)->(10,10). Extend the vertical last run down…
        // boundary y=15 → new end (10,15).
        var poly = new PolylineGeometry(1, "0",
        [
            new LineSegment(new Point2(0, 0), new Point2(10, 0)),
            new LineSegment(new Point2(10, 0), new Point2(10, 10)),
        ], isClosed: false);
        var doc = Doc(poly, Line(2, 10, 15, 20, 15));
        var history = new CommandHistory();
        history.Execute(new TrimExtendCommand(doc, 1, 2, TrimSide.KeepStart, 0.05));

        var result = (PolylineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(2, result.Segments.Count);
        Assert.Equal(10.0, result.EndPoint.X, 12);
        Assert.Equal(15.0, result.EndPoint.Y, 12);
    }
}