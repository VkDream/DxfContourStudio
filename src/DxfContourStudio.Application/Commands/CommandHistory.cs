#nullable enable

namespace DxfContourStudio.Application.Commands;

/// <summary>
/// A single undoable/redoable mutation performed on the document.
///
/// Commands capture enough state to undo and redo themselves; they are the
/// unit of the undo stack. The UI layer creates command instances and executes
/// them through <see cref="CommandInvoker"/>; business logic stays inside the
/// command so the UI class does not get a business role.
/// </summary>
public interface ICommand
{
    /// <summary>Display name shown in Undo/Redo menus.</summary>
    string Name { get; }

    /// <summary>Applies the mutation and (per contract) registers nothing itself.</summary>
    void Execute();

    /// <summary>Reverses <see cref="Execute"/>.</summary>
    void Undo();
}

/// <summary>
/// Tracks the undo/redo stacks and executes commands on a document.
/// Bound stack depth keeps memory bounded for large drawings.
/// </summary>
public sealed class CommandHistory
{
    private const int MaxDepth = 50;
    private readonly List<ICommand> _undoStack = [];
    private readonly List<ICommand> _redoStack = [];

    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;

    /// <summary>Whether an undo is currently possible.</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>Whether a redo is currently possible.</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Raised whenever the stacks change (execute/undo/redo/clear) so the UI can refresh CanExecute.</summary>
    public event Action? Changed;

    private void RaiseChanged() => Changed?.Invoke();

    /// <summary>Executes <paramref name="command"/> and pushes it onto the undo stack.</summary>
    public void Execute(ICommand command)
    {
        command.Execute();
        _redoStack.Clear();
        _undoStack.Add(command);
        if (_undoStack.Count > MaxDepth)
        {
            _undoStack.RemoveAt(0);
        }

        RaiseChanged();
    }

    /// <summary>Undoes the most recent command; false when nothing to undo.</summary>
    public bool TryUndo()
    {
        if (_undoStack.Count == 0)
        {
            return false;
        }

        var command = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        command.Undo();
        _redoStack.Add(command);
        RaiseChanged();
        return true;
    }

    /// <summary>Redoes the most recently undone command; false when nothing to redo.</summary>
    public bool TryRedo()
    {
        if (_redoStack.Count == 0)
        {
            return false;
        }

        var command = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        command.Execute();
        _undoStack.Add(command);
        RaiseChanged();
        return true;
    }

    /// <summary>Returns the command that the next undo would reverse (without undoing it), or null.</summary>
    public ICommand? PeekUndo() => _undoStack.Count > 0 ? _undoStack[^1] : null;

    /// <summary>Returns the command that the next redo would re-apply (without redoing it), or null.</summary>
    public ICommand? PeekRedo() => _redoStack.Count > 0 ? _redoStack[^1] : null;

    /// <summary>Clears both stacks (used when a document is replaced).</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        RaiseChanged();
    }
}