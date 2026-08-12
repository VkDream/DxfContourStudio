#nullable enable

using System;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Commands;

/// <summary>
/// Moves one editable node (vertex / path endpoint) of an entity to an
/// absolute world position, as a single undoable command
/// (docs/ADR-014-Node-Editing.md). The entity keeps its id; undo restores
/// the pristine geometry at the original document position. Refused targets
/// (out of range, degenerate arcs) throw at construction.
/// </summary>
public sealed class MoveNodeCommand : ICommand
{
    private readonly CadDocument _document;
    private readonly long _entityId;
    private readonly int _nodeIndex;
    private readonly Point2 _target;

    private IGeometryEntity? _original;
    private IGeometryEntity? _moved;
    private int _originalIndex = -1;

    public string Name { get; }

    public MoveNodeCommand(CadDocument document, long entityId, int nodeIndex, Point2 target, string name = "Move node")
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _entityId = entityId;
        _nodeIndex = nodeIndex;
        _target = target;
        Name = name;

        var entity = document.GetEntityById(entityId)
            ?? throw new ArgumentException($"MoveNodeCommand: entity {entityId} not found.");

        if (nodeIndex < 0 || nodeIndex >= NodeEditEngine.NodeCount(entity))
        {
            throw new ArgumentOutOfRangeException(nameof(nodeIndex), $"MoveNodeCommand: node {nodeIndex} out of range for entity {entityId}.");
        }

        _moved = NodeEditEngine.MoveNode(entity, nodeIndex, target)
            ?? throw new ArgumentException($"MoveNodeCommand: refused to move node {nodeIndex} of entity {entityId} to {target}.");
    }

    public void Execute()
    {
        if (_original is null)
        {
            _original = _document.GetEntityById(_entityId)
                ?? throw new InvalidOperationException("MoveNodeCommand: entity vanished before execution.");
            _originalIndex = IndexOf(_entityId);
        }

        _document.ReplaceEntity(_moved!);
    }

    public void Undo()
    {
        if (_original is null)
        {
            throw new InvalidOperationException("MoveNodeCommand.Undo before Execute.");
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