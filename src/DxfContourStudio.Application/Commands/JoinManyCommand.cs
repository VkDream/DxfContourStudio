#nullable enable

using System;
using System.Collections.Generic;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Commands;

/// <summary>
/// Joins an ordered list of endpoint-adjacent entities into one mixed
/// polyline as a single undoable transaction. Every join step follows the
/// rules of <see cref="JoinEntitiesCommand"/> (endpoint distance within
/// tolerance, unique pair, same layer); the first id in the list survives
/// as the merged entity's id. Undo restores all original entities with ids,
/// order and layers intact — one Ctrl+Z reverses the whole chain.
/// </summary>
public sealed class JoinManyCommand : ICommand
{
    private sealed record Step(
        long PrimaryId,
        long SecondaryId,
        PolylineGeometry Joined,
        IGeometryEntity PrimaryOriginal,
        IGeometryEntity SecondaryOriginal);

    private readonly CadDocument _document;
    private readonly List<Step> _steps = [];
    private readonly int[,] _lastIndexes = new int[2, 2];

    public string Name { get; }

    public JoinManyCommand(CadDocument document, IReadOnlyList<long> ids, GeometryTolerance tolerance, string name = "Join entities")
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        Name = name;

        if (ids is null || ids.Count < 2)
        {
            throw new ArgumentException("JoinManyCommand: at least two entity ids are required.");
        }

        // Simulate the chain eagerly: the constructor fails fast (like
        // JoinEntitiesCommand) when any step is not joinable, so the UI can
        // disable the command instead of producing a broken state.
        IGeometryEntity? current = document.GetEntityById(ids[0]);
        for (int i = 1; i < ids.Count; i++)
        {
            long secondaryId = ids[i];
            IGeometryEntity? secondary = document.GetEntityById(secondaryId);
            if (current is null || secondary is null)
            {
                throw new ArgumentException(
                    $"JoinManyCommand: entity {(current is null ? ids[0] : secondaryId)} not found.");
            }

            var attempt = JoinEngine.TryJoin(current, secondary, ids[0], tolerance);
            if (!attempt.IsValid || attempt.Joined is null)
            {
                throw new ArgumentException(
                    $"JoinManyCommand: cannot join step {i} ({current.Id} + {secondaryId}): {attempt.Reason}.");
            }

            _steps.Add(new Step(
                current.Id,
                secondaryId,
                attempt.Joined,
                current,
                secondary));
            current = attempt.Joined;
        }
    }

    public void Execute()
    {
        for (int i = 0; i < _steps.Count; i++)
        {
            Step step = _steps[i];
            // Refresh indices right before this step executes: earlier steps
            // shrink the list, so construction-time indices are stale.
            _lastIndexes[i, 0] = IndexOf(step.PrimaryId);
            _lastIndexes[i, 1] = IndexOf(step.SecondaryId);
            _document.ReplaceEntity(step.Joined);
            _document.RemoveEntity(step.SecondaryId);
        }
    }

    public void Undo()
    {
        // Reverse execution order. Within one step the primary original goes
        // back first (it occupied the merged entity's slot), then the
        // secondary — this restores the pre-join ordering exactly.
        for (int i = _steps.Count - 1; i >= 0; i--)
        {
            Step step = _steps[i];
            _document.RemoveEntity(step.Joined.Id);
            _document.InsertEntity(_lastIndexes[i, 0], step.PrimaryOriginal);
            _document.InsertEntity(_lastIndexes[i, 1], step.SecondaryOriginal);
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
