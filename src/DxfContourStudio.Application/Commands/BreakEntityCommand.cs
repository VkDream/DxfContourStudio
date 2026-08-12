#nullable enable

using System;
using System.Linq;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Commands;

/// <summary>
/// Splits one path entity (Line / Arc / Polyline) into two pieces at a world
/// point that lies on the curve (within tolerance). The left piece keeps the
/// original entity id, the right piece gets a fresh id
/// (<c>max(entity ids) + 1</c>). Undo removes both pieces and restores the
/// original entity exactly at its original document position.
///
/// Refused (see docs/ADR/ADR-012-Break-Semantics.md): cutting at the path
/// endpoints (t == 0 or 1), points farther than tolerance from the curve,
/// and unsupported entity kinds.
/// </summary>
public sealed class BreakEntityCommand : ICommand
{
    private readonly CadDocument _document;
    private readonly long _entityId;
    private readonly Point2 _cutPoint;
    private readonly double _tolerance;

    private IGeometryEntity? _original;
    private IGeometryEntity? _left;
    private IGeometryEntity? _right;
    private int _originalIndex = -1;

    public string Name { get; }

    public BreakEntityCommand(CadDocument document, long entityId, Point2 cutPoint, double tolerance, string name = "Break entity")
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _entityId = entityId;
        _cutPoint = cutPoint;
        _tolerance = tolerance;
        Name = name;

        var entity = document.GetEntityById(entityId)
            ?? throw new ArgumentException($"BreakEntityCommand: entity {entityId} not found.");
        PlanSplit(entity);
    }

    private void PlanSplit(IGeometryEntity entity)
    {
        if (!PathBreaker.TryProjectParameter(entity, _cutPoint, _tolerance, out double t, out _))
        {
            throw new ArgumentException(
                $"BreakEntityCommand: cut point {_cutPoint} is not on entity {_entityId} within tolerance {_tolerance}.");
        }

        long rightId = NextId();
        var split = PathBreaker.SplitEntity(entity, t, _entityId, rightId)
            ?? throw new ArgumentException("BreakEntityCommand: cannot split at the entity endpoints.");
        _left = split.Item1;
        _right = split.Item2;
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
            // First execution or after Undo: capture the pristine entity.
            _original = _document.GetEntityById(_entityId)
                ?? throw new InvalidOperationException("BreakEntityCommand: entity vanished before execution.");
            _originalIndex = IndexOf(_entityId);
        }

        _document.ReplaceEntity(_left!);
        _document.AddEntity(_right!);
    }

    public void Undo()
    {
        if (_original is null)
        {
            throw new InvalidOperationException("BreakEntityCommand.Undo before Execute.");
        }

        _document.RemoveEntity(_left!.Id);
        _document.RemoveEntity(_right!.Id);
        _document.InsertEntity(_originalIndex, _original);
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