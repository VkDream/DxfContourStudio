#nullable enable

using System.Collections.Generic;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Contours;

namespace DxfContourStudio.Application.Commands;

/// <summary>
/// Repairs every currently auto-repairable gap in one undoable step. The
/// repair list is captured at construction time from the analysis result, so
/// the command is stable even if the document changes before it is executed.
/// Undo restores every touched entity with a single Ctrl+Z (composite undo).
/// </summary>
public sealed class BatchRepairCommand : ICommand
{
    private readonly CompositeCommand _inner;

    /// <summary>Display name shown in the undo history.</summary>
    public string Name => _inner.Name;

    /// <summary>Number of gaps this batch repairs.</summary>
    public int GapCount { get; }

    public BatchRepairCommand(CadDocument document, ContourAnalysisResult analysis, string name = "Repair all gaps")
    {
        var repairs = new List<ICommand>();
        foreach (GapDiagnostic d in analysis.Diagnostics)
        {
            if (d.Kind == GapKind.SmallGap && d.CanAutoRepair)
            {
                repairs.Add(new RepairGapCommand(document, d));
            }
        }

        GapCount = repairs.Count;
        _inner = new CompositeCommand(name, repairs);
    }

    /// <inheritdoc />
    public void Execute() => _inner.Execute();

    /// <inheritdoc />
    public void Undo() => _inner.Undo();
}
