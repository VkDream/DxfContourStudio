#nullable enable

using System;
using System.Collections.Generic;

namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// Why a section trim could not be planned. Mapped by the UI layer onto
/// localized status messages — never on a crash.
/// </summary>
public enum TrimSectionRefusalReason
{
    /// <summary>The target is neither a Line nor an Arc (Polyline/Circle …).</summary>
    UnsupportedTarget,

    /// <summary>The click is farther than the pick tolerance from the target path.</summary>
    NotOnTarget,

    /// <summary>No boundary crossing splits the target path at all.</summary>
    NoBoundary,

    /// <summary>The removal would cover the whole path (or a sub-tolerance sliver
    /// right at a boundary — i.e. the click sits on a boundary).</summary>
    DegenerateRemoval,

    /// <summary>A non-empty kept piece would be shorter than the minimum piece
    /// length (no zero-length residue may be produced).</summary>
    TinyKeptPiece,
}

/// <summary>
/// A planned "click this section away" trim: everything of the target between
/// <paramref name="StartT"/> and <paramref name="EndT"/> (normalized path
/// parameters, 0 = start, 1 = end) is removed; the rest stays as one or two
/// pieces. <paramref name="RemovedRuns"/> holds the exact geometry of the
/// removed part (used for the hover preview); <paramref name="BoundaryPoints"/>
/// are the boundary crossings that delimit the removed section (markers).
/// Pure math — executing the plan is the command layer's job.
/// </summary>
public sealed record TrimSectionPlan(
    double StartT,
    double EndT,
    Point2 RemoveStart,
    Point2 RemoveEnd,
    IReadOnlyList<Point2> BoundaryPoints,
    IReadOnlyList<IPathSegment> RemovedRuns);

/// <summary>Result of <see cref="TrimSectionEngine.PlanSectionTrim"/>. Valid
/// plans are never null — refusals carry a reason instead.</summary>
public readonly record struct TrimSectionOutcome(bool IsValid, TrimSectionRefusalReason Reason, TrimSectionPlan? Plan)
{
    public static TrimSectionOutcome Refused(TrimSectionRefusalReason reason) => new(false, reason, null);

    public static TrimSectionOutcome Succeeded(TrimSectionPlan plan) => new(true, default, plan);
}

/// <summary>
/// Pure "section trim" planning for the interactive trim tool (D15): given the
/// target path (Line or Arc; Polyline is refused — see
/// <see cref="TrimSectionRefusalReason.UnsupportedTarget"/>), the boundary
/// entities and the click point, it decides which contiguous interval of the
/// target must be removed:
///
/// - every boundary crossing along the target path is a cut point (boundary
///   semantics identical to <see cref="TrimExtendEngine"/>: line → infinite
///   line, arc → full circle, circle → full circle, polyline → each run);
/// - the click picks the interval between the two cuts that surround it (or
///   between a cut and the path end) — that whole interval is removed, so
///   N boundaries split the target into N+1 sections,
/// - a click exactly on top of a cut (within tolerance) removes the section
///   immediately to its LEFT (deterministic, never randomized),
/// - boundary cuts within the pick tolerance of a path end count as endpoint
///   boundaries (they clamp to 0 / 1),
/// - crossings that coincide within tolerance merge into one cut,
/// - the planner guarantees the result never contains a zero-length residue:
///   every non-empty kept piece must be at least
///   <paramref name="minPieceLength"/> long, otherwise the plan is refused.
/// </summary>
public static class TrimSectionEngine
{
    /// <summary>
    /// Plans the removal interval for one click. Null-safe for <c>null</c>
    /// targets/boundaries (treated like a refusal).
    /// </summary>
    public static TrimSectionOutcome PlanSectionTrim(
        IGeometryEntity target,
        IReadOnlyList<IGeometryEntity> boundaries,
        Point2 click,
        double tolerance,
        double minPieceLength)
    {
        if (tolerance <= 0 || minPieceLength < 0)
        {
            return TrimSectionOutcome.Refused(TrimSectionRefusalReason.UnsupportedTarget);
        }

        if (target is not (LineGeometry or ArcGeometry))
        {
            return TrimSectionOutcome.Refused(TrimSectionRefusalReason.UnsupportedTarget);
        }

        if (!PathBreaker.TryProjectParameter(target, click, tolerance, out double clickT, out _))
        {
            return TrimSectionOutcome.Refused(TrimSectionRefusalReason.NotOnTarget);
        }

        if (boundaries is null || boundaries.Count == 0)
        {
            return TrimSectionOutcome.Refused(TrimSectionRefusalReason.NoBoundary);
        }

        double length = target.Length;
        if (length <= 1e-12)
        {
            return TrimSectionOutcome.Refused(TrimSectionRefusalReason.NotOnTarget);
        }

        // Crossing parameters along the path (normalized to [0,1]).
        var crossingParams = new List<double>();
        CollectCrossings(target, boundaries, crossingParams);
        if (crossingParams.Count == 0)
        {
            return TrimSectionOutcome.Refused(TrimSectionRefusalReason.NoBoundary);
        }

        // Tolerance in parameter space (the pick tolerance converted from a
        // distance to a path-parameter band on this particular target).
        double eps = tolerance / length;

        // Clip crossings inside the tolerance band onto the path ends.
        var clipped = new List<double>();
        foreach (double p in crossingParams)
        {
            if (p < -eps || p > 1 + eps)
            {
                continue; // crossing outside the path's reach — not a boundary
            }

            clipped.Add(Math.Clamp(p, 0, 1));
        }

        if (clipped.Count == 0)
        {
            return TrimSectionOutcome.Refused(TrimSectionRefusalReason.NoBoundary);
        }

        // Distinct cuts, sorted; coincident/tolerance-adjacent cuts merge into
        // one so a crossing can never split the path twice ~2ε apart.
        clipped.Sort();
        var cuts = new List<double>();
        foreach (double p in clipped)
        {
            if (cuts.Count == 0 || p - cuts[^1] > eps)
            {
                cuts.Add(p);
            }
        }

        // Sections between consecutive cuts (outer sections delimited by the
        // path ends). Zero-width or sub-tolerance sections are not selectable.
        double sectionEps = Math.Max(eps, 1e-12);
        double? removalStart = null;
        double? removalEnd = null;
        for (int k = 0; k <= cuts.Count; k++)
        {
            double left = k == 0 ? 0 : cuts[k - 1];
            double right = k == cuts.Count ? 1 : cuts[k];
            if (right - left <= sectionEps)
            {
                continue;
            }

            if (clickT >= left && clickT <= right)
            {
                // First match wins — a click exactly on a shared cut belongs
                // to the section on its left.
                removalStart = left;
                removalEnd = right;
                break;
            }
        }

        if (removalStart is null || removalEnd is null)
        {
            return TrimSectionOutcome.Refused(TrimSectionRefusalReason.DegenerateRemoval);
        }

        double startT = removalStart.Value;
        double endT = removalEnd.Value;

        // Removing the whole path is not a trim.
        if ((startT <= 0 && endT >= 1) || (endT - startT) * length <= tolerance)
        {
            return TrimSectionOutcome.Refused(TrimSectionRefusalReason.DegenerateRemoval);
        }

        // Every non-empty kept piece must survive: no zero-length residue.
        double keptLeft = startT * length; // piece [0, startT]
        double keptRight = (1 - endT) * length; // piece [endT, 1]
        bool hasLeft = startT > 0;
        bool hasRight = endT < 1;
        if ((hasLeft && keptLeft < minPieceLength) || (hasRight && keptRight < minPieceLength))
        {
            return TrimSectionOutcome.Refused(TrimSectionRefusalReason.TinyKeptPiece);
        }

        Point2 removeStart = target.PointAtParameter(startT);
        Point2 removeEnd = target.PointAtParameter(endT);

        var boundaryPoints = new List<Point2>(cuts.Count);
        foreach (double cut in cuts)
        {
            boundaryPoints.Add(target.PointAtParameter(Math.Clamp(cut, 0, 1)));
        }

        return TrimSectionOutcome.Succeeded(new TrimSectionPlan(
            startT, endT, removeStart, removeEnd,
            boundaryPoints, RemovedRuns(target, startT, endT, removeStart, removeEnd)));
    }

    /// <summary>
    /// The exact geometry of the removed interval, ready for the overlay
    /// renderer (a straight run for lines, the true arc run for arcs).
    /// </summary>
    private static IReadOnlyList<IPathSegment> RemovedRuns(
        IGeometryEntity target, double startT, double endT, Point2 removeStart, Point2 removeEnd)
    {
        return target switch
        {
            LineGeometry => [new LineSegment(removeStart, removeEnd)],
            ArcGeometry arc => [new ArcSegment(
                arc.Center, arc.Radius,
                arc.StartAngleRadians + startT * arc.SweepRadians,
                (endT - startT) * arc.SweepRadians,
                arc.IsCounterClockwise)],
            _ => [],
        };
    }

    /// <summary>Crossing parameters of the target's full supporting curve with
    /// every boundary's supporting curves, in normalized path parameters.</summary>
    private static void CollectCrossings(
        IGeometryEntity target, IReadOnlyList<IGeometryEntity> boundaries, List<double> into)
    {
        if (target is LineGeometry line)
        {
            var d = new Vector2(line.P1.X - line.P0.X, line.P1.Y - line.P0.Y);
            double len = d.Length;
            if (len <= 1e-12)
            {
                return;
            }

            foreach (IGeometryEntity boundary in boundaries)
            {
                foreach (Point2 p in TrimExtendEngine.InfiniteLineCrossings(line.P0, d, boundary))
                {
                    double s = ((p.X - line.P0.X) * d.X + (p.Y - line.P0.Y) * d.Y) / len;
                    if (double.IsFinite(s))
                    {
                        into.Add(s / len);
                    }
                }
            }

            return;
        }

        if (target is ArcGeometry arc)
        {
            double start = arc.StartAngleRadians;
            bool ccw = arc.IsCounterClockwise;
            double effSweep = Math.Abs(arc.SweepRadians);
            if (effSweep <= 1e-12)
            {
                return;
            }

            foreach (IGeometryEntity boundary in boundaries)
            {
                foreach (Point2 p in TrimExtendEngine.CircleCrossings(arc.Center, arc.Radius, boundary))
                {
                    double ang = Math.Atan2(p.Y - arc.Center.Y, p.X - arc.Center.X);
                    double delta = ccw ? WrapPositive(ang - start) : -WrapPositive(start - ang);
                    into.Add(delta / effSweep);
                }
            }
        }
    }

    /// <summary>Normalizes an angle difference into [0, 2π).</summary>
    private static double WrapPositive(double angle)
    {
        angle %= MathUtil.TwoPi;
        return angle < 0 ? angle + MathUtil.TwoPi : angle;
    }
}