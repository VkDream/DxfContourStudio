#nullable enable

using DxfContourStudio.Application.Commands;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Contours;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Tests for batch repair (one composite undo), the document dirty flag and
/// the unsaved-changes guard decision flow.
/// </summary>
public class BatchRepairAndDirtyTests
{
    /// <summary>
    /// A document with three independent small gaps (two lines each, 0.03 mm).
    /// </summary>
    private static CadDocument ThreeGapsDocument()
    {
        var doc = new CadDocument();
        var entities = new List<IGeometryEntity>
        {
            new LineGeometry(1, "0", new Point2(0, 0), new Point2(100, 0)),
            new LineGeometry(2, "0", new Point2(100.03, 0), new Point2(200, 0)),
            new LineGeometry(3, "0", new Point2(0, 50), new Point2(100, 50)),
            new LineGeometry(4, "0", new Point2(100.03, 50), new Point2(200, 50)),
            new LineGeometry(5, "0", new Point2(0, 100), new Point2(100, 100)),
            new LineGeometry(6, "0", new Point2(100.03, 100), new Point2(200, 100)),
        };
        doc.ReplaceContent(entities, [new LayerState("0", true, false, 7, true)], null, null, null);
        return doc;
    }

    [Fact]
    public void BatchRepair_AllGapsClosedAndUndoneInOneStep()
    {
        CadDocument doc = ThreeGapsDocument();
        ContourAnalysisResult analysis = ContourAnalyzer.Analyze(doc.Entities.ToList());
        Assert.Equal(3, analysis.SmallGapCount);

        var history = new CommandHistory();
        var batch = new BatchRepairCommand(doc, analysis);
        Assert.Equal(3, batch.GapCount);
        history.Execute(batch);

        ContourAnalysisResult after = ContourAnalyzer.Analyze(doc.Entities.ToList());
        Assert.Equal(0, after.SmallGapCount);

        // One Ctrl+Z restores every gap.
        Assert.True(history.CanUndo);
        history.TryUndo();
        ContourAnalysisResult undone = ContourAnalyzer.Analyze(doc.Entities.ToList());
        Assert.Equal(3, undone.SmallGapCount);

        // One Ctrl+Y re-applies the whole batch.
        Assert.True(history.CanRedo);
        history.TryRedo();
        ContourAnalysisResult redone = ContourAnalyzer.Analyze(doc.Entities.ToList());
        Assert.Equal(0, redone.SmallGapCount);
    }

    [Fact]
    public void BatchRepair_WithNoGaps_IsNoOp()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [new LineGeometry(1, "0", new Point2(0, 0), new Point2(100, 0))],
            [new LayerState("0", true, false, 7, true)], null, null, null);
        ContourAnalysisResult analysis = ContourAnalyzer.Analyze(doc.Entities.ToList());
        var batch = new BatchRepairCommand(doc, analysis);
        Assert.Equal(0, batch.GapCount);
        batch.Execute(); // must not throw
        Assert.Single(doc.Entities);
    }

    [Fact]
    public void RepairMarksDocumentDirty()
    {
        CadDocument doc = ThreeGapsDocument();
        doc.IsDirty = false;

        ContourAnalysisResult analysis = ContourAnalyzer.Analyze(doc.Entities.ToList());
        GapDiagnostic gap = analysis.Diagnostics.First(d => d.Kind == GapKind.SmallGap);
        new RepairGapCommand(doc, gap).Execute();

        Assert.True(doc.IsDirty);
    }

    [Fact]
    public void ImportMarksDocumentDirty()
    {
        var doc = new CadDocument();
        doc.IsDirty = false;
        doc.ReplaceContent([new LineGeometry(1, "0", new Point2(0, 0), new Point2(1, 0))], [], null, null, null);
        Assert.True(doc.IsDirty);
    }

    [Fact]
    public void CleanDocument_GuardProceedsWithoutPrompt()
    {
        var doc = new CadDocument();
        doc.IsDirty = false;
        var guard = new UnsavedChangesGuard(new StubPrompt(UnsavedPromptResult.Save));
        var (proceed, shouldSave) = guard.ConfirmBeforeDiscard(doc, "open another file");
        Assert.True(proceed);
        Assert.False(shouldSave);
    }

    [Fact]
    public void DirtyDocument_PromptSave_ProceedsAndSaves()
    {
        var doc = new CadDocument();
        doc.IsDirty = true;
        var guard = new UnsavedChangesGuard(new StubPrompt(UnsavedPromptResult.Save));
        var (proceed, shouldSave) = guard.ConfirmBeforeDiscard(doc, "open another file");
        Assert.True(proceed);
        Assert.True(shouldSave);
    }

    [Fact]
    public void DirtyDocument_PromptDiscard_ProceedsWithoutSave()
    {
        var doc = new CadDocument();
        doc.IsDirty = true;
        var guard = new UnsavedChangesGuard(new StubPrompt(UnsavedPromptResult.Discard));
        var (proceed, shouldSave) = guard.ConfirmBeforeDiscard(doc, "exit");
        Assert.True(proceed);
        Assert.False(shouldSave);
    }

    [Fact]
    public void DirtyDocument_PromptCancel_BlocksOperation()
    {
        var doc = new CadDocument();
        doc.IsDirty = true;
        var guard = new UnsavedChangesGuard(new StubPrompt(UnsavedPromptResult.Cancel));
        var (proceed, _) = guard.ConfirmBeforeDiscard(doc, "exit");
        Assert.False(proceed);
    }

    [Fact]
    public void LayerVisibilityToggle_DoesNotMarkDirty()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [new LineGeometry(1, "0", new Point2(0, 0), new Point2(100, 0))],
            [new LayerState("0", true, false, 7, true)], null, null, null);
        doc.IsDirty = false;

        // A view-level preference must not dirty the document data.
        doc.SetLayerVisible("0", false);
        Assert.False(doc.IsDirty);
    }
}
