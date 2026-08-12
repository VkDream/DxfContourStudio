#nullable enable

using DxfContourStudio.Application.Commands;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Chain-join command: N endpoint-adjacent entities collapse into one mixed
/// polyline in a single undoable transaction (D12 UI binding support).
/// </summary>
public sealed class JoinManyCommandTests
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

    [Fact]
    public void ThreeSegments_ChainJoinsToSinglePolyline()
    {
        var doc = Document(
            new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 0)),
            new ArcGeometry(2, "0", new Point2(10, 10), 10, Math.PI, Math.PI / 2),
            new LineGeometry(3, "0", new Point2(0, 10), new Point2(0, 15)));
        var history = new CommandHistory();

        history.Execute(new JoinManyCommand(doc, [1, 2, 3], GeometryTolerance.Default));

        var poly = Assert.IsType<PolylineGeometry>(Assert.Single(doc.Entities));
        Assert.Equal(1L, poly.Id);
        Assert.Equal(3, poly.Segments.Count);
        Assert.Equal(GeometryType.Arc, poly.Segments[1].GeometryType);
    }

    [Fact]
    public void ChainJoin_StampOneUndoRestoresAllOriginals()
    {
        var original = new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 0));
        var arc = new ArcGeometry(2, "0", new Point2(10, 10), 10, Math.PI, Math.PI / 2);
        var tail = new LineGeometry(3, "0", new Point2(0, 10), new Point2(0, 15));
        var doc = Document(original, arc, tail);
        var history = new CommandHistory();
        history.Execute(new JoinManyCommand(doc, [1, 2, 3], GeometryTolerance.Default));
        Assert.Single(doc.Entities);

        history.TryUndo();

        Assert.Equal(3, doc.Entities.Count);
        Assert.Equal(original.Id, doc.Entities[0].Id);
        Assert.Equal(arc.Id, doc.Entities[1].Id);
        Assert.Equal(tail.Id, doc.Entities[2].Id);
        Assert.IsType<ArcGeometry>(doc.Entities[1]);
        Assert.Equal(tail.P0, ((LineGeometry)doc.Entities[2]).P0);

        // Redo collapses again.
        history.TryRedo();
        var poly = Assert.IsType<PolylineGeometry>(Assert.Single(doc.Entities));
        Assert.Equal(1L, poly.Id);
        Assert.Equal(3, poly.Segments.Count);
    }

    [Fact]
    public void NonAdjacentChain_RejectedAtConstruction()
    {
        // id 1 and id 2 share no endpoint.
        var doc = Document(
            new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 0)),
            new LineGeometry(2, "0", new Point2(100, 100), new Point2(110, 100)));

        Assert.Throws<ArgumentException>(() => new JoinManyCommand(doc, [1, 2], GeometryTolerance.Default));
    }

    [Fact]
    public void SingleId_Rejected()
    {
        var doc = Document(new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 0)));
        Assert.Throws<ArgumentException>(() => new JoinManyCommand(doc, [1], GeometryTolerance.Default));
    }
}