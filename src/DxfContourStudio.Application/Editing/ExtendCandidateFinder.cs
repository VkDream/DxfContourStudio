#nullable enable

using System;
using System.Collections.Generic;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Editing;

/// <summary>
/// D13A: finds the unique boundary for extending a selected target. No new
/// geometry algorithm — it evaluates the existing <see cref="TrimExtendEngine"/>
/// against every candidate boundary and collects only the cases that would
/// actually <b>lengthen</b> the target (TrimExtendAction.Extended), on either
/// side (KeepStart / KeepEnd). When more than one boundary qualifies, the
/// caller refuses (ambiguity — never pick one at random).
/// </summary>
public static class ExtendCandidateFinder
{
    /// <summary>
    /// Returns the unique (boundary, side) extension for <paramref name="target"/>,
    /// or null when there is no qualifying boundary or the choice is ambiguous.
    /// </summary>
    public static (IGeometryEntity Boundary, TrimSide Side)? FindUniqueExtension(
        IGeometryEntity target,
        IReadOnlyList<IGeometryEntity> candidates,
        double tolerance)
    {
        (IGeometryEntity Boundary, TrimSide Side)? found = null;

        foreach (IGeometryEntity boundary in candidates)
        {
            foreach (TrimSide side in new[] { TrimSide.KeepStart, TrimSide.KeepEnd })
            {
                TrimExtendResult? attempt = TrimExtendEngine.TrimEnd(target, boundary, side, tolerance, 0);
                if (attempt is { Action: TrimExtendAction.Extended })
                {
                    if (found is not null)
                    {
                        // Second qualifying boundary/side → ambiguous.
                        return null;
                    }

                    found = (boundary, side);
                }
            }
        }

        return found;
    }

    /// <summary>
    /// D15 extend tool: searches only on <paramref name="side"/> (the end the
    /// mouse is near) and returns the qualifying boundary whose extension hit
    /// point is <b>nearest</b> to the free end. The extension must actually
    /// lengthen the target (TrimExtendAction.Extended) and stay within
    /// <paramref name="maxExtendDistance"/>. Refused (null) when
    /// <paramref name="maxExtendDistance"/> is exceeded by every candidate,
    /// when no boundary qualifies, or when two different boundaries hit the
    /// <em>same</em> point (ambiguous — the trim would be identical but the
    /// boundary attribution is not; never pick one at random).
    /// </summary>
    public static (IGeometryEntity Boundary, Point2 HitPoint)? FindNearestExtension(
        IGeometryEntity target,
        IReadOnlyList<IGeometryEntity> candidates,
        TrimSide side,
        double tolerance,
        double maxExtendDistance)
    {
        Point2 freeEnd = side == TrimSide.KeepStart ? target.EndPoint : target.StartPoint;
        var hits = new List<(IGeometryEntity Boundary, Point2 HitPoint, double Length)>();

        foreach (IGeometryEntity boundary in candidates)
        {
            TrimExtendResult? attempt = TrimExtendEngine.TrimEnd(target, boundary, side, tolerance, 0);
            if (attempt is not { Action: TrimExtendAction.Extended } result)
            {
                continue;
            }

            Point2 hit = side == TrimSide.KeepStart ? result.Entity.EndPoint : result.Entity.StartPoint;
            double length = freeEnd.DistanceTo(hit);
            if (length > maxExtendDistance)
            {
                continue;
            }

            hits.Add((boundary, hit, length));
        }

        // Any two boundaries hitting the same point → ambiguous, never guess.
        for (int i = 0; i < hits.Count; i++)
        {
            for (int j = i + 1; j < hits.Count; j++)
            {
                if (hits[i].HitPoint.DistanceTo(hits[j].HitPoint) <= tolerance)
                {
                    return null;
                }
            }
        }

        if (hits.Count == 0)
        {
            return null;
        }

        (IGeometryEntity Boundary, Point2 HitPoint, double Length) nearest = hits[0];
        foreach (var hit in hits)
        {
            if (hit.Length < nearest.Length)
            {
                nearest = hit;
            }
        }

        return (nearest.Boundary, nearest.HitPoint);
    }
}