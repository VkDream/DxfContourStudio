#nullable enable

using System.IO;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Interaction;
using DxfContourStudio.Application.Localization;
using DxfContourStudio.Core.Contours;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Wpf.ViewModels;
using Xunit;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// View-model level tests for the D17 editing-tool wiring: the string
/// command parameter activates the right tool, the session's requests are
/// bridged into the history stack / selection / status bar, the Esc chain
/// walks gesture 鈫?tool 鈫?selection, and any geometry change (including a
/// tool commit) marks the analysis stale while gap repair keeps it fresh.
/// The gesture state machine itself is covered by EditToolSessionTests.
/// </summary>
public class MainViewModelToolTests
{
    private const double Tol = 5.0;

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

    private static CadDocument LineDocument()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [new LineGeometry(1, "0", new Point2(0, 0), new Point2(100, 0))],
            [new LayerState("0", true, false, 7, true)], null, null, null);
        return doc;
    }

[Fact]
    public void ActivateTool_StringParameter_ActivatesToolAndKeepsSelection()
    {
        var vm = new MainViewModel();
        vm.Document.ReplaceContent(LineDocument().Entities, [], null, null, null);
        vm.Selection.Add(1);

        vm.ActivateToolCommand.Execute("Join");

        Assert.Equal(ToolMode.Join, vm.CurrentTool);
        Assert.Equal(ToolMode.Join, vm.ToolSession.ActiveTool);
        // v0.3.0: switching tools preserves the selection; the viewport just
        // hides the grips while a tool owns the mouse.
        Assert.Equal(1, vm.Selection.PrimaryId);
        Assert.False(string.IsNullOrEmpty(vm.ToolStatusText));
    }

    [Fact]
    public void ActivateTool_BackToSelect_RestoresEmptyStatus()
    {
        var vm = new MainViewModel();
        vm.ActivateToolCommand.Execute("Break");
        vm.ActivateToolCommand.Execute("Select");

        Assert.Equal(ToolMode.Select, vm.CurrentTool);
        Assert.Equal("", vm.ToolStatusText);
    }

    [Fact]
    public void JoinTool_FullPipeline_ExecutesUndoableCommandAndSelectsResult()
    {
        var vm = new MainViewModel();
        vm.Document.ReplaceContent(JoinDocument().Entities, [], null, null, null);

vm.ActivateToolCommand.Execute("Join");
        vm.ToolSession.OnPointerLeftDown(new Point2(100, 0), SnapResult.None, Tol);
        vm.ToolSession.OnPointerLeftDown(new Point2(200, 0), SnapResult.None, Tol);

        // The join was executed through the history stack and the survivor is
        // selected; the tool stays active (v0.3.0 continuous mode).
        Assert.Equal(1, vm.History.UndoCount);
        Assert.Single(vm.Document.Entities);
        Assert.Equal(1, vm.Selection.PrimaryId);
        Assert.Equal(ToolMode.Join, vm.CurrentTool);

        // Undo restores both original entities.
        vm.UndoCommand.Execute(null);
        Assert.Equal(2, vm.Document.Entities.Count);
    }

    [Fact]
    public void RefusedJoin_ReportsLocalizedStatus_StaysInTool()
    {
        var vm = new MainViewModel();
        var doc = new CadDocument();
        doc.ReplaceContent(
            [
                new LineGeometry(1, "0", new Point2(0, 0), new Point2(100, 0)),
                new LineGeometry(2, "0", new Point2(300, 0), new Point2(400, 0)),
            ],
            [new LayerState("0", true, false, 7, true)], null, null, null);
        vm.Document.ReplaceContent(doc.Entities, [], null, null, null);

        vm.ActivateToolCommand.Execute("Join");
        vm.ToolSession.OnPointerLeftDown(new Point2(100, 0), SnapResult.None, Tol);
        vm.ToolSession.OnPointerLeftDown(new Point2(300, 0), SnapResult.None, Tol);

        Assert.Equal(0, vm.History.UndoCount);
        Assert.Equal(ToolMode.Join, vm.CurrentTool);
        // The status line carries the localized refusal text (resolved from
        // the session's status key by the VM bridge).
        Assert.Equal(
            LocalizationService.Instance.Get("EditTools.Join.NotConnected"),
            vm.StatusText);
    }

    [Fact]
    public void Esc_Chain_CancelsGestureThenLeavesToolThenClearsSelection()
    {
        var vm = new MainViewModel();
        vm.Document.ReplaceContent(JoinDocument().Entities, [], null, null, null);

        vm.ActivateToolCommand.Execute("Join");
        vm.ToolSession.OnPointerLeftDown(new Point2(100, 0), SnapResult.None, Tol);
        Assert.True(vm.ToolSession.HasPendingTarget);

        // 1st Esc: cancels the pending gesture, the tool stays active.
        vm.CancelToolCommand.Execute(null);
        Assert.Equal(ToolMode.Join, vm.CurrentTool);
        Assert.False(vm.ToolSession.HasPendingTarget);

        // 2nd Esc: leaves the tool.
        vm.CancelToolCommand.Execute(null);
        Assert.Equal(ToolMode.Select, vm.CurrentTool);

        // 3rd Esc: clears the selection.
        vm.Selection.Add(1);
        vm.CancelToolCommand.Execute(null);
        Assert.Empty(vm.Selection.Ids);
    }

    [Fact]
    public void ToolCommit_InvalidatesAnalysis_ButRepairKeepsItFresh()
    {
        var vm = new MainViewModel();
        vm.OpenFile(Path.Combine(AppContext.BaseDirectory, "testdata/dxf/small_gap_003.dxf"));
        vm.AnalyzeCommand.Execute(null);
        Assert.False(vm.IsAnalysisStale);
        Assert.Equal(1, vm.AnalysisResult!.SmallGapCount);
        Assert.Equal(2, vm.AnalysisResult.OpenCount);

        // Delete one entity (any undoable edit) 鈫?stale.
        vm.Selection.Add(1);
        vm.DeleteCommand.Execute(null);
        Assert.True(vm.IsAnalysisStale);
        Assert.Empty(vm.DiagnosticItems);
        // The kept result object still reports the old numbers.
        Assert.Equal(1, vm.AnalysisResult.SmallGapCount);

        // Re-analyze 鈫?fresh, revision bumped.
        int revision = vm.AnalysisRevision;
        vm.AnalyzeCommand.Execute(null);
        Assert.False(vm.IsAnalysisStale);
        Assert.Equal(revision + 1, vm.AnalysisRevision);
    }

    [Fact]
    public void UndoOfToolCommand_AlsoMarksAnalysisStale()
    {
        // Any undoable geometry edit 鈥?here a join executed through the tool
        // session 鈥?marks the analysis stale, and undoing it marks it stale
        // again.
        var vm = new MainViewModel();
        vm.Document.ReplaceContent(JoinDocument().Entities, [], null, null, null);

        vm.ActivateToolCommand.Execute("Join");
        vm.ToolSession.OnPointerLeftDown(new Point2(100, 0), SnapResult.None, Tol);
        vm.ToolSession.OnPointerLeftDown(new Point2(200, 0), SnapResult.None, Tol);
        // v0.3.0: continuous mode — the tool stays active after a commit.
        Assert.Equal(ToolMode.Join, vm.CurrentTool);

        vm.UndoCommand.Execute(null);
        Assert.Equal(2, vm.Document.Entities.Count);
    }

    [Fact]
    public void UndoOfAnyEdit_KeepsResultButRaisesStale()
    {
        var vm = new MainViewModel();
        vm.OpenFile(Path.Combine(AppContext.BaseDirectory, "testdata/dxf/small_gap_003.dxf"));
        vm.AnalyzeCommand.Execute(null);
        Assert.False(vm.IsAnalysisStale);

        vm.Selection.Add(1);
        vm.DeleteCommand.Execute(null);
        Assert.True(vm.IsAnalysisStale);

        vm.UndoCommand.Execute(null);
        Assert.True(vm.IsAnalysisStale);
        Assert.NotNull(vm.AnalysisResult);
    }

    [Fact]
    public void ToolSession_HoverAndDocumentChange_ClearPendingWithoutLeavingTool()
    {
        var vm = new MainViewModel();
        vm.Document.ReplaceContent(JoinDocument().Entities, [], null, null, null);

        vm.ActivateToolCommand.Execute("Join");
        vm.ToolSession.OnPointerLeftDown(new Point2(100, 0), SnapResult.None, Tol);
        Assert.True(vm.ToolSession.HasPendingTarget);

        // Hovering never affects the gesture.
        vm.ToolSession.OnPointerMoved(new Point2(150, 0), SnapResult.None, Tol);
        Assert.True(vm.ToolSession.HasPendingTarget);

        // A document mutation (e.g. an undo) abandons the pending gesture.
        vm.ToolSession.OnDocumentChanged();
        Assert.False(vm.ToolSession.HasPendingTarget);
        Assert.Equal(ToolMode.Join, vm.CurrentTool);
    }
}

