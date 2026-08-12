#nullable enable

using System.IO;
using DxfContourStudio.Application.Commands;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Projects;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Edited-geometry round-trip (D10): entities produced by the editing
/// commands (Join → mixed polylines, Break → split polylines, Trim/Extend →
/// re-spliced arcs, NodeEdit → moved vertices) must survive
/// save → load byte-exact in the .dxfstudio project format, including
/// polyline arc runs (center/radius/start/sweep, CW sign) and closed flags.
/// </summary>
public class EditedGeometryRoundTripTests
{
    private static CadDocument Document(params IGeometryEntity[] entities)
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            entities,
            [new LayerState("0", IsOn: true, IsFrozen: false, AciColorIndex: 7, IsColorByLayer: true)],
            null, null, null);
        return doc;
    }

    private static (CadDocument Loaded, string Dir) RoundTrip(CadDocument doc)
    {
        string dir = Path.Combine(Path.GetTempPath(), $"dcs-edit-rt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "edited.dxfstudio");
        ProjectSerializer.Save(ProjectSerializer.ToProject(doc, GeometryTolerance.Default), path);
        (CadDocument loaded, _) = ProjectSerializer.ToDocument(ProjectSerializer.Load(path));
        return (loaded, dir);
    }

    [Fact]
    public void Joined_MixedPolyline_SurvivesRoundTrip()
    {
        // D9-style chain: line + arc + line joined into one mixed polyline.
        var doc = Document(
            new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 0)),
            new ArcGeometry(2, "0", new Point2(10, 10), 10, Math.PI, Math.PI / 2),
            new LineGeometry(3, "0", new Point2(0, 10), new Point2(0, 15)));
        var history = new CommandHistory();
        history.Execute(new JoinEntitiesCommand(doc, 1, 2, GeometryTolerance.Default));
        history.Execute(new JoinEntitiesCommand(doc, 1, 3, GeometryTolerance.Default));

        var (loaded, dir) = RoundTrip(doc);
        try
        {
            var poly = Assert.IsType<PolylineGeometry>(loaded.Entities[0]);
            Assert.Equal(1L, poly.Id);
            Assert.Equal(3, poly.Segments.Count);
            Assert.Equal(GeometryType.Line, poly.Segments[0].GeometryType);
            Assert.Equal(GeometryType.Arc, poly.Segments[1].GeometryType);
            Assert.Equal(GeometryType.Line, poly.Segments[2].GeometryType);
            var arcRun = (ArcSegment)poly.Segments[1];
            Assert.Equal(10.0, arcRun.Center.X, 9);
            Assert.Equal(10.0, arcRun.Center.Y, 9);
            Assert.Equal(10.0, arcRun.Radius, 9);
            // Join reverses the arc so the chained walk starts at the arc end
            // (3π/2 from the center (10,10), i.e. the point (10,0)) and sweeps
            // back towards (0,10); the sweep sign carries the orientation.
            Assert.Equal(3 * Math.PI / 2, arcRun.StartAngleRadians, 9);
            Assert.Equal(-Math.PI / 2, arcRun.SweepRadians, 9); // reversed during join
            Assert.False(arcRun.IsCounterClockwise);
            Assert.False(poly.IsClosed);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Broken_Polyline_KeepsRunKindsAfterRoundTrip()
    {
        var poly = new PolylineGeometry(1, "0",
        [
            new LineSegment(new Point2(0, 0), new Point2(10, 0)),
            new ArcSegment(new Point2(10, 0), 10, Math.PI, Math.PI / 2, true),
        ], isClosed: false);
        var doc = Document(poly);
        var history = new CommandHistory();
        var cut = new Point2(10 + 10 * Math.Cos(202.5 * Math.PI / 180), 10 * Math.Sin(202.5 * Math.PI / 180));
        history.Execute(new BreakEntityCommand(doc, 1, cut, 0.05));

        var (loaded, dir) = RoundTrip(doc);
        try
        {
            Assert.Equal(2, loaded.Entities.Count);
            var left = Assert.IsType<PolylineGeometry>(loaded.Entities[0]);
            Assert.Equal(2, left.Segments.Count);
            Assert.Equal(GeometryType.Arc, left.Segments[1].GeometryType);
            var right = Assert.IsType<PolylineGeometry>(loaded.Entities[1]);
            Assert.Single(right.Segments);
            Assert.Equal(GeometryType.Arc, right.Segments[0].GeometryType);
            var arcLeft = (ArcSegment)left.Segments[1];
            Assert.Equal(10.0, arcLeft.Radius, 9);
            Assert.True(arcLeft.IsCounterClockwise);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TrimmedArc_SweepSign_SurvivesRoundTrip()
    {
        // CW arc (sweep negative) trimmed by a boundary: the in-memory result
        // must round-trip exactly, including sweep sign and start angle.
        var doc = Document(
            new ArcGeometry(1, "0", new Point2(0, 0), 10, 0, -Math.PI / 2),
            new LineGeometry(2, "0", new Point2(8, -10), new Point2(8, 10)));
        var history = new CommandHistory();
        history.Execute(new TrimExtendCommand(doc, 1, 2, TrimSide.KeepStart, 0.05));

        var trimmed = Assert.IsType<ArcGeometry>(doc.Entities[0]);

        var (loaded, dir) = RoundTrip(doc);
        try
        {
            var arc = Assert.IsType<ArcGeometry>(loaded.Entities[0]);
            Assert.Equal(trimmed.StartAngleRadians, arc.StartAngleRadians, 9);
            Assert.Equal(trimmed.SweepRadians, arc.SweepRadians, 9);
            Assert.Equal(trimmed.IsCounterClockwise, arc.IsCounterClockwise);
            Assert.Equal(trimmed.Radius, arc.Radius, 9);
            Assert.Equal(trimmed.IsCounterClockwise, arc.IsCounterClockwise);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void NodeMovedClosedPolyline_SurvivesRoundTrip()
    {
        var poly = new PolylineGeometry(1, "0",
        [
            new LineSegment(new Point2(0, 0), new Point2(10, 0)),
            new LineSegment(new Point2(10, 0), new Point2(0, 10)),
            new LineSegment(new Point2(0, 10), new Point2(0, 0)),
        ], isClosed: true);
        var doc = Document(poly);
        var history = new CommandHistory();
        history.Execute(new MoveNodeCommand(doc, 1, 1, new Point2(12, 0)));

        var (loaded, dir) = RoundTrip(doc);
        try
        {
            var result = Assert.IsType<PolylineGeometry>(loaded.Entities[0]);
            Assert.True(result.IsClosed);
            Assert.Equal(new Point2(12, 0), result.Segments[0].EndPoint);
            Assert.Equal(new Point2(12, 0), result.Segments[1].StartPoint);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}