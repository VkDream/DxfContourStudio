#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace DxfContourStudio.Core.Geometry;

/// <summary>Why a join between two entities is not possible.</summary>
public enum JoinRejectReason
{
    /// <summary>Join is possible (result valid).</summary>
    None,

    /// <summary>The endpoints are not within JoinTolerance of each other.</summary>
    NotConnected,

    /// <summary>More than one endpoint pair matches — no unique connection
    /// (e.g. two collinear overlapping lines). The user must disambiguate.</summary>
    Ambiguous,

    /// <summary>The two entities belong to different layers; join is refused
    /// by default (they may be re-layered first).</summary>
    DifferentLayers,

    /// <summary>An input entity is empty or of an unsupported kind.</summary>
    Unsupported,
}

/// <summary>
/// Pure join planning: given two entities, decide whether their endpoints can
/// be joined into a single <see cref="PolylineGeometry"/> path and how to
/// orient the two run lists. No document mutation here — the command layer
/// executes/undoes the plan.
///
/// Semantics (documented in docs/ADR/ADR-011-Join-Semantics.md):
/// - Only endpoint-adjacent entities join (distance &lt;= JoinTolerance).
/// - Exactly one matching endpoint pair is allowed; a second match means the
///   connection is ambiguous and the join is refused (no random guess).
/// - Layers must match; cross-layer merging is refused.
/// - The result is one mixed polyline (LineSegment/ArcSegment runs), never a
///   mere "touching pair". Bulges survive the merge.
/// - Id policy (ADR-013): the primary entity's id is kept.
/// </summary>
public static class JoinEngine
{
    /// <summary>
    /// Builds the join plan for the two entities. Returns the merged polyline
    /// geometry (with <paramref name="resultId"/> and the primary layer) when
    /// the join is valid.
    /// </summary>
    public static JoinAttempt TryJoin(
        IGeometryEntity primary, IGeometryEntity secondary,
        long resultId, GeometryTolerance tolerance)
    {
        if (primary.LayerName != secondary.LayerName)
        {
            return JoinAttempt.Rejected(JoinRejectReason.DifferentLayers);
        }

        var runsA = RunsOf(primary);
        var runsB = RunsOf(secondary);
        if (runsA.Count == 0 || runsB.Count == 0)
        {
            return JoinAttempt.Rejected(JoinRejectReason.Unsupported);
        }

        Point2 a0 = runsA[0].StartPoint;
        Point2 a1 = runsA[^1].EndPoint;
        Point2 b0 = runsB[0].StartPoint;
        Point2 b1 = runsB[^1].EndPoint;
        double tol = tolerance.JoinTolerance <= 0 ? 0.05 : tolerance.JoinTolerance;

        // A unique matching endpoint pair is required.
        bool matchA1B0 = Near(a1, b0, tol);
        bool matchA1B1 = Near(a1, b1, tol);
        bool matchA0B0 = Near(a0, b0, tol);
        bool matchA0B1 = Near(a0, b1, tol);
        int matches = (matchA1B0 ? 1 : 0) + (matchA1B1 ? 1 : 0) + (matchA0B0 ? 1 : 0) + (matchA0B1 ? 1 : 0);
        if (matches > 1)
        {
            return JoinAttempt.Rejected(JoinRejectReason.Ambiguous);
        }

        if (matches == 0)
        {
            return JoinAttempt.Rejected(JoinRejectReason.NotConnected);
        }

        bool reverseA;
        bool reverseB;
        if (matchA1B0)
        {
            reverseA = false;
            reverseB = false;
        }
        else if (matchA1B1)
        {
            reverseA = false;
            reverseB = true;
        }
        else if (matchA0B0)
        {
            reverseA = true;
            reverseB = false;
        }
        else
        {
            reverseA = true;
            reverseB = true;
        }

        var merged = new List<IPathSegment>(runsA.Count + runsB.Count);
        if (reverseA)
        {
            merged.AddRange(runsA.Reverse().Select(Reverse));
        }
        else
        {
            merged.AddRange(runsA);
        }

        if (reverseB)
        {
            merged.AddRange(runsB.Reverse().Select(Reverse));
        }
        else
        {
            merged.AddRange(runsB);
        }

        var joined = new PolylineGeometry(
            resultId, primary.LayerName, merged, isClosed: false, isVisible: primary.IsVisible);
        return JoinAttempt.Success(joined);
    }

    /// <summary>Whether a join of the pair matches the tolerance distance at all.</summary>
    public static bool AreJoinable(IGeometryEntity a, IGeometryEntity b, GeometryTolerance tolerance)
    {
        if (a.LayerName != b.LayerName)
        {
            return false;
        }

        var runsA = RunsOf(a);
        var runsB = RunsOf(b);
        if (runsA.Count == 0 || runsB.Count == 0)
        {
            return false;
        }

        double tol = tolerance.JoinTolerance <= 0 ? 0.05 : tolerance.JoinTolerance;
        Point2 a0 = runsA[0].StartPoint;
        Point2 a1 = runsA[^1].EndPoint;
        Point2 b0 = runsB[0].StartPoint;
        Point2 b1 = runsB[^1].EndPoint;
        return Near(a0, b0, tol) || Near(a0, b1, tol) || Near(a1, b0, tol) || Near(a1, b1, tol);
    }

    /// <summary>
    /// Reverses a run: the reversed arc keeps its center/radius and covers the
    /// same geometric span in the opposite direction (sweep sign + ccw flag
    /// flip, so the (center, radius) pair stays canonical).
    /// </summary>
    public static IPathSegment Reverse(IPathSegment segment)
    {
        return segment switch
        {
            LineSegment l => new LineSegment(l.EndPoint, l.StartPoint),
            ArcSegment a => new ArcSegment(
                a.Center, a.Radius,
                a.StartAngleRadians + a.SweepRadians,
                -a.SweepRadians,
                !a.IsCounterClockwise),
            _ => segment,
        };
    }

    internal static IReadOnlyList<IPathSegment> RunsOf(IGeometryEntity entity) => entity switch
    {
        LineGeometry l => [new LineSegment(l.StartPoint, l.EndPoint)],
        ArcGeometry arc => [new ArcSegment(arc.Center, arc.Radius, arc.StartAngleRadians, arc.SweepRadians, arc.IsCounterClockwise)],
        CircleGeometry c => [new ArcSegment(c.Center, c.Radius, 0, MathUtil.TwoPi, true)],
        PolylineGeometry p when p.Segments.Count > 0 => p.Segments,
        _ => [],
    };

    private static bool Near(Point2 a, Point2 b, double tol) => a.DistanceTo(b) <= tol;
}

/// <summary>Result of a join evaluation.</summary>
public readonly record struct JoinAttempt(bool IsValid, PolylineGeometry? Joined, JoinRejectReason Reason)
{
    public static JoinAttempt Success(PolylineGeometry joined) => new(true, joined, JoinRejectReason.None);

    public static JoinAttempt Rejected(JoinRejectReason reason) => new(false, null, reason);
}