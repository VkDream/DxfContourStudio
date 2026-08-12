#nullable enable

using System;
using System.Linq;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Commands;

/// <summary>
/// Removes one contiguous section of a path entity (Line / Arc) between two
/// normalized path parameters [StartT, EndT], as chosen by the interactive
/// trim tool (D15). The rest of the path survives as one or two pieces:
///
/// - removal touches the start (StartT ≈ 0) → one piece [EndT, 1] replaces
///   the entity (original id kept),
/// - removal touches the end (EndT ≈ 1) → one piece [0, StartT] replaces the
///   entity (original id kept),
/// - interior removal → two pieces: [0, StartT] keeps the original id,
///   [EndT, 1] gets a fresh id (max id + 1).
///
/// Undo removes the fresh piece (if any) and restores the original entity
/// exactly at its original document position. Redo re-applies the split.
/// </summary>
public sealed class TrimSectionCommand : ICommand
{
    private readonly CadDocument _document;
    private readonly long _entityId;
    private readonly double _startT;
    private readonly double _endT;
    private const double ParamEps = 1e-9;

    private IGeometryEntity? _original;
    private IGeometryEntity? _left; // kept piece [0, StartT]  (original id)
    private IGeometryEntity? _right; // kept piece [EndT, 1]   (fresh id)
    private int _originalIndex = -1;

    public string Name { get; }

    public TrimSectionCommand(
        CadDocument document, long entityId, double startT, double endT, string name = "Trim section")
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _entityId = entityId;
        _startT = startT;
        _endT = endT;
        Name = name;

        IGeometryEntity entity = document.GetEntityById(entityId)
            ?? throw new ArgumentException($"TrimSectionCommand: entity {entityId} not found.");
        bool hasLeft = startT >= ParamEps;
        bool hasRight = endT <= 1 - ParamEps;
        if (!hasLeft && !hasRight)
        {
            throw new ArgumentException("TrimSectionCommand: the whole entity cannot be trimmed away.");
        }

        if (hasLeft)
        {
            // [0, StartT] — kept piece with the original id.
            _left = PathBreaker.SplitEntity(entity, startT, entity.Id, NextId())?.Item1
                ?? throw new ArgumentException("TrimSectionCommand: invalid start parameter.");
        }

        if (hasRight)
        {
            // [EndT, 1] — split the original path at EndT directly; the right
            // piece is the kept one (fresh id when the left piece exists,
            // original id when the removal touches the start and the right
            // piece replaces the entity).
            long next = NextId();
            long rightId = hasLeft ? next : entity.Id;
            long discardedId = hasLeft ? next + 1 : next;
            var split = PathBreaker.SplitEntity(entity, endT, discardedId, rightId)
                ?? throw new ArgumentException("TrimSectionCommand: invalid end parameter.");
            _right = split.Item2;
        }
    }

    private long NextId()
    {
        long max = _document.Entities.Count == 0 ? 0 : _document.Entities.Max(e => e.Id);
        return max + 1;
    }

    public void Execute()
    {
        if (_original is null)
        {
            _original = _document.GetEntityById(_entityId)
                ?? throw new InvalidOperationException("TrimSectionCommand: entity vanished before execution.");
            _originalIndex = IndexOf(_entityId);
        }

        if (_left is not null)
        {
            _document.ReplaceEntity(_left);
            if (_right is not null)
            {
                _document.AddEntity(_right);
            }
        }
        else if (_right is not null)
        {
            // Removal touches the start: the kept right piece (original id)
            // replaces the entity.
            _document.ReplaceEntity(_right);
        }
        else
        {
            _document.RemoveEntity(_entityId);
        }
    }

    public void Undo()
    {
        if (_original is null)
        {
            throw new InvalidOperationException("TrimSectionCommand.Undo before Execute.");
        }

        if (_right is not null)
        {
            _document.RemoveEntity(_right.Id);
        }

        if (_left is not null)
        {
            _document.ReplaceEntity(_original);
        }
        else
        {
            // The kept piece carried the original id — swap it back and
            // restore the original document order.
            _document.RemoveEntity(_original.Id);
            _document.InsertEntity(_originalIndex, _original);
        }
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