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
/// v0.3.0 UX-overhaul behaviours of <see cref="EditToolSession"/> that are
/// separate from the core gesture state machine (covered by
/// EditToolSessionTests): continuous mode after a successful commit, tool
/// activation hints, status-key anti-storm dedup, the join first-step hover
/// marker, the break invalid-at-endpoint hover state, the trim sticky plan
/// (preview == commit), and the extend first-step end marker. Pure state
/// machine, no window.
/// </summary>
public class ToolUxTests
{
    private const double Tol = 5.0;

    private sealed class Harness
    {
        public CadDocument Document { get; }
        public EditToolSession Session { get; }
        public List<ICommand> Commands { get; } = [];
        public List<string> StatusKeys { get; } = [];
        public List<long> Selected { get; } = [];

        public Harness(CadDocument document)
        {
            Document = document;
            Session = new EditToolSession(document, GeometryTolerance.Default);
            Session.CommandRequested += Commands.Add;
            Session.StatusKeyRequested += StatusKeys.Add;
            Session.ResultEntitySelected += Selected.Add;
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

    // ------------------------------------------------------------ continuous

    [Fact]
    public void Join_AfterSuccessfulCommit_StaysInJoinTool()
    {
        var h = new Harness(JoinDocument());
        h.Session.ActivateTool(ToolMode.Join);
        h.Session.OnPointerLeftDown(new Point2(100, 0), SnapResult.None, Tol);
        h.Session.OnPointerLeftDown(new Point2(200, 0), SnapResult.None, Tol);
        h.Commands[0].Execute();

        h.Session.NotifyCommandCompleted(true);

        Assert.Equal(ToolMode.Join, h.Session.ActiveTool);
        // No pending gesture remains — the next click starts a fresh join.
        Assert.False(h.Session.HasPendingTarget);
        Assert.Single(h.Selected);
    }

    [Fact]
    public void Break_AfterSuccessfulCommit_StaysInBreakTool()
    {
        var h = new Harness(JoinDocument());
        h.Session.ActivateTool(ToolMode.Break);
        h.Session.OnPointerLeftDown(new Point2(50, 0), SnapResult.None, Tol);
        var command = Assert.IsType<BreakEntityCommand>(Assert.Single(h.Commands));
        command.Execute();

        h.Session.NotifyCommandCompleted(true);

        Assert.Equal(ToolMode.Break, h.Session.ActiveTool);
    }

    [Fact]
    public void Trim_AfterSuccessfulCommit_StaysInTrimTool()
    {
        var h = new Harness(TrimDocument());
        h.Session.ActivateTool(ToolMode.Trim);
        h.Session.OnPointerLeftDown(new Point2(15, 0), SnapResult.None, Tol);
        h.Session.OnPointerLeftDown(new Point2(45, 0), SnapResult.None, Tol);
        h.Commands[0].Execute();

        h.Session.NotifyCommandCompleted(true);

        Assert.Equal(ToolMode.Trim, h.Session.ActiveTool);
    }

    [Fact]
    public void Extend_AfterSuccessfulCommit_StaysInExtendTool()
    {
        var h = new Harness(ExtendDocument());
        h.Session.ActivateTool(ToolMode.Extend);
        h.Session.OnPointerLeftDown(new Point2(48, 0), SnapResult.None, Tol);
        h.Session.OnPointerLeftDown(new Point2(48, 0), SnapResult.None, Tol);
        h.Commands[0].Execute();

        h.Session.NotifyCommandCompleted(true);

        Assert.Equal(ToolMode.Extend, h.Session.ActiveTool);
    }

    // ------------------------------------------------------------- activation

    [Fact]
    public void ActivateTool_RaisesLocalizedHintOnce()
    {
        var h = new Harness(JoinDocument());

        h.Session.ActivateTool(ToolMode.Join);
        h.Session.ActivateTool(ToolMode.Join); // idempotent — no second hint

        Assert.Equal(["EditTools.Join.Hint"], h.StatusKeys);
    }

    [Fact]
    public void ActivateTool_HintsAreToolSpecific()
    {
        var h = new Harness(JoinDocument());

        h.Session.ActivateTool(ToolMode.Break);
        h.Session.ActivateTool(ToolMode.Trim);
        h.Session.ActivateTool(ToolMode.Extend);

        Assert.Contains("EditTools.Break.Hint", h.StatusKeys);
        Assert.Contains("EditTools.Trim.Hint", h.StatusKeys);
        Assert.Contains("EditTools.Extend.Hint", h.StatusKeys);
        Assert.DoesNotContain("EditTools.Join.Hint", h.StatusKeys);
    }

    // -------------------------------------------------------------- anti-storm

    [Fact]
    public void RepeatedIdenticalStatusRequests_AreDeduplicated()
    {
        var h = new Harness(JoinDocument());
        h.Session.ActivateTool(ToolMode.Join);

        // Two clicks with the same refusal after the first pick fired
        // different keys — here we directly replay the hover + click pattern
        // that used to publish the same key repeatedly.
        h.Session.OnPointerLeftDown(new Point2(0, 0), SnapResult.None, Tol); // first pick
        h.Session.OnPointerLeftDown(new Point2(150, 150), SnapResult.None, Tol); // not an endpoint → NoEndpoint
        int noEndpointCount = h.StatusKeys.Count(k => k == "EditTools.Join.NoEndpoint");
        h.Session.OnPointerLeftDown(new Point2(160, 160), SnapResult.None, Tol); // again → dedup
        int noEndpointAfter = h.StatusKeys.Count(k => k == "EditTools.Join.NoEndpoint");

        Assert.Equal(1, noEndpointCount);
        Assert.Equal(noEndpointCount, noEndpointAfter);
    }

    [Fact]
    public void DifferentKeysInARow_AreAllRaised()
    {
        var h = new Harness(JoinDocument());
        h.Session.ActivateTool(ToolMode.Join);

        h.Session.OnPointerLeftDown(new Point2(0, 0), SnapResult.None, Tol);
        h.Session.OnPointerLeftDown(new Point2(0, 0), SnapResult.None, Tol);

        Assert.Contains("EditTools.Join.PickSecond", h.StatusKeys);
        Assert.Contains("EditTools.Join.SameEndpoint", h.StatusKeys);
    }

    // -------------------------------------------------------------- join hover

    [Fact]
    public void JoinHover_BeforeFirstPick_ShowsEndpointMarker()
    {
        var h = new Harness(JoinDocument());
        h.Session.ActivateTool(ToolMode.Join);

        h.Session.OnPointerMoved(new Point2(100, 0), SnapResult.None, Tol);

        Assert.Equal(ToolPreviewKind.Normal, h.Session.Overlay.Kind);
        Assert.Single(h.Session.Overlay.Markers);
        Assert.Equal(new Point2(100, 0), h.Session.Overlay.Markers[0]);
        Assert.Empty(h.Session.Overlay.HighlightRuns);
        Assert.False(h.Session.HasPendingTarget);
    }

    [Fact]
    public void JoinHover_NoEndpoint_NoMarker()
    {
        var h = new Harness(JoinDocument());
        h.Session.ActivateTool(ToolMode.Join);

        h.Session.OnPointerMoved(new Point2(150, 150), SnapResult.None, Tol);

        Assert.Equal(ToolPreviewKind.None, h.Session.Overlay.Kind);
        Assert.Empty(h.Session.Overlay.Markers);
    }

    // ---------------------------------------------------------- break invalid

    [Fact]
    public void BreakHover_OnEndpoint_ShowsInvalidMarker()
    {
        var h = new Harness(JoinDocument());
        h.Session.ActivateTool(ToolMode.Break);

        h.Session.OnPointerMoved(new Point2(0, 0), SnapResult.None, Tol);

        Assert.Equal(ToolPreviewKind.Invalid, h.Session.Overlay.Kind);
        Assert.Single(h.Session.Overlay.Markers);
    }

    [Fact]
    public void BreakHover_OnInterior_ShowsNormalMarker()
    {
        var h = new Harness(JoinDocument());
        h.Session.ActivateTool(ToolMode.Break);

        h.Session.OnPointerMoved(new Point2(50, 0), SnapResult.None, Tol);

        Assert.Equal(ToolPreviewKind.Normal, h.Session.Overlay.Kind);
        Assert.Single(h.Session.Overlay.Markers);
    }

    // ---------------------------------------------------------- trim sticky

    [Fact]
    public void Trim_StickyPlan_ReusedWhenCursorDidNotMoveAfterHover()
    {
        var h = new Harness(TrimDocument());
        h.Session.ActivateTool(ToolMode.Trim);
        h.Session.OnPointerLeftDown(new Point2(15, 0), SnapResult.None, Tol);

        // Hover section A (between x=30 and x=60).
        h.Session.OnPointerMoved(new Point2(45, 0), SnapResult.None, Tol);
        Assert.Equal(ToolPreviewKind.Remove, h.Session.Overlay.Kind);
        Assert.Equal(2, h.Session.Overlay.Markers.Count);

        // The click lands within the pick tolerance of the hovered cursor —
        // the committed section is the one that was previewed.
        h.Session.OnPointerLeftDown(new Point2(45, 0), SnapResult.None, Tol);
        var command = Assert.IsType<TrimSectionCommand>(Assert.Single(h.Commands));
        command.Execute();

        // Section [0.3, 0.6] (x=30..60) removed: the id-1 piece runs to x=30,
        // a fresh right piece starts at x=60, the boundary lines survive.
        Assert.Equal(4, h.Document.Entities.Count);
        var kept = Assert.IsType<LineGeometry>(h.Document.Entities.Single(e => e.Id == 1));
        Assert.Equal(new Point2(30, 0), kept.EndPoint);
        h.Document.Entities.Single(e => e.Id != 1 && e.Id != 2 && e.Id != 3);
        Assert.Contains(h.Document.Entities, e => e is LineGeometry l && l.StartPoint == new Point2(60, 0));
    }

    [Fact]
    public void Trim_RecomputeWhenCursorJumps_FollowsNewCursor()
    {
        var h = new Harness(TrimDocument());
        h.Session.ActivateTool(ToolMode.Trim);
        h.Session.OnPointerLeftDown(new Point2(15, 0), SnapResult.None, Tol);

        h.Session.OnPointerMoved(new Point2(45, 0), SnapResult.None, Tol);
        // Cursor dives toward the first section (x=10 < 30) — far enough that
        // the sticky plan must NOT be reused.
        h.Session.OnPointerMoved(new Point2(10, 0), SnapResult.None, Tol);
        h.Session.OnPointerLeftDown(new Point2(10, 0), SnapResult.None, Tol);

        var command = Assert.IsType<TrimSectionCommand>(Assert.Single(h.Commands));
        command.Execute();

        // Section [0, 0.3] removed: the id-1 piece runs from x=30.
        Assert.Equal(3, h.Document.Entities.Count);
        var kept = Assert.IsType<LineGeometry>(h.Document.Entities.Single(e => e.Id == 1));
        Assert.Equal(new Point2(30, 0), kept.StartPoint);
        Assert.Equal(new Point2(100, 0), kept.EndPoint);
    }

    [Fact]
    public void Trim_StickyPlan_ResetByDocumentChange()
    {
        var h = new Harness(TrimDocument());
        h.Session.ActivateTool(ToolMode.Trim);
        h.Session.OnPointerLeftDown(new Point2(15, 0), SnapResult.None, Tol);
        h.Session.OnPointerMoved(new Point2(45, 0), SnapResult.None, Tol);

        h.Session.OnDocumentChanged();
        h.Session.OnPointerMoved(new Point2(45, 0), SnapResult.None, Tol);

        // Pending target gone (document changed) — first-step highlight only.
        Assert.False(h.Session.HasPendingTarget);
        Assert.Equal(ToolPreviewKind.Normal, h.Session.Overlay.Kind);
    }

    // ----------------------------------------------------------- extend hover

    [Fact]
    public void ExtendHover_FirstStep_MarksTheEndToExtend()
    {
        var h = new Harness(ExtendDocument());
        h.Session.ActivateTool(ToolMode.Extend);

        // Cursor near the free end (50,0) → KeepStart side → the EndPoint is
        // the end that would be extended to the boundary at x=80.
        h.Session.OnPointerMoved(new Point2(48, 0), SnapResult.None, Tol);

        Assert.Equal(ToolPreviewKind.Normal, h.Session.Overlay.Kind);
        Assert.Equal(new Point2(50, 0), h.Session.Overlay.Markers[0]); // EndPoint marker
        Assert.Single(h.Session.Overlay.HighlightRuns);
    }

    [Fact]
    public void ExtendHover_FirstStep_SideWithoutBoundary_ShowsInvalidMarker()
    {
        var h = new Harness(ExtendDocument());
        h.Session.ActivateTool(ToolMode.Extend);

        // Cursor at the start end (0,0) → KeepEnd side → the StartPoint would
        // extend in the -x half-plane where no boundary exists → the marker
        // is invalid so the click-refusal does not surprise.
        h.Session.OnPointerMoved(new Point2(0, 0), SnapResult.None, Tol);

        Assert.Equal(ToolPreviewKind.Invalid, h.Session.Overlay.Kind);
        Assert.Equal(new Point2(0, 0), h.Session.Overlay.Markers[0]); // StartPoint marker
    }

    [Fact]
    public void ExtendClick_FirstStep_SetsFreeEndMarkerImmediately()
    {
        var h = new Harness(ExtendDocument());
        h.Session.ActivateTool(ToolMode.Extend);

        h.Session.OnPointerLeftDown(new Point2(48, 0), SnapResult.None, Tol);

        Assert.Equal(ToolPreviewKind.Normal, h.Session.Overlay.Kind);
        Assert.Equal(new Point2(50, 0), h.Session.Overlay.Markers[0]);
        Assert.True(h.Session.HasPendingTarget);
        Assert.Contains("EditTools.Extend.PickEnd", h.StatusKeys);
    }
}