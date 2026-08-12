#nullable enable

using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Wpf.ViewModels;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// D13A wiring tests: the Extend command surfaced on the view model must use
/// the existing Trim/Extend engine, go through the command history (Ctrl+Z /
/// Ctrl+Y) and leave the document untouched when no unique boundary exists.
/// </summary>
public class ExtendUiCommandTests
{
    private static LayerState Layer(string name = "0") =>
        new(name, IsOn: true, IsFrozen: false, AciColorIndex: 7, IsColorByLayer: true);

    private static MainViewModel ViewModelWith(params IGeometryEntity[] entities)
    {
        var vm = new MainViewModel();
        vm.Document.ReplaceContent(entities, [Layer()], null, null, null);
        return vm;
    }

    [Fact]
    public void Extend_CommandAvailable_LineExtendsToUniqueBoundary()
    {
        var vm = ViewModelWith(
            new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 0)),
            new LineGeometry(2, "0", new Point2(30, -20), new Point2(30, 20)));
        vm.Selection.SelectSingle(1);

        Assert.True(vm.ExtendSelectedCommand.CanExecute(null));
        vm.ExtendSelectedCommand.Execute(null);

        var line = Assert.IsType<LineGeometry>(vm.Document.Entities[0]);
        Assert.Equal(30.0, line.P1.X, 6);
        Assert.Equal(0.0, line.P1.Y, 6);
    }

    [Fact]
    public void Extend_UndoRestoresOriginal_RedoReapplies()
    {
        var vm = ViewModelWith(
            new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 0)),
            new LineGeometry(2, "0", new Point2(30, -20), new Point2(30, 20)));
        vm.Selection.SelectSingle(1);
        vm.ExtendSelectedCommand.Execute(null);

        vm.History.TryUndo();
        Assert.Equal(10.0, Assert.IsType<LineGeometry>(vm.Document.Entities[0]).P1.X, 6);

        vm.History.TryRedo();
        Assert.Equal(30.0, Assert.IsType<LineGeometry>(vm.Document.Entities[0]).P1.X, 6);
    }

    [Fact]
    public void Extend_ArcExtendsToUniqueBoundary_UndoRedo()
    {
        var vm = ViewModelWith(
            new ArcGeometry(1, "0", new Point2(0, 0), 10, 0, Math.PI / 2),
            new LineGeometry(2, "0", new Point2(-5, 30), new Point2(-5, -30)));
        vm.Selection.SelectSingle(1);

        vm.ExtendSelectedCommand.Execute(null);
        ArcGeometry extended = Assert.IsType<ArcGeometry>(vm.Document.Entities[0]);
        Assert.Equal(-5.0, extended.EndPoint.X, 6);
        Assert.True(extended.SweepRadians > Math.PI / 2);

        vm.History.TryUndo();
        Assert.Equal(Math.PI / 2, Assert.IsType<ArcGeometry>(vm.Document.Entities[0]).SweepRadians, 9);

        vm.History.TryRedo();
        ArcGeometry redone = Assert.IsType<ArcGeometry>(vm.Document.Entities[0]);
        Assert.Equal(-5.0, redone.EndPoint.X, 6);
    }

    [Fact]
    public void Extend_NoBoundary_LeavesDocumentUnchanged()
    {
        var vm = ViewModelWith(new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 0)));
        vm.Selection.SelectSingle(1);

        vm.ExtendSelectedCommand.Execute(null);

        Assert.Equal(10.0, Assert.IsType<LineGeometry>(vm.Document.Entities[0]).P1.X, 9);
        Assert.Equal(0, vm.History.UndoCount);
    }

    [Fact]
    public void Extend_AmbiguousBoundary_LeavesDocumentUnchanged()
    {
        var vm = ViewModelWith(
            new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 0)),
            new LineGeometry(2, "0", new Point2(30, -20), new Point2(30, 20)),
            new LineGeometry(3, "0", new Point2(50, -20), new Point2(50, 20)));
        vm.Selection.SelectSingle(1);

        vm.ExtendSelectedCommand.Execute(null);

        Assert.Equal(10.0, Assert.IsType<LineGeometry>(vm.Document.Entities[0]).P1.X, 9);
        Assert.Equal(0, vm.History.UndoCount);
    }

    [Fact]
    public void Extend_NotEnabledWithoutSelection()
    {
        var vm = ViewModelWith(new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 0)));
        Assert.False(vm.ExtendSelectedCommand.CanExecute(null));
    }
}

/// <summary>D13A snap pipeline tests: the VM computes candidates only when the master switch is on.</summary>
public class SnapPipelineTests
{
    private static MainViewModel ViewModelWithLine()
    {
        var vm = new MainViewModel();
        vm.Document.ReplaceContent(
            [new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 0))],
            [new LayerState("0", IsOn: true, IsFrozen: false, AciColorIndex: 7, IsColorByLayer: true)],
            null, null, null);
        return vm;
    }

    [Fact]
    public void Disabled_MasterSwitch_ProducesNoCandidate()
    {
        var vm = ViewModelWithLine();
        vm.SnapMasterEnabled = false;
        vm.UpdateSnap(new Point2(0, 0));
        Assert.Null(vm.CurrentSnap);
    }

    [Fact]
    public void EndpointEnabled_EndpointCandidateWins()
    {
        var vm = ViewModelWithLine();
        vm.UpdateSnap(new Point2(0, 0));
        Assert.NotNull(vm.CurrentSnap);
        Assert.Equal(SnapKind.Endpoint, vm.CurrentSnap!.Value.Kind);
        Assert.Equal(0.0, vm.CurrentSnap!.Value.WorldPoint.X, 9);
    }

    [Fact]
    public void EndpointDisabled_EndpointIgnored_NoSurrogate()
    {
        var vm = ViewModelWithLine();
        vm.SnapEndpointEnabled = false;
        // Zoom in so the 8px radius covers 2 world units: the line midpoint at
        // (5,0) is out of reach and endpooint snap is disabled → no candidate.
        vm.Viewport.ZoomAt(4.0);
        vm.UpdateSnap(new Point2(0, 0));
        Assert.Null(vm.CurrentSnap);
    }

    [Fact]
    public void NearestOffByDefault_NotReportedFarFromEveryPoint()
    {
        var vm = ViewModelWithLine();
        vm.UpdateSnap(new Point2(500, 500));
        Assert.Null(vm.CurrentSnap);
    }

    [Fact]
    public void Intersection_PrioritizedOverMidpointAtSameSpot()
    {
        var vm = new MainViewModel();
        vm.Document.ReplaceContent(
            [
                new LineGeometry(1, "0", new Point2(0, 0), new Point2(10, 10)),
                new LineGeometry(2, "0", new Point2(0, 10), new Point2(10, 0)),
            ],
            [new LayerState("0", IsOn: true, IsFrozen: false, AciColorIndex: 7, IsColorByLayer: true)],
            null, null, null);

        // Zoom in so the 8px radius covers only 2 world units: the endpoints
        // sit 7.07 away and are out of reach; the intersection at (5,5) must
        // outrank the coincident midpoints.
        vm.Viewport.ZoomAt(4.0);
        vm.UpdateSnap(new Point2(5, 5));
        Assert.NotNull(vm.CurrentSnap);
        Assert.Equal(SnapKind.Intersection, vm.CurrentSnap!.Value.Kind);
        Assert.Equal(5.0, vm.CurrentSnap!.Value.WorldPoint.X, 9);
        Assert.Equal(5.0, vm.CurrentSnap!.Value.WorldPoint.Y, 9);
    }

    [Fact]
    public void PixelTolerance_ScalesWithZoom()
    {
        var vm = ViewModelWithLine();
        // Zoomed out: 8px covers ~80 world units → (11,0) snaps to (0,0).
        vm.Viewport.ZoomAt(0.1);
        vm.UpdateSnap(new Point2(11, 0));
        Assert.NotNull(vm.CurrentSnap);

        // Zoomed in: 8px covers ~0.4 world → (0.55,0) is out of reach.
        vm.Viewport.ZoomAt(200.0);
        vm.ClearSnap();
        vm.UpdateSnap(new Point2(0.55, 0));
        Assert.Null(vm.CurrentSnap);
    }

    [Fact]
    public void DocumentChange_ClearsStaleCandidate()
    {
        var vm = ViewModelWithLine();
        vm.UpdateSnap(new Point2(0, 0));
        Assert.NotNull(vm.CurrentSnap);

        vm.Document.RemoveEntity(1);
        Assert.Null(vm.CurrentSnap);
    }
}