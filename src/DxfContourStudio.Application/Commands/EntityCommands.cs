#nullable enable

using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Commands;

/// <summary>
/// Inserts a new entity into the document. Generic enough for drawing
/// commands and test fixtures. Entity ids are owned by the caller (usually
/// the document's next-id counter).
/// </summary>
public sealed class AddEntityCommand : ICommand
{
    private readonly CadDocument _document;
    private readonly IGeometryEntity _entity;

    public string Name { get; }

    public AddEntityCommand(CadDocument document, IGeometryEntity entity, string name = "Add entity")
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
        Name = name;
    }

    public void Execute() => _document.AddEntity(_entity);

    public void Undo() => _document.RemoveEntity(_entity.Id);
}

/// <summary>
/// Removes one or more entities from the document. Captures the removed
/// entities so Undo re-inserts them with their original ids, layers and
/// geometry intact.
/// </summary>
public sealed class DeleteEntitiesCommand : ICommand
{
    private readonly CadDocument _document;
    private readonly IReadOnlyList<long> _ids;
    private readonly List<(int Index, IGeometryEntity Entity)> _removed = [];

    public string Name { get; }

    public DeleteEntitiesCommand(CadDocument document, IEnumerable<long> ids, string name = "Delete")
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _ids = ids.Distinct().ToList();
        Name = _ids.Count switch
        {
            0 => name,
            1 => name,
            _ => $"{name} {_ids.Count} entities",
        };
    }

    public void Execute()
    {
        _removed.Clear();
        var entities = _document.Entities;
        foreach (long id in _ids)
        {
            int idx = 0;
            IGeometryEntity? found = null;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Id == id)
                {
                    idx = i;
                    found = entities[i];
                    break;
                }
            }

            if (found is not null)
            {
                _removed.Add((idx, found));
            }
        }

        // Remove highest index first so earlier indexes stay valid.
        foreach ((int index, IGeometryEntity entity) in _removed.OrderByDescending(r => r.Index))
        {
            _document.RemoveEntity(entity.Id);
        }
    }

    public void Undo()
    {
        // Re-insert at their original positions, lowest first, so relative
        // ordering and ids are preserved exactly.
        foreach ((int index, IGeometryEntity entity) in _removed.OrderBy(r => r.Index))
        {
            _document.InsertEntity(index, entity);
        }
    }
}

/// <summary>
/// Moves one or more entities by a world-space delta. One drag gesture is
/// exactly one command (the UI accumulates the pixel delta and commits on
/// mouse-up; it never pushes a per-move command).
/// </summary>
public sealed class MoveEntitiesCommand : ICommand
{
    private readonly CadDocument _document;
    private readonly IReadOnlyList<long> _ids;
    private readonly Vector2 _delta;
    private readonly List<IGeometryEntity> _originals = [];

    public string Name { get; }

    public MoveEntitiesCommand(CadDocument document, IEnumerable<long> ids, Vector2 delta, string name = "Move")
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _ids = ids.Distinct().ToList();
        _delta = delta;
        Name = name;
    }

    public void Execute()
    {
        if (_originals.Count == 0)
        {
            foreach (long id in _ids)
            {
                IGeometryEntity? entity = _document.GetEntityById(id);
                if (entity is not null)
                {
                    _originals.Add(entity.Clone());
                }
            }
        }

        ApplyDelta(_delta);
    }

    public void Undo()
    {
        // Restore the exact pre-move snapshots (not an inverse transform, so
        // floating point cannot drift on repeated undo/redo cycles).
        foreach (IGeometryEntity original in _originals)
        {
            _document.ReplaceEntity(original);
        }
    }

    private void ApplyDelta(Vector2 delta)
    {
        var transform = Transform2.CreateTranslation(delta);
        foreach (IGeometryEntity original in _originals)
        {
            _document.ReplaceEntity(original.Transformed(transform));
        }
    }
}