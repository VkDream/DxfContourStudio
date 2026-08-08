#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Selection;

/// <summary>
/// Selection set over a document's entities. Selection is kept as ids so it
/// stays valid across a replace/re-import of identical drawings. This is the
/// single source of truth for "what is selected"; the document and the UI both
/// consume it, neither owns a second copy.
///
/// Semantics (matching the CAD conventions the UI implements):
/// - a plain click selects exactly one entity (SelectSingle),
/// - Ctrl+click toggles membership,
/// - clicking empty space or Esc clears,
/// - the most recently interacted entity is the primary (focused) selection.
/// </summary>
public sealed class SelectionModel
{
    private readonly HashSet<long> _ids = [];

    /// <summary>Selected entity ids, in selection order (insertion order).</summary>
    public IReadOnlyCollection<long> Ids => _ids;

    /// <summary>Number of currently selected entities.</summary>
    public int Count => _ids.Count;

    /// <summary>
    /// The primary (focused) selection: the entity the user clicked last.
    /// null when nothing is selected. The UI renders it differently.
    /// </summary>
    public long? PrimaryId { get; private set; }

    /// <summary>Raised whenever the selection content changes.</summary>
    public event Action? SelectionChanged;

    /// <summary>Whether the given id is currently selected.</summary>
    public bool IsSelected(long id) => _ids.Contains(id);

    private void RaiseChanged() => SelectionChanged?.Invoke();

    /// <summary>Clears the whole selection (and the primary id).</summary>
    public void Clear()
    {
        if (_ids.Count == 0 && PrimaryId is null)
        {
            return;
        }

        _ids.Clear();
        PrimaryId = null;
        RaiseChanged();
    }

    /// <summary>
    /// Selects exactly <paramref name="id"/>: replaces the whole selection and
    /// makes it the primary selection. Used by plain clicks.
    /// </summary>
    public void SelectSingle(long id)
    {
        _ids.Clear();
        _ids.Add(id);
        PrimaryId = id;
        RaiseChanged();
    }

    /// <summary>
    /// Adds <paramref name="id"/> to the selection without removing anything.
    /// If it was already selected nothing changes (no duplicate entry).
    /// </summary>
    public void Add(long id)
    {
        if (!_ids.Add(id))
        {
            return;
        }

        PrimaryId = id;
        RaiseChanged();
    }

    /// <summary>Removes <paramref name="id"/> from the selection if present.</summary>
    public void Remove(long id)
    {
        if (!_ids.Remove(id))
        {
            return;
        }

        if (PrimaryId == id)
        {
            PrimaryId = _ids.Count == 0 ? null : _ids.First();
        }

        RaiseChanged();
    }

    /// <summary>Toggles membership of <paramref name="id"/> (Ctrl+click).</summary>
    public bool Toggle(long id)
    {
        bool added = _ids.Add(id);
        if (added)
        {
            PrimaryId = id;
        }
        else
        {
            _ids.Remove(id);
            if (PrimaryId == id)
            {
                PrimaryId = _ids.Count == 0 ? null : _ids.First();
            }
        }

        RaiseChanged();
        return added;
    }

    /// <summary>Replaces the whole selection with the given ids (first id becomes primary).</summary>
    public void ReplaceWith(IEnumerable<long> ids)
    {
        _ids.Clear();
        long? first = null;
        foreach (long id in ids)
        {
            _ids.Add(id);
            first ??= id;
        }

        PrimaryId = first;
        RaiseChanged();
    }

    /// <summary>Selects all of the given ids, replacing the current selection.</summary>
    public void SelectAll(IEnumerable<long> ids) => ReplaceWith(ids);

    /// <summary>
    /// Applies <paramref name="pick"/> (entity ids already filtered by the
    /// calling code) but keeps previously selected ids when the user is
    /// adding to the selection. Used by the UI to implement click-select.
    /// </summary>
    public void ApplyClickPick(long pickedId, bool additive)
    {
        if (additive)
        {
            Toggle(pickedId);
            return;
        }

        SelectSingle(pickedId);
    }

    /// <summary>
    /// Drops every selected id for which <paramref name="stillValid"/> is
    /// false. Used to keep the selection consistent when entities disappear
    /// (delete, undo/redo, layer hide) — no dangling ids are ever kept.
    /// </summary>
    public void Prune(Func<long, bool> stillValid)
    {
        long[] removed = _ids.Where(id => !stillValid(id)).ToArray();
        if (removed.Length == 0)
        {
            return;
        }

        foreach (long id in removed)
        {
            _ids.Remove(id);
        }

        if (PrimaryId is { } p && !_ids.Contains(p))
        {
            PrimaryId = _ids.Count == 0 ? null : _ids.First();
        }

        RaiseChanged();
    }
}
