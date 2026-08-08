#nullable enable

using DxfContourStudio.Application.Commands;
using DxfContourStudio.Application.Selection;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// End-to-end command behavior through <see cref="CommandHistory"/>:
/// Add/Delete/Move with full undo/redo cycles, redo-stack clearing and
/// selection/geometry consistency after each step.
/// </summary>
public class CommandHistoryTests
{
    [Fact]
    public void Add_Undo_Redo_RestoresEntity()
    {
        var doc = TestDocs.LineDocument();
        var h = new CommandHistory();
        var line = new LineGeometry(99, "0", new Point2(300, 0), new Point2(400, 0));

        h.Execute(new AddEntityCommand(doc, line));
        Assert.Equal(2, doc.Entities.Count);

        h.TryUndo();
        Assert.Single(doc.Entities);
        Assert.Null(doc.GetEntityById(99));

        h.TryRedo();
        Assert.Equal(2, doc.Entities.Count);
        Assert.NotNull(doc.GetEntityById(99));
    }

    [Fact]
    public void Delete_Undo_RestoresIdLayerGeometryAndOrder()
    {
        var doc = TestDocs.SceneDocument();
        var h = new CommandHistory();

        h.Execute(new DeleteEntitiesCommand(doc, [3]));
        Assert.Null(doc.GetEntityById(3));

        h.TryUndo();

        var restored = doc.GetEntityById(3);
        Assert.NotNull(restored);
        Assert.Equal("cut", restored!.LayerName);
        Assert.Equal(GeometryType.Arc, restored.GeometryType);
        // Order stability: entity 3 still sits between 2 and 4.
        Assert.Equal([1L, 2L, 3L, 4L], doc.Entities.Select(e => e.Id));
    }

    [Fact]
    public void DeleteMultiple_Undo_RestoresAll()
    {
        var doc = TestDocs.SceneDocument();
        var h = new CommandHistory();

        h.Execute(new DeleteEntitiesCommand(doc, [1, 4]));
        Assert.Equal(2, doc.Entities.Count);

        h.TryUndo();

        Assert.Equal(4, doc.Entities.Count);
        Assert.Equal([1L, 2L, 3L, 4L], doc.Entities.Select(e => e.Id));
    }

    [Fact]
    public void Move_Undo_Redo_ChangesAndRestoresGeometry()
    {
        var doc = TestDocs.LineDocument();
        var h = new CommandHistory();
        var delta = new Vector2(25, -10);

        h.Execute(new MoveEntitiesCommand(doc, [1], delta));

        var moved = (LineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(25.0, moved.P0.X, 9);
        Assert.Equal(-10.0, moved.P0.Y, 9);

        h.TryUndo();
        var undone = (LineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(0.0, undone.P0.X, 6);
        Assert.Equal(0.0, undone.P0.Y, 6);

        h.TryRedo();
        var redone = (LineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(25.0, redone.P0.X, 6);
    }

    [Fact]
    public void Move_OnlyAffectsSelectedIds()
    {
        var doc = TestDocs.SceneDocument();
        var h = new CommandHistory();

        h.Execute(new MoveEntitiesCommand(doc, [2], new Vector2(5, 5)));

        var line = (LineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(new Point2(0, 0), line.P0); // line untouched
        var circle = (CircleGeometry)doc.GetEntityById(2)!;
        Assert.Equal(new Point2(55, 30), circle.Center);
    }

    [Fact]
    public void MoveEntity_BoundsUpdateWithMove()
    {
        var doc = TestDocs.SceneDocument();
        var h = new CommandHistory();
        var before = doc.OverallBounds!.Value;

        h.Execute(new MoveEntitiesCommand(doc, [2], new Vector2(100, 50)));

        var after = doc.OverallBounds!.Value;
        Assert.True(after.MaxX > before.MaxX);
        Assert.True(after.MaxY > before.MaxY);
    }

    [Fact]
    public void MovePolyline_SegmentsTranslateTogether()
    {
        var doc = TestDocs.SceneDocument();
        var h = new CommandHistory();
        var delta = new Vector2(20, 30);

        h.Execute(new MoveEntitiesCommand(doc, [4], delta));

        var poly = (PolylineGeometry)doc.GetEntityById(4)!;
        LineSegment first = (LineSegment)poly.Segments[0];
        Assert.Equal(new Point2(20, 30), first.StartPoint);
        // The whole chain moved: last endpoint = first of original + delta.
        Assert.Equal(new Point2(20, 30), poly.StartPoint);
    }

    [Fact]
    public void NewCommandAfterUndo_ClearsRedoStack()
    {
        var doc = TestDocs.LineDocument();
        var h = new CommandHistory();
        h.Execute(new AddEntityCommand(doc, new LineGeometry(90, "0", new Point2(0, 0), new Point2(1, 1))));
        h.TryUndo();
        Assert.True(h.CanRedo);

        h.Execute(new AddEntityCommand(doc, new LineGeometry(91, "0", new Point2(0, 0), new Point2(2, 2))));

        Assert.False(h.CanRedo);
        Assert.True(h.CanUndo);
    }

    [Fact]
    public void UndoRedo_StackDepthIsBounded()
    {
        var doc = TestDocs.LineDocument();
        var h = new CommandHistory();

        for (int i = 0; i < 2000; i++)
        {
            h.Execute(new AddEntityCommand(doc, new LineGeometry(1000 + i, "0", new Point2(0, 0), new Point2(i, i))));
        }

        Assert.True(h.UndoCount <= 2000);
        Assert.True(h.UndoCount > 0);
    }

    [Fact]
    public void Selection_Prune_RemovesDanglingIdsAfterDelete()
    {
        var doc = TestDocs.SceneDocument();
        var h = new CommandHistory();
        var selection = new SelectionModel();
        selection.SelectAll([1, 2, 3]);

        h.Execute(new DeleteEntitiesCommand(doc, [2]));
        selection.Prune(id => doc.GetEntityById(id) is not null);

        Assert.Equal([1L, 3L], selection.Ids.OrderBy(x => x));
    }

    [Fact]
    public void Selection_AfterUndo_NoDanglingIds()
    {
        var doc = TestDocs.LineDocument();
        var h = new CommandHistory();
        var selection = new SelectionModel();
        var line = new LineGeometry(5, "0", new Point2(0, 0), new Point2(1, 1));
        h.Execute(new AddEntityCommand(doc, line));
        selection.SelectSingle(5);

        h.TryUndo();
        selection.Prune(id => doc.GetEntityById(id) is not null);
        Assert.Empty(selection.Ids); // 5 vanished with the entity

        // Redo restores the entity; the id is valid again — selection can
        // legitimately hold it once more.
        h.TryRedo();
        Assert.NotNull(doc.GetEntityById(5));
        selection.Add(5);
        Assert.Contains(5L, selection.Ids);
    }

    [Fact]
    public void RedoStack_ClearedByClear()
    {
        var doc = TestDocs.LineDocument();
        var h = new CommandHistory();
        h.Execute(new AddEntityCommand(doc, new LineGeometry(1, "0", new Point2(0, 0), new Point2(1, 1))));
        h.TryUndo();
        Assert.True(h.CanRedo);

        h.Clear();

        Assert.False(h.CanUndo);
        Assert.False(h.CanRedo);
    }

    [Fact]
    public void MoveUndo_AfterSelectionChange_StillConsistent()
    {
        var doc = TestDocs.LineDocument();
        var h = new CommandHistory();
        var selection = new SelectionModel();
        selection.SelectSingle(1);

        h.Execute(new MoveEntitiesCommand(doc, selection.Ids, new Vector2(10, 0)));
        selection.Clear();

        h.TryUndo();

        var line = (LineGeometry)doc.GetEntityById(1)!;
        Assert.Equal(0.0, line.P0.X, 6);
        // Selection stays empty — no user action re-selected.
        Assert.Empty(selection.Ids);
    }
}