#nullable enable

using System;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Commands;

/// <summary>
/// Replaces one entity with an edited copy produced by a node-edit session
/// (D14) as a single undoable command. The entity keeps its id; undo restores
/// the pristine original at its document position. Commands are created only
/// after the edited result passed <see cref="Interaction.NodeEditValidator"/>,
/// so a failed gesture never reaches the history.
/// </summary>
public sealed class NodeEditCommand : ICommand
{
    private readonly CadDocument _document;
    private readonly long _entityId;
    private readonly IGeometryEntity _edited;

    private IGeometryEntity? _original;
    private int _originalIndex = -1;

    public string Name { get; }

    public NodeEditCommand(CadDocument document, IGeometryEntity original, IGeometryEntity edited, string name = "Edit node")
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _entityId = original.Id;
        _edited = edited ?? throw new ArgumentNullException(nameof(edited));
        Name = name;

        if (edited.Id != original.Id)
        {
            throw new ArgumentException("NodeEditCommand: the edited copy must keep the original id.");
        }
    }

    public void Execute()
    {
        if (_original is null)
        {
            _original = _document.GetEntityById(_entityId)
                ?? throw new InvalidOperationException("NodeEditCommand: entity vanished before execution.");
            _originalIndex = IndexOf(_entityId);
        }

        _document.ReplaceEntity(_edited);
    }

    public void Undo()
    {
        if (_original is null)
        {
            throw new InvalidOperationException("NodeEditCommand.Undo before Execute.");
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