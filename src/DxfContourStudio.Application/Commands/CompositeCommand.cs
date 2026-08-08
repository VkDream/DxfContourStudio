#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace DxfContourStudio.Application.Commands;

/// <summary>
/// Runs several commands as one undoable unit: Execute runs every child in
/// order, Undo reverses them in reverse order. Used for batch repairs so the
/// user can undo a whole "repair all gaps" with a single Ctrl+Z.
/// </summary>
public sealed class CompositeCommand : ICommand
{
    private readonly IReadOnlyList<ICommand> _children;

    /// <summary>Display name for the whole group.</summary>
    public string Name { get; }

    public CompositeCommand(string name, IEnumerable<ICommand> children)
    {
        Name = name;
        _children = children.ToList();
    }

    /// <inheritdoc />
    public void Execute()
    {
        foreach (ICommand child in _children)
        {
            child.Execute();
        }
    }

    /// <inheritdoc />
    public void Undo()
    {
        for (int i = _children.Count - 1; i >= 0; i--)
        {
            _children[i].Undo();
        }
    }
}
