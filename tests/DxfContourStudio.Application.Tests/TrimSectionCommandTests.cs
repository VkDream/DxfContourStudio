#nullable enable

using DxfContourStudio.Application.Commands;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;
using Xunit;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Command-level tests for <see cref="TrimSectionCommand"/>: the three
/// removal shapes (interior / touches start / touches end) must leave exactly
/// the right entity set with the right ids, and undo/redo must restore the
/// original document exactly. Regression net for the D17 wiring round which
/// found that "removal touches the start" kept the original entity around.
/// </summary>
public class TrimSectionCommandTests
{
    private static CadDocument DocumentWithBoundaries()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [
                new LineGeometry(1, "0", new Point2(0, 0), new Point2(100, 0)),
                new LineGeometry(2, "0", new Point2(30, -10), new Point2(30, 10)),
                new LineGeometry(3, "0", new Point2(60, -10), new Point2(60, 10)),
            ],
            [new LayerState("0", true, false, 7, true)], null, null, null);
        return doc;
    }

    [Fact]
    public void InteriorRemoval_LeavesTwoPieces_LeftKeepsId_RightGetsFreshId()
    {
        var doc = DocumentWithBoundaries();
        var command = new TrimSectionCommand(doc, 1, 0.30, 0.60);

        command.Execute();

        Assert.Equal(4, doc.Entities.Count);
        var left = doc.GetEntityById(1);
        var right = doc.GetEntityById(4);
        Assert.NotNull(left);
        Assert.NotNull(right);
        Assert.Equal(new Point2(30, 0), left!.EndPoint);
        Assert.Equal(new Point2(60, 0), right!.StartPoint);
    }

    [Fact]
    public void TouchesStart_KeptRightPiece_ReplacesOriginalEntity()
    {
        var doc = DocumentWithBoundaries();
        var command = new TrimSectionCommand(doc, 1, 0.0, 0.30);

        command.Execute();

        // No fresh entity: the kept piece [0.3, 1] replaces entity 1 in place.
        Assert.Equal(3, doc.Entities.Count);
        var kept = Assert.IsType<LineGeometry>(doc.GetEntityById(1));
        Assert.Equal(new Point2(30, 0), kept.StartPoint);
        Assert.Equal(new Point2(100, 0), kept.EndPoint);
    }

    [Fact]
    public void TouchesEnd_KeptLeftPiece_ReplacesOriginalEntity()
    {
        var doc = DocumentWithBoundaries();
        var command = new TrimSectionCommand(doc, 1, 0.60, 1.0);

        command.Execute();

        Assert.Equal(3, doc.Entities.Count);
        var kept = Assert.IsType<LineGeometry>(doc.GetEntityById(1));
        Assert.Equal(new Point2(0, 0), kept.StartPoint);
        Assert.Equal(new Point2(60, 0), kept.EndPoint);
    }

    [Fact]
    public void WholeRemoval_IsRefusedAtConstruction()
    {
        var doc = DocumentWithBoundaries();
        Assert.Throws<ArgumentException>(() => new TrimSectionCommand(doc, 1, 0.0, 1.0));
    }

    [Fact]
    public void InteriorRemoval_UndoRedo_RestoresExactly()
    {
        var doc = DocumentWithBoundaries();
        var command = new TrimSectionCommand(doc, 1, 0.30, 0.60);
        var history = new CommandHistory();
        history.Execute(command);

        history.TryUndo();

        Assert.Equal(3, doc.Entities.Count);
        var original = Assert.IsType<LineGeometry>(doc.GetEntityById(1));
        Assert.Equal(new Point2(0, 0), original.StartPoint);
        Assert.Equal(new Point2(100, 0), original.EndPoint);
        Assert.Null(doc.GetEntityById(4));

        history.TryRedo();

        Assert.Equal(4, doc.Entities.Count);
        Assert.NotNull(doc.GetEntityById(1));
        Assert.NotNull(doc.GetEntityById(4));
    }

    [Fact]
    public void TouchesStart_UndoRedo_RestoresExactly()
    {
        var doc = DocumentWithBoundaries();
        var command = new TrimSectionCommand(doc, 1, 0.0, 0.30);
        var history = new CommandHistory();
        history.Execute(command);

        history.TryUndo();

        var original = Assert.IsType<LineGeometry>(doc.GetEntityById(1));
        Assert.Equal(new Point2(0, 0), original.StartPoint);
        Assert.Equal(new Point2(100, 0), original.EndPoint);
        Assert.Equal(3, doc.Entities.Count);

        history.TryRedo();

        var kept = Assert.IsType<LineGeometry>(doc.GetEntityById(1));
        Assert.Equal(new Point2(30, 0), kept.StartPoint);
        Assert.Equal(3, doc.Entities.Count);
    }
}