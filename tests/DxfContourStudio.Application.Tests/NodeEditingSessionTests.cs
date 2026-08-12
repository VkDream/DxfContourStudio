#nullable enable

using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Interaction;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Wpf.ViewModels;
using LayerState = DxfContourStudio.Application.Documents.LayerState;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// D14 acceptance — the node-edit session driven through the same public
/// surface the viewport calls (BeginNodeEdit → NodeEditDrag → Commit / Cancel).
/// One gesture produces exactly one undoable command; previews never touch the
/// document; Escape cancels; snap participates with the grabbed grip excluded;
/// invalid results are refused at commit.
/// </summary>
[Collection("LocalizationShared")]
public class NodeEditingSessionTests
{
    private static MainViewModel ViewModelWith(params IGeometryEntity[] entities)
    {
        var vm = new MainViewModel();
        vm.Document.ReplaceContent(
            entities,
            [new LayerState("0", IsOn: true, IsFrozen: false, AciColorIndex: 7, IsColorByLayer: true)],
            null, null, null);
        vm.Selection.SelectSingle(entities[0].Id);
        return vm;
    }

    private static GripDescriptor GripOf(IGeometryEntity entity, GripKind kind) =>
        GripBuilder.Build(entity).First(g => g.Kind == kind);

    [Fact]
    public void NODE1_LineStartDrag_UpdatesGeometry_UndoRedo()
    {
        var line = new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 0));
        var vm = ViewModelWith(line);
        vm.BeginNodeEdit(GripOf(line, GripKind.LineStart));

        vm.NodeEditDrag(new Point2(-5, 0));
        Assert.True(vm.IsNodeEditing);
        // Preview only — the document is untouched until commit.
        Assert.Equal(new Point2(0, 0), ((LineGeometry)vm.Document.GetEntityById(1)!).P0);

        vm.CommitNodeEdit();
        Assert.Equal(new Point2(-5, 0), ((LineGeometry)vm.Document.GetEntityById(1)!).P0);
        Assert.Equal(1, vm.History.UndoCount);

        vm.History.TryUndo();
        Assert.Equal(new Point2(0, 0), ((LineGeometry)vm.Document.GetEntityById(1)!).P0);
        Assert.Equal(new Point2(10, 0), ((LineGeometry)vm.Document.GetEntityById(1)!).P1);

        vm.History.TryRedo();
        Assert.Equal(new Point2(-5, 0), ((LineGeometry)vm.Document.GetEntityById(1)!).P0);
        Assert.Equal(1, vm.History.UndoCount);
    }

    [Fact]
    public void NODE2_CircleRadiusDrag_25To30_Undo25()
    {
        var circle = new CircleGeometry(2, "0", new Point2(0, 0), 25);
        var vm = ViewModelWith(circle);
        vm.BeginNodeEdit(GripOf(circle, GripKind.CircleRadius));

        vm.NodeEditDrag(new Point2(30, 0));
        Assert.Equal(25.0, ((CircleGeometry)vm.Document.GetEntityById(2)!).Radius, 9);

        vm.CommitNodeEdit();
        Assert.Equal(30.0, ((CircleGeometry)vm.Document.GetEntityById(2)!).Radius, 9);
        Assert.Equal(1, vm.History.UndoCount);

        vm.History.TryUndo();
        Assert.Equal(25.0, ((CircleGeometry)vm.Document.GetEntityById(2)!).Radius, 9);

        vm.History.TryRedo();
        Assert.Equal(30.0, ((CircleGeometry)vm.Document.GetEntityById(2)!).Radius, 9);
    }

    [Fact]
    public void NODE2b_CircleCenterDrag_MovesWholeCircle()
    {
        var circle = new CircleGeometry(2, "0", new Point2(10, 10), 5);
        var vm = ViewModelWith(circle);
        vm.BeginNodeEdit(GripOf(circle, GripKind.CircleCenter));

        vm.NodeEditDrag(new Point2(20, 30));
        vm.CommitNodeEdit();

        var result = (CircleGeometry)vm.Document.GetEntityById(2)!;
        Assert.Equal(new Point2(20, 30), result.Center);
        Assert.Equal(5.0, result.Radius, 9);
    }

    [Fact]
    public void NODE3_ArcStartDrag_AcrossZero_SweepStaysCorrect()
    {
        // Arc from 350° sweeping 20° CCW → ends at 10°. The start grip (350°)
        // dragged to 0° must yield a 0°→10° arc (sweep 10°), never a flipped
        // or exploded 340° turn.
        var arc = new ArcGeometry(3, "0", new Point2(0, 0), 10, Math.PI * 35.0 / 18.0, Math.PI / 9.0);
        var vm = ViewModelWith(arc);

        vm.BeginNodeEdit(GripOf(arc, GripKind.ArcStart));
        vm.NodeEditDrag(new Point2(10, 0));
        vm.CommitNodeEdit();

        var result = (ArcGeometry)vm.Document.GetEntityById(3)!;
        Assert.True(result.IsCounterClockwise);
        Assert.Equal(0.0, result.StartAngleRadians, 9);
        Assert.Equal(Math.PI / 18.0, result.SweepRadians, 9);
        Assert.Equal(10.0, result.Radius, 9);
        // Length consistent with the sweep; endpoint stays on the circle.
        Assert.Equal(Math.PI / 18.0, result.Length / result.Radius, 9);
        Assert.Equal(10.0, result.EndPoint.DistanceTo(result.Center), 9);
    }

    [Fact]
    public void NODE4_ClosedPolylineVertexDrag_StaysClosed()
    {
        var poly = new PolylineGeometry(4, "0",
        [
            new LineSegment(new Point2(0, 0), new Point2(10, 0)),
            new LineSegment(new Point2(10, 0), new Point2(0, 10)),
            new LineSegment(new Point2(0, 10), new Point2(0, 0)),
        ], isClosed: true);
        var vm = ViewModelWith(poly);

        vm.BeginNodeEdit(GripOf(poly, GripKind.PolylineVertex));
        vm.NodeEditDrag(new Point2(-3, -3));
        vm.CommitNodeEdit();

        var result = (PolylineGeometry)vm.Document.GetEntityById(poly.Id)!;
        Assert.True(result.IsClosed);
        Assert.Equal(3, result.Segments.Count);
        // No crack: the run carrying the moved vertex re-anchors both sides,
        // and the closing chain stays continuous.
        Assert.Equal(new Point2(-3, -3), result.Segments[0].StartPoint);
        Assert.Equal(new Point2(-3, -3), result.Segments[2].EndPoint);
        Assert.Equal(result.EndPoint, result.StartPoint);
    }

    [Fact]
    public void NODE5_InvalidRadius_CommitRejected()
    {
        var circle = new CircleGeometry(2, "0", new Point2(0, 0), 25);
        var vm = ViewModelWith(circle);
        vm.BeginNodeEdit(GripOf(circle, GripKind.CircleRadius));

        // Radius grip dragged onto the center → radius 0 → refused at commit.
        vm.NodeEditDrag(new Point2(0, 0));
        vm.CommitNodeEdit();

        Assert.Equal(25.0, ((CircleGeometry)vm.Document.GetEntityById(2)!).Radius, 9);
        Assert.Equal(0, vm.History.UndoCount);
        Assert.Contains("无效", vm.StatusText);
    }

    [Fact]
    public void CancelGesture_RestoresOriginal_NoUndoEntry()
    {
        var line = new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 0));
        var vm = ViewModelWith(line);
        vm.BeginNodeEdit(GripOf(line, GripKind.LineEnd));
        vm.NodeEditDrag(new Point2(20, 0));
        Assert.True(vm.IsNodeEditing);

        vm.CancelNodeEdit();
        Assert.False(vm.IsNodeEditing);
        var kept = (LineGeometry)vm.Document.GetEntityById(1)!;
        Assert.Equal(new Point2(0, 0), kept.P0);
        Assert.Equal(new Point2(10, 0), kept.P1);
        Assert.Equal(0, vm.History.UndoCount);
        Assert.Null(vm.NodeEditPreview);
    }

    [Fact]
    public void SnapDrag_RejectsOwnGrip_AllowsOtherEntityPoints()
    {
        var line1 = new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 0));
        var line2 = new LineGeometry(2, "0", new Point2(30, 0), new Point2(30, 10));
        var vm = ViewModelWith(line1, line2);
        vm.BeginNodeEdit(GripOf(line1, GripKind.LineEnd));

        // Dragging near the grabbed end (10,0): the snap engine finds the
        // grabbed grip itself, the self-exclusion filter drops it, and the
        // raw cursor point wins — no glueing back to the origin.
        vm.NodeEditDrag(new Point2(9.7, 0));
        vm.CommitNodeEdit();
        var edited = (LineGeometry)vm.Document.GetEntityById(1)!;
        Assert.Equal(9.7, edited.P1.X, 6);
        Assert.Equal(0.0, edited.P1.Y, 9);

        // Dragging line 1's end onto line 2's nearby start snaps there.
        vm.BeginNodeEdit(GripOf(edited, GripKind.LineEnd));
        vm.NodeEditDrag(new Point2(30.05, 0)); // within 8px of (30,0) at 1 ppm
        vm.CommitNodeEdit();
        var snapped = (LineGeometry)vm.Document.GetEntityById(1)!;
        Assert.Equal(30.0, snapped.P1.X, 6);
        Assert.Equal(0.0, snapped.P1.Y, 9);
    }
}