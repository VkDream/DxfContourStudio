#nullable enable

using System;
using System.Collections.Generic;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Commands;

/// <summary>
/// Joins two endpoint-adjacent entities (Line + Line, Line + Arc, Arc + Arc,
/// Polyline + any path) into a single mixed <see cref="PolylineGeometry"/>.
///
/// Rules enforced here (see docs/ADR/ADR-011-Join-Semantics.md):
/// - endpoints must be within <see cref="GeometryTolerance.JoinTolerance"/>;
/// - exactly one matching endpoint pair (no ambiguity);
/// - same layer (cross-layer join is refused);
/// - the primary entity's id and layer survive; the secondary entity is
///   removed. Undo restores both original entities with ids, order and layers
///   intact.
/// </summary>
public sealed class JoinEntitiesCommand : ICommand
{
    private readonly CadDocument _document;
    private readonly long _primaryId;
    private readonly long _secondaryId;
    private readonly GeometryTolerance _tolerance;
    private readonly PolylineGeometry _joined;

    private IGeometryEntity? _primaryOriginal;
    private IGeometryEntity? _secondaryOriginal;
    private int _primaryIndex = -1;
    private int _secondaryIndex = -1;

    public string Name { get; }

    public JoinEntitiesCommand(CadDocument document, long primaryId, long secondaryId, GeometryTolerance tolerance, string name = "Join entities")
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _primaryId = primaryId;
        _secondaryId = secondaryId;
        _tolerance = tolerance;
        Name = name;

        var primary = document.GetEntityById(primaryId)
            ?? throw new ArgumentException($"JoinEntitiesCommand: primary entity {primaryId} not found.");
        var secondary = document.GetEntityById(secondaryId)
            ?? throw new ArgumentException($"JoinEntitiesCommand: secondary entity {secondaryId} not found.");

        var attempt = JoinEngine.TryJoin(primary, secondary, primaryId, tolerance);
        if (!attempt.IsValid || attempt.Joined is null)
        {
            throw new ArgumentException($"JoinEntitiesCommand: cannot join {primaryId} and {secondaryId}: {attempt.Reason}.");
        }

        _joined = attempt.Joined;
    }

    public void Execute()
    {
        var entities = _document.Entities;
        if (_primaryOriginal is null)
        {
            // First execution (or after Undo): capture snapshots for undo.
            _primaryOriginal = FindAndCapture(_primaryId, out _primaryIndex);
            _secondaryOriginal = FindAndCapture(_secondaryId, out _secondaryIndex);
            if (_primaryOriginal is null || _secondaryOriginal is null)
            {
                throw new InvalidOperationException("JoinEntitiesCommand: entities vanished between construction and execution.");
            }
        }
        else
        {
            // Redo after Undo: restore the merged entity in place of the
            // re-inserted originals (their current positions are captured).
            _primaryIndex = IndexOf(_primaryId);
            _secondaryIndex = IndexOf(_secondaryId);
        }

        _document.ReplaceEntity(_joined);
        _document.RemoveEntity(_secondaryId);
    }

    public void Undo()
    {
        if (_primaryOriginal is null || _secondaryOriginal is null)
        {
            throw new InvalidOperationException("JoinEntitiesCommand.Undo before Execute.");
        }

        _document.RemoveEntity(_joined.Id);
        _document.InsertEntity(_secondaryIndex, _secondaryOriginal);
        _document.InsertEntity(_primaryIndex, _primaryOriginal);
    }

    private IGeometryEntity? FindAndCapture(long id, out int index)
    {
        index = IndexOf(id);
        return index >= 0 ? _document.Entities[index] : null;
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