#nullable enable

using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Contours;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Core.Topology;

namespace DxfContourStudio.Application.Commands;

/// <summary>
/// Closes a small gap: both gap ends are moved to their midpoint (the
/// strategy fixed by ADR-007 "Gap repair strategy"). Only gaps that were
/// flagged <see cref="GapDiagnostic.CanAutoRepair"/> may be repaired.
///
/// Undo restores the original entities (the gap reappears); redo re-applies
/// the repair. The document entity positions stay stable because the
/// replacement is done in place via <see cref="CadDocument.ReplaceEntity"/>.
/// </summary>
public sealed class RepairGapCommand : ICommand
{
    private readonly CadDocument _document;

    // For every affected entity: (id, original instance, repaired instance).
    private readonly IReadOnlyList<(long Id, IGeometryEntity Original, IGeometryEntity Repaired)> _replacements;

    /// <summary>Command label shown in undo history tooling.</summary>
    public string Name { get; }

    public RepairGapCommand(CadDocument document, GapDiagnostic gap, string name = "Repair gap")
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        if (gap is null || !gap.CanAutoRepair)
        {
            throw new ArgumentException("Only auto-repairable gaps can be repaired.", nameof(gap));
        }

        Name = name;

        Point2 midpoint = new(
            (gap.PositionA.X + gap.PositionB.X) * 0.5,
            (gap.PositionA.Y + gap.PositionB.Y) * 0.5);

        var replacements = new List<(long, IGeometryEntity, IGeometryEntity)>();
        var order = new[] { (gap.EntityIdA, gap.SegmentIndexA, gap.IsStartA), (gap.EntityIdB, gap.SegmentIndexB, gap.IsStartB) };

        if (order[0].Item1 == order[1].Item1)
        {
            // Both ends live on the same entity: apply the first move, then
            // the second on top of the intermediate result.
            IGeometryEntity? current = _document.GetEntityById(order[0].Item1);
            if (current is null)
            {
                throw new InvalidOperationException("Gap references an entity that no longer exists.");
            }

            IGeometryEntity intermediate = EndpointRepair.MoveEndpoint(current, order[0].Item2, order[0].Item3, midpoint);
            IGeometryEntity final = EndpointRepair.MoveEndpoint(intermediate, order[1].Item2, order[1].Item3, midpoint);
            replacements.Add((current.Id, current, final));
        }
        else
        {
            foreach ((long id, int segmentIndex, bool isStart) in order)
            {
                IGeometryEntity? current = _document.GetEntityById(id);
                if (current is null)
                {
                    throw new InvalidOperationException("Gap references an entity that no longer exists.");
                }

                IGeometryEntity repaired = EndpointRepair.MoveEndpoint(current, segmentIndex, isStart, midpoint);
                replacements.Add((id, current, repaired));
            }
        }

        _replacements = replacements;
    }

    /// <inheritdoc />
    public void Execute()
    {
        foreach ((long _, IGeometryEntity _, IGeometryEntity repaired) in _replacements)
        {
            _document.ReplaceEntity(repaired);
        }
    }

    /// <inheritdoc />
    public void Undo()
    {
        foreach ((long _, IGeometryEntity original, IGeometryEntity _) in _replacements)
        {
            _document.ReplaceEntity(original);
        }
    }
}
