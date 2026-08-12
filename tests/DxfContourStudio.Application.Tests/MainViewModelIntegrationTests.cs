#nullable enable

using System.Collections.ObjectModel;
using System.IO;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Localization;
using DxfContourStudio.Application.Selection;
using DxfContourStudio.Core.Contours;
using DxfContourStudio.Wpf.ViewModels;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Application-programmed integration of the main window view model against
/// the real signed-in testdata files 鈥?no interactive UI, no window, straight
/// through the same public surface the toolbar/menu commands use. Regression
/// for the GUI crash "clicking Analyze terminates the app" and for the
/// auto-fit-after-open behaviour the GUI showed as a blank viewport.
/// </summary>
public class MainViewModelIntegrationTests
{
    private static string SamplePath(string relative) =>
        Path.Combine(AppContext.BaseDirectory, relative);

    [Fact]
    public void OpenOuterHole_AutoFitsToFiniteViewport()
    {
        var vm = new MainViewModel();
        vm.OpenFile(SamplePath("testdata/dxf/outer_hole.dxf"));

        Assert.True(vm.IsOpen);
        Assert.Equal(2, vm.Document.Entities.Count);
        // OpenFile sets a pending auto-fit; with the default 1000x800 test
        // viewport size it must have run: zoom > 0 and finite, and the camera
        // moved from its (0,0) default onto the plan centroid (100,75) of the
        // 0..200 x 0..150 outer rectangle.
        Assert.True(vm.Viewport.PixelsPerWorld > 0);
        Assert.True(double.IsFinite(vm.Viewport.PixelsPerWorld));
        Assert.Equal(100, vm.Viewport.Center.X, 3);
        Assert.Equal(75, vm.Viewport.Center.Y, 3);
    }

    [Fact]
    public void OuterHole_AnalyzeDoesNotThrowAndPopulatesPanels()
    {
        var vm = new MainViewModel();
        vm.OpenFile(SamplePath("testdata/dxf/outer_hole.dxf"));

        // The exact sequence the toolbar button runs.
        vm.AnalyzeCommand.Execute(null);

        ContourAnalysisResult? result = vm.AnalysisResult;
        Assert.NotNull(result);
        Assert.Equal(2, result.ClosedCount);
        Assert.Equal(0, result.OpenCount);
        Assert.Equal(1, result.OuterCount);
        Assert.Equal(1, result.HoleCount);

        Assert.Equal(2, vm.ContourItems.Count);
        Assert.Empty(vm.DiagnosticItems);
        Assert.DoesNotContain("澶辫触", vm.StatusText);
    }

    [Fact]
    public void SmallGap_AnalyzeRepairUndoRedoEndToEnd()
    {
        var vm = new MainViewModel();
        vm.OpenFile(SamplePath("testdata/dxf/small_gap_003.dxf"));

        vm.AnalyzeCommand.Execute(null);
        ContourAnalysisResult? first = vm.AnalysisResult;
        Assert.NotNull(first);
        Assert.Equal(1, first.SmallGapCount);
        Assert.Equal(2, first.OpenCount);
        Assert.Equal(0, first.ClosedCount);
        Assert.Equal(0.03, Assert.Single(first.Diagnostics, d => d.Kind == GapKind.SmallGap).Distance, 9);

// repair the selected gap row (pick the auto-repairable SmallGap row)
        DiagnosticItemViewModel gapRow = Assert.Single(vm.DiagnosticItems, d => d.Gap?.Kind == GapKind.SmallGap);
        vm.SelectedDiagnostic = gapRow;
        Assert.True(vm.RepairSelectedGapCommand.CanExecute(null));
        vm.RepairSelectedGapCommand.Execute(null);

ContourAnalysisResult? afterRepair = vm.AnalysisResult;
        Assert.NotNull(afterRepair);
        // repair → analysis is re-run automatically (the D17 exception: the
        // repair commands keep the panels fresh).
        Assert.False(vm.IsAnalysisStale);
        Assert.Equal(0, afterRepair.SmallGapCount);
        // Open-chain contract: the two runs join into one open chain.
        Assert.Equal(1, afterRepair.OpenCount);
        Assert.Equal(0, afterRepair.ClosedCount);
        Assert.DoesNotContain(vm.DiagnosticItems, d => d.Gap?.Kind == GapKind.SmallGap);

        // undo → the geometry changed, so the analysis goes stale: the result
        // object is kept as-is (one click re-analyzes) but every row and
        // overlay marker is dropped and the stale flag is raised.
        vm.UndoCommand.Execute(null);
        Assert.True(vm.IsAnalysisStale);
        Assert.Empty(vm.DiagnosticItems);
        // The kept result object is the repaired snapshot — no re-analysis
        // happened on undo.
        Assert.Equal(0, vm.AnalysisResult!.SmallGapCount);

        // re-analyze → fresh again, the small gap row is back.
        vm.AnalyzeCommand.Execute(null);
        Assert.False(vm.IsAnalysisStale);
        Assert.Equal(1, vm.AnalysisResult!.SmallGapCount);
        Assert.Single(vm.DiagnosticItems, d => d.Gap?.Kind == GapKind.SmallGap);

        // redo → stale again (the redo also edits geometry).
        vm.RedoCommand.Execute(null);
        Assert.True(vm.IsAnalysisStale);
        Assert.Empty(vm.DiagnosticItems);
        // The kept result object is the re-analyzed (gap restored) snapshot.
        Assert.Equal(1, vm.AnalysisResult!.SmallGapCount);
    }

    [Fact]
    public void AnalyzeOnEmptyDocument_CannotExecute()
    {
        var vm = new MainViewModel();
        Assert.False(vm.AnalyzeCommand.CanExecute(null));
    }
}
