#nullable enable

using System.Collections.Generic;
using System.Linq;
using DxfContourStudio.Application.Commands;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Interaction;
using DxfContourStudio.Core.Geometry;
using Xunit;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Interaction-level tests for the D17 editing-tool state machine
/// (<see cref="EditToolSession"/>): the four tools plan and request the
/// right commands, refuse the right situations with the right status keys,
/// never mutate the document on hover, and always clean up their pending
/// state. Command execution/undo itself is covered by the command tests;
/// here the focus is the gesture logic.
/// </summary>
public class EditToolSessionTests
{
    private const double Tol = 5.0; // world pick tolerance, as the viewport would compute it

    private sealed class Harness
    {
        public CadDocument Document { get; }
        public EditToolSession Session { get; }
        public List<ICommand> Commands { get; } = [];
        public List<string> StatusKeys { get; } = [];
        public List<long> Selected { get; } = [];
        public List<ToolOverlayState> Overlays { get; } = [];

        public Harness(CadDocument? document = null)
        {
            Document = document ?? new CadDocument();
            Session = new EditToolSession(Document, GeometryTolerance.Default);
            Session.CommandRequested += Commands.Add;
            Session.StatusKeyRequested += StatusKeys.Add;
            Session.ResultEntitySelected += Selected.Add;
            Session.OverlayChanged += () => Overlays.Add(Session.Overlay);
        }
    }

    private static CadDocument JoinDocument()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [
                new LineGeometry(1, "0", new Point2(0, 0), new Point2(100, 0)),
                new LineGeometry(2, "0", new Point2(100, 0), new Point2(200, 0)),
            ],
            [new LayerState("0", true, false, 7, true)], null, null, null);
        return doc;
    }

    private static CadDocument TrimDocument()
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

    private static CadDocument ExtendDocument()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [
                new LineGeometry(1, "0", new Point2(0, 0), new Point2(50, 0)),
                new LineGeometry(2, "0", new Point2(80, -10), new Point2(80, 10)),
            ],
            [new LayerState("0", true, false, 7, true)], null, null, null);
        return doc;
    }

    private static CadDocument LineDocument()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [new LineGeometry(1, "0", new Point2(0, 0), new Point2(100, 0))],
            [new LayerState("0", true, false, 7, true)], null, null, null);
        return doc;
    }

    private static SnapResult NoneSnap => SnapResult.None;

    // ---------------------------------------------------------------- Join

    [Fact]
    public void Join_TwoClicksOnEndpoints_RequestsJoinCommand_AndSelectsResult()
    {
        var h = new Harness(JoinDocument());
        h.Session.ActivateTool(ToolMode.Join);

        h.Session.OnPointerLeftDown(new Point2(100, 0), NoneSnap, Tol);
        Assert.Contains("EditTools.Join.PickSecond", h.StatusKeys);

        h.Session.OnPointerLeftDown(new Point2(200, 0), NoneSnap, Tol);

        Assert.Single(h.Commands);

        // Execution through the (shared) history path leaves one merged entity
        // carrying the primary id.
        h.Commands[0].Execute();
        Assert.Single(h.Document.Entities);
        Assert.IsType<PolylineGeometry>(h.Document.Entities[0]);
        Assert.Equal(1, h.Document.Entities[0].Id);

        h.Session.NotifyCommandCompleted(true);
        // v0.3.0: continuous mode — the tool stays active after a commit.
        Assert.Equal(ToolMode.Join, h.Session.ActiveTool);
        Assert.Equal(1, Assert.Single(h.Selected));
    }

    [Fact]
    public void Join_SameEndpointTwice_IsRefused()
    {
        var h = new Harness(JoinDocument());
        h.Session.ActivateTool(ToolMode.Join);

        h.Session.OnPointerLeftDown(new Point2(0, 0), NoneSnap, Tol);
        h.Session.OnPointerLeftDown(new Point2(0, 0), NoneSnap, Tol);

        Assert.Contains("EditTools.Join.SameEndpoint", h.StatusKeys);
        Assert.Empty(h.Commands);
        // The first pick is still pending — the user may pick another end.
        Assert.True(h.Session.HasPendingTarget);
    }

    [Fact]
    public void Join_CircleAndClosedPolyline_HaveNoOpenEndpoints()
    {
        var h = new Harness(SceneWithClosedShapes());
        h.Session.ActivateTool(ToolMode.Join);

        // Click exactly on the circle's "start" point and the closed polyline
        // vertex — neither exposes an open endpoint.
        h.Session.OnPointerLeftDown(new Point2(60, 25), NoneSnap, Tol);
        h.Session.OnPointerLeftDown(new Point2(0, 0), NoneSnap, Tol);

        Assert.Contains("EditTools.Join.NoEndpoint", h.StatusKeys);
        Assert.Empty(h.Commands);
    }

    private static CadDocument SceneWithClosedShapes()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [
                new CircleGeometry(1, "0", new Point2(50, 25), 10),
                new PolylineGeometry(2, "0",
                    [
                        new LineSegment(new Point2(0, 0), new Point2(0, 50)),
                        new LineSegment(new Point2(0, 50), new Point2(50, 50)),
                        new LineSegment(new Point2(50, 50), new Point2(0, 0)),
                    ], isClosed: true),
            ],
            [new LayerState("0", true, false, 7, true)], null, null, null);
        return doc;
    }

    [Fact]
    public void Join_EndpointsTooFarApart_ReportsNotConnected()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [
                new LineGeometry(1, "0", new Point2(0, 0), new Point2(100, 0)),
                new LineGeometry(2, "0", new Point2(300, 0), new Point2(400, 0)),
            ],
            [new LayerState("0", true, false, 7, true)], null, null, null);
        var h = new Harness(doc);
        h.Session.ActivateTool(ToolMode.Join);

        h.Session.OnPointerLeftDown(new Point2(0, 0), NoneSnap, Tol);
        // A valid open endpoint of the second entity, but 300 mm away from the
        // first end — the engine refuses on the JoinTolerance.
        h.Session.OnPointerLeftDown(new Point2(400, 0), NoneSnap, Tol);

        Assert.Contains("EditTools.Join.NotConnected", h.StatusKeys);
        Assert.Empty(h.Commands);
        // The first pick survives so the user can try another second end.
        Assert.True(h.Session.HasPendingTarget);
    }

    [Fact]
    public void Join_DifferentLayers_IsRefused()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [
                new LineGeometry(1, "0", new Point2(0, 0), new Point2(100, 0)),
                new LineGeometry(2, "cut", new Point2(100, 0), new Point2(200, 0)),
            ],
            [
                new LayerState("0", true, false, 7, true),
                new LayerState("cut", true, false, 1, true),
            ], null, null, null);
        var h = new Harness(doc);
        h.Session.ActivateTool(ToolMode.Join);

        h.Session.OnPointerLeftDown(new Point2(100, 0), NoneSnap, Tol); // line1 end
        h.Session.OnPointerLeftDown(new Point2(200, 0), NoneSnap, Tol);

        Assert.Contains("EditTools.Join.DifferentLayers", h.StatusKeys);
        Assert.Empty(h.Commands);
    }

    [Fact]
    public void Join_HoverPreview_ShowsConnection_AndClearsWhenAway()
    {
        var h = new Harness(JoinDocument());
        h.Session.ActivateTool(ToolMode.Join);

        h.Session.OnPointerLeftDown(new Point2(0, 0), NoneSnap, Tol);
        h.Session.OnPointerMoved(new Point2(200, 0), NoneSnap, Tol);

        ToolOverlayState overlay = h.Session.Overlay;
        Assert.Equal(ToolPreviewKind.Normal, overlay.Kind);
        Assert.Single(overlay.HighlightRuns);
        Assert.Equal(2, overlay.Markers.Count);

        h.Session.OnPointerMoved(new Point2(0, 250), NoneSnap, Tol);
        Assert.Equal(ToolOverlayState.Empty, h.Session.Overlay);
    }

    // ---------------------------------------------------------------- Break

    [Fact]
    public void Break_ClickOnLine_RequestsBreakCommand()
    {
        var h = new Harness(LineDocument());
        h.Session.ActivateTool(ToolMode.Break);

        h.Session.OnPointerLeftDown(new Point2(50, 0), NoneSnap, Tol);

        var command = Assert.IsType<BreakEntityCommand>(Assert.Single(h.Commands));
        command.Execute();
        Assert.Equal(2, h.Document.Entities.Count);
        Assert.Equal(1, h.Document.Entities[0].Id);
    }

    [Fact]
    public void Break_ClickNearEndpoint_IsRefused()
    {
        var h = new Harness(LineDocument());
        h.Session.ActivateTool(ToolMode.Break);

        h.Session.OnPointerLeftDown(new Point2(100, 0), NoneSnap, Tol);

        Assert.Contains("EditTools.Break.EndpointGuard", h.StatusKeys);
        Assert.Empty(h.Commands);
        // No mutation: the initial ReplaceContent does not bump the revision.
        Assert.Equal(0, h.Document.GeometryRevision);
    }

    [Fact]
    public void Break_ClickOffEntity_ReportsNoTarget()
    {
        var h = new Harness(LineDocument());
        h.Session.ActivateTool(ToolMode.Break);

        h.Session.OnPointerLeftDown(new Point2(50, 50), NoneSnap, Tol);

        Assert.Contains("EditTools.Break.NoTarget", h.StatusKeys);
        Assert.Empty(h.Commands);
    }

    [Fact]
    public void Break_IntersectionSnap_ReplacesCutPointWithSnapPoint()
    {
        var h = new Harness(LineDocument());
        h.Session.ActivateTool(ToolMode.Break);

        // The cursor is off the line, but the snap result is an intersection
        // point on it — the cut must happen there.
        var snap = SnapResult.At(SnapKind.Intersection, new Point2(40, 0), [1, 2], 0.1);
        h.Session.OnPointerLeftDown(new Point2(39, 1), snap, Tol);

        var command = Assert.IsType<BreakEntityCommand>(Assert.Single(h.Commands));
        command.Execute();
        Assert.Equal(2, h.Document.Entities.Count);
    }

    [Fact]
    public void Break_HoverShowsProjectedMarker_ButNeverMutates()
    {
        var h = new Harness(LineDocument());
        h.Session.ActivateTool(ToolMode.Break);
        int revision = h.Document.GeometryRevision;

        h.Session.OnPointerMoved(new Point2(50, 2), NoneSnap, Tol);

        Assert.Equal(ToolPreviewKind.Normal, h.Session.Overlay.Kind);
        Assert.Single(h.Session.Overlay.Markers);
        Assert.Equal(revision, h.Document.GeometryRevision);
        Assert.Empty(h.Commands);
    }

    // ---------------------------------------------------------------- Trim

    [Fact]
    public void Trim_FirstClickPicksTarget_SecondClickRequestsSectionCommand()
    {
        var h = new Harness(TrimDocument());
        h.Session.ActivateTool(ToolMode.Trim);

        h.Session.OnPointerLeftDown(new Point2(15, 0), NoneSnap, Tol);
        Assert.Contains("EditTools.Trim.PickSection", h.StatusKeys);

        // Hover preview: the section between x=30 and x=60 under the cursor.
        h.Session.OnPointerMoved(new Point2(45, 0), NoneSnap, Tol);
        Assert.Equal(ToolPreviewKind.Remove, h.Session.Overlay.Kind);
        Assert.Equal(2, h.Session.Overlay.Markers.Count);

        h.Session.OnPointerLeftDown(new Point2(45, 0), NoneSnap, Tol);
        var command = Assert.IsType<TrimSectionCommand>(Assert.Single(h.Commands));
        command.Execute();
        Assert.Equal(4, h.Document.Entities.Count); // left + right + 2 boundary lines

        h.Session.NotifyCommandCompleted(true);
        // v0.3.0: continuous mode — the tool stays active after a commit.
        Assert.Equal(ToolMode.Trim, h.Session.ActiveTool);
    }

    [Fact]
    public void Trim_SecondClickAwayFromTarget_RecomputesFromClick()
    {
        var h = new Harness(TrimDocument());
        h.Session.ActivateTool(ToolMode.Trim);

        h.Session.OnPointerLeftDown(new Point2(15, 0), NoneSnap, Tol);
        // Second click on the first section (before x=30).
        h.Session.OnPointerLeftDown(new Point2(10, 0), NoneSnap, Tol);

        Assert.Single(h.Commands);
        h.Commands[0].Execute();
        // Section [0, 0.3] removed → the kept piece [0.3, 1] replaces the
        // entity; the two boundary lines remain.
        Assert.Equal(3, h.Document.Entities.Count);
        Assert.Equal(1, h.Document.Entities[0].Id);
    }

    [Fact]
    public void Trim_PolylineTarget_IsRefused()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [
                new PolylineGeometry(1, "0",
                    [new LineSegment(new Point2(0, 0), new Point2(100, 0))], isClosed: false),
                new LineGeometry(2, "0", new Point2(50, -10), new Point2(50, 10)),
            ],
            [new LayerState("0", true, false, 7, true)], null, null, null);
        var h = new Harness(doc);
        h.Session.ActivateTool(ToolMode.Trim);

        h.Session.OnPointerLeftDown(new Point2(40, 0), NoneSnap, Tol);

        Assert.Contains("EditTools.Trim.UnsupportedTarget", h.StatusKeys);
        Assert.Empty(h.Commands);
        Assert.False(h.Session.HasPendingTarget);
    }

    [Fact]
    public void Trim_NoBoundaries_ReportsNoBoundary()
    {
        var h = new Harness(LineDocument());
        h.Session.ActivateTool(ToolMode.Trim);

        h.Session.OnPointerLeftDown(new Point2(15, 0), NoneSnap, Tol);
        Assert.Contains("EditTools.Trim.PickSection", h.StatusKeys);

        // The boundary query excludes the target itself — nothing else
        // crosses it, so the second click refuses.
        h.Session.OnPointerLeftDown(new Point2(15, 0), NoneSnap, Tol);
        Assert.Contains("EditTools.Trim.NoBoundary", h.StatusKeys);
        Assert.Empty(h.Commands);
    }

    [Fact]
    public void Trim_ClickExactlyOnBoundary_RemovesSectionOnItsLeft()
    {
        var h = new Harness(TrimDocument());
        h.Session.ActivateTool(ToolMode.Trim);

        h.Session.OnPointerLeftDown(new Point2(15, 0), NoneSnap, Tol);
        // A click exactly on the x=30 cut belongs to the section left of it.
        h.Session.OnPointerLeftDown(new Point2(30, 0), NoneSnap, Tol);

        var command = Assert.IsType<TrimSectionCommand>(Assert.Single(h.Commands));
        command.Execute();
        // Section [0, 0.3] removed, kept piece [0.3, 1] replaces entity 1.
        Assert.Equal(new Point2(30, 0), h.Document.Entities[0].StartPoint);
        Assert.Equal(new Point2(100, 0), h.Document.Entities[0].EndPoint);
    }

    [Fact]
    public void Trim_BoundaryInsideEndpointBand_RemovalSpansWholePath_Refused()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [
                new LineGeometry(1, "0", new Point2(0, 0), new Point2(100, 0)),
                // Boundary crossing within the pick-tolerance band of the path
                // start clamps to t=0 → the only section covers the whole
                // path, which is not a trim.
                new LineGeometry(2, "0", new Point2(-1, -10), new Point2(-1, 10)),
            ],
            [new LayerState("0", true, false, 7, true)], null, null, null);
        var h = new Harness(doc);
        h.Session.ActivateTool(ToolMode.Trim);

        h.Session.OnPointerLeftDown(new Point2(15, 0), NoneSnap, Tol);
        h.Session.OnPointerLeftDown(new Point2(50, 0), NoneSnap, Tol);

        Assert.Contains("EditTools.Trim.InvalidSection", h.StatusKeys);
        Assert.Empty(h.Commands);
    }

    [Fact]
    public void Trim_Cancel_ReturnsTrueWhilePending_ThenFalse()
    {
        var h = new Harness(TrimDocument());
        h.Session.ActivateTool(ToolMode.Trim);
        h.Session.OnPointerLeftDown(new Point2(15, 0), NoneSnap, Tol);
        Assert.True(h.Session.HasPendingTarget);

        Assert.True(h.Session.Cancel());
        Assert.False(h.Session.HasPendingTarget);
        Assert.False(h.Session.Cancel());
    }

    // ---------------------------------------------------------------- Extend

    [Fact]
    public void Extend_ClickNearEnd_ExtendsToBoundary_RequestsTrimExtendCommand()
    {
        var h = new Harness(ExtendDocument());
        h.Session.ActivateTool(ToolMode.Extend);

        h.Session.OnPointerLeftDown(new Point2(48, 0), NoneSnap, Tol);
        Assert.Contains("EditTools.Extend.PickEnd", h.StatusKeys);

        h.Session.OnPointerMoved(new Point2(48, 0), NoneSnap, Tol);
        Assert.Equal(ToolPreviewKind.Extend, h.Session.Overlay.Kind);
        Assert.Single(h.Session.Overlay.HighlightRuns);

        h.Session.OnPointerLeftDown(new Point2(48, 0), NoneSnap, Tol);
        var command = Assert.IsType<TrimExtendCommand>(Assert.Single(h.Commands));
        command.Execute();
        Assert.Equal(new Point2(80, 0), h.Document.Entities[0].EndPoint);
    }

    [Fact]
    public void Extend_ClickNearStart_ExtendsStartSide()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [
                new LineGeometry(1, "0", new Point2(50, 0), new Point2(100, 0)),
                new LineGeometry(2, "0", new Point2(20, -10), new Point2(20, 10)),
            ],
            [new LayerState("0", true, false, 7, true)], null, null, null);
        var h = new Harness(doc);
        h.Session.ActivateTool(ToolMode.Extend);

        // Click near the StartPoint (50,0) → KeepEnd side → start extends to x=20.
        h.Session.OnPointerLeftDown(new Point2(52, 0), NoneSnap, Tol);
        h.Session.OnPointerLeftDown(new Point2(52, 0), NoneSnap, Tol);

        var command = Assert.IsType<TrimExtendCommand>(Assert.Single(h.Commands));
        command.Execute();
        Assert.Equal(new Point2(20, 0), h.Document.Entities[0].StartPoint);
        Assert.Equal(new Point2(100, 0), h.Document.Entities[0].EndPoint);
    }

    [Fact]
    public void Extend_NoQualifyingBoundary_ReportsNoBoundary()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [
                new LineGeometry(1, "0", new Point2(0, 0), new Point2(50, 0)),
                // Boundary inside the target span: would shorten, not lengthen.
                new LineGeometry(2, "0", new Point2(20, -10), new Point2(20, 10)),
            ],
            [new LayerState("0", true, false, 7, true)], null, null, null);
        var h = new Harness(doc);
        h.Session.ActivateTool(ToolMode.Extend);

        h.Session.OnPointerLeftDown(new Point2(48, 0), NoneSnap, Tol);
        h.Session.OnPointerLeftDown(new Point2(48, 0), NoneSnap, Tol);

        Assert.Contains("EditTools.Extend.NoBoundary", h.StatusKeys);
        Assert.Empty(h.Commands);
    }

    // ------------------------------------------------------- cross-cutting

    [Fact]
    public void Hover_NeverMutatesDocument_ForAnyTool()
    {
        var h = new Harness(TrimDocument());
        h.Document.IsDirty = false; // ReplaceContent marks the document dirty by design
        foreach (ToolMode mode in new[] { ToolMode.Join, ToolMode.Break, ToolMode.Trim, ToolMode.Extend })
        {
            h.Session.ActivateTool(mode);
            int revision = h.Document.GeometryRevision;
            h.Session.OnPointerMoved(new Point2(45, 0), NoneSnap, Tol);
            h.Session.OnPointerMoved(new Point2(10, 0), NoneSnap, Tol);
            h.Session.OnPointerMoved(new Point2(80, 5), NoneSnap, Tol);
            Assert.Equal(revision, h.Document.GeometryRevision);
            Assert.Empty(h.Commands);
            Assert.False(h.Document.IsDirty);
        }
    }

    [Fact]
    public void ToolSwitch_CancelsPendingGesture_AndClearsOverlay()
    {
        var h = new Harness(JoinDocument());
        h.Session.ActivateTool(ToolMode.Join);
        h.Session.OnPointerLeftDown(new Point2(0, 0), NoneSnap, Tol);
        Assert.True(h.Session.HasPendingTarget);

        h.Session.ActivateTool(ToolMode.Break);

        Assert.False(h.Session.HasPendingTarget);
        Assert.Equal(ToolOverlayState.Empty, h.Session.Overlay);
        Assert.Equal(ToolMode.Break, h.Session.ActiveTool);
    }

    [Fact]
    public void HiddenLayer_IsNeverPickableOrSnappable()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [
                new LineGeometry(1, "0", new Point2(0, 0), new Point2(100, 0)),
                new LineGeometry(2, "cut", new Point2(0, 50), new Point2(100, 50)),
            ],
            [
                new LayerState("0", true, false, 7, true),
                new LayerState("cut", true, false, 1, true),
            ], null, null, null);
        doc.SetLayerVisible("cut", false);
        var h = new Harness(doc);

        // Break on the hidden line → no target.
        h.Session.ActivateTool(ToolMode.Break);
        h.Session.OnPointerLeftDown(new Point2(50, 50), NoneSnap, Tol);
        Assert.Contains("EditTools.Break.NoTarget", h.StatusKeys);
        Assert.Empty(h.Commands);

        // Join endpoint of the hidden line is not a candidate.
        h.Session.ActivateTool(ToolMode.Join);
        h.Session.OnPointerLeftDown(new Point2(0, 50), NoneSnap, Tol);
        Assert.Contains("EditTools.Join.NoEndpoint", h.StatusKeys);

        // Trim: the hidden line is not a boundary.
        h.Session.ActivateTool(ToolMode.Trim);
        h.Session.OnPointerLeftDown(new Point2(15, 0), NoneSnap, Tol);
        h.Session.OnPointerLeftDown(new Point2(15, 0), NoneSnap, Tol);
        Assert.Contains("EditTools.Trim.NoBoundary", h.StatusKeys);
        Assert.Empty(h.Commands);
    }

    [Fact]
    public void PointerLeft_ClearsOverlay_KeepsPendingGesture()
    {
        var h = new Harness(JoinDocument());
        h.Session.ActivateTool(ToolMode.Join);
        h.Session.OnPointerLeftDown(new Point2(0, 0), NoneSnap, Tol);
        Assert.True(h.Session.HasPendingTarget);

        h.Session.OnPointerLeft();

        Assert.Equal(ToolOverlayState.Empty, h.Session.Overlay);
        Assert.True(h.Session.HasPendingTarget);
    }

    [Fact]
    public void FailedCommit_KeepsToolActive_AndClearsPending()
    {
        var h = new Harness(TrimDocument());
        h.Session.ActivateTool(ToolMode.Trim);
        h.Session.OnPointerLeftDown(new Point2(15, 0), NoneSnap, Tol);

        h.Session.NotifyCommandCompleted(false);

        Assert.Equal(ToolMode.Trim, h.Session.ActiveTool);
        Assert.False(h.Session.HasPendingTarget);
        Assert.Equal(ToolOverlayState.Empty, h.Session.Overlay);
    }

    [Fact]
    public void SelectMode_ConsumesNothing_AndActivateToolFromSelectWorks()
    {
        var h = new Harness(LineDocument());

        Assert.False(h.Session.OnPointerLeftDown(new Point2(50, 0), NoneSnap, Tol));

        h.Session.ActivateTool(ToolMode.Break);
        Assert.True(h.Session.OnPointerLeftDown(new Point2(50, 0), NoneSnap, Tol));
        Assert.Single(h.Commands);
    }

    [Fact]
    public void DocumentMutation_ClearsOverlayAndPending()
    {
        var h = new Harness(JoinDocument());
        h.Session.ActivateTool(ToolMode.Join);
        h.Session.OnPointerLeftDown(new Point2(0, 0), NoneSnap, Tol);
        Assert.True(h.Session.HasPendingTarget);

        // The view model calls this after any command execution/document swap.
        h.Session.OnDocumentChanged();

        Assert.False(h.Session.HasPendingTarget);
        Assert.Equal(ToolOverlayState.Empty, h.Session.Overlay);
    }
}