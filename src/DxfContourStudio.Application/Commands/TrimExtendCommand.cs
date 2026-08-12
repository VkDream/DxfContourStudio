#nullable enable

using System;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Commands;

/// <summary>
/// Trims or extends one end of a path entity to its intersection with a
/// boundary entity, as a single undoable command
/// (docs/ADR/ADR-013-Trim-Extend-Semantics.md). The primary keeps its id;
/// undo restores the pristine primary at its original document position.
/// A no-op (end already touching the boundary) leaves the document untouched.
/// </summary>
public sealed class TrimExtendCommand : ICommand
{
    private readonly CadDocument _document;
    private readonly long _primaryId;
    private readonly long _boundaryId;
    private readonly TrimSide _side;
    private readonly double _tolerance;

    private IGeometryEntity? _original;
    private IGeometryEntity? _resultEntity;
    private int _originalIndex = -1;

    public string Name { get; }

    public TrimExtendCommand(CadDocument document, long primaryId, long boundaryId, TrimSide side, double tolerance, string name = "Trim/extend entity")
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _primaryId = primaryId;
        _boundaryId = boundaryId;
        _side = side;
        _tolerance = tolerance;
        Name = name;

        var primary = document.GetEntityById(primaryId)
            ?? throw new ArgumentException($"TrimExtendCommand: primary entity {primaryId} not found.");
        var boundary = document.GetEntityById(boundaryId)
            ?? throw new ArgumentException($"TrimExtendCommand: boundary entity {boundaryId} not found.");

        var result = TrimExtendEngine.TrimEnd(primary, boundary, side, tolerance, primaryId)
            ?? throw new ArgumentException($"TrimExtendCommand: cannot trim/extend {primaryId} against {boundaryId} (no usable crossing).");
        _resultEntity = result.Entity;
    }

    public void Execute()
    {
        if (_original is null)
        {
            _original = _document.GetEntityById(_primaryId)
                ?? throw new InvalidOperationException("TrimExtendCommand: primary vanished before execution.");
            _originalIndex = IndexOf(_primaryId);
        }

        _document.ReplaceEntity(_resultEntity!);
    }

    public void Undo()
    {
        if (_original is null)
        {
            throw new InvalidOperationException("TrimExtendCommand.Undo before Execute.");
        }

        _document.ReplaceEntity(_original);
    }

    private int IndexOf(long id)
    {
        var entities = _document.Entities;
        for (int i = 0; i < entities.Count; i++)
        {
            if (entities[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }
}