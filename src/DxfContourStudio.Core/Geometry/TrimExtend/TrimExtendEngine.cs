#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace DxfContourStudio.Core.Geometry;

/// <summary>Which end of the primary path is kept during trim/extend.</summary>
public enum TrimSide
{
    /// <summary>Keep the start point; adjust the end.</summary>
    KeepStart,

    /// <summary>Keep the end point; adjust the start.</summary>
    KeepEnd,
}

/// <summary>What the trim/extend pass actually did.</summary>
public enum TrimExtendAction
{
    /// <summary>The adjusted end already touches the boundary within tolerance.</summary>
    Unchanged,

    /// <summary>The path was cut back to the boundary intersection.</summary>
    Trimmed,

    /// <summary>The path was lengthened up to the boundary intersection.</summary>
    Extended,
}

/// <summary>Result of one trim/extend operation.</summary>
public readonly record struct TrimExtendResult(IGeometryEntity Entity, TrimExtendAction Action);

/// <summary>
/// Pure trim/extend geometry (docs/ADR/ADR-013-Trim-Extend-Semantics.md):
/// one end of a path entity (Line, Arc, or the end run of a Polyline) is
/// moved to its intersection with a boundary entity (Line, Arc or Circle).
/// The kept end never moves.
///
/// Rules:
/// - Line: trimmed to the nearest crossing inside the segment, or extended
///   along its direction to the infinite-line crossing.
/// - Arc: trimmed/extended along its circle to the boundary crossing closest
///   to the adjusted end; extending is refused when the sweep would reach or
///   exceed 2π.
/// - Polyline: only the run that touches the adjusted end may change. A
///   crossing on any interior run (or none at all) yields null.
/// - Boundary semantics: line → infinite line; arc → its FULL circle (the
///   arc's own span is not a trimming gate); circle → the circle.
/// - The adjusted end already on the boundary (within tolerance) → Unchanged.
/// </summary>
public static class TrimExtendEngine
{
    public static TrimExtendResult? TrimEnd(IGeometryEntity primary, IGeometryEntity boundary, TrimSide side, double tolerance, long freshIdForSplits)
    {
        if (tolerance <= 0 || primary is null || boundary is null)
        {
            return null;
        }

        return primary switch
        {
            LineGeometry line => TrimLine(line, boundary, side, tolerance),
            ArcGeometry arc => TrimArc(arc, boundary, side, tolerance),
            PolylineGeometry poly => TrimPolyline(poly, boundary, side, tolerance),
            _ => null,
        };
    }

    // ---------------- Line ----------------

    private static TrimExtendResult? TrimLine(LineGeometry line, IGeometryEntity boundary, TrimSide side, double tolerance)
    {
        Vector2 d = new(line.P1.X - line.P0.X, line.P1.Y - line.P0.Y);
        double len = d.Length;
        if (len <= 1e-12)
        {
            return null;
        }

        // Crossing parameters along the line (s = arc length from P0).
        var cands = new List<(double s, Point2 p)>();
        foreach (Point2 p in InfiniteLineCrossings(line.P0, d, boundary))
        {
            double s = ((p.X - line.P0.X) * d.X + (p.Y - line.P0.Y) * d.Y) / len;
            if (double.IsFinite(s))
            {
                cands.Add((s, p));
            }
        }

        if (cands.Count == 0)
        {
            return null;
        }

        bool keepStart = side == TrimSide.KeepStart;
        double freeS = keepStart ? len : 0;

        // The relevant crossing is the one nearest to the free end inside the
        // travel reach (start→∞ for KeepStart, end→-∞ for KeepEnd).
        double reach = keepStart ? double.PositiveInfinity : double.NegativeInfinity;
        (double s, Point2 p) best = default;
        double bestDelta = double.PositiveInfinity;
        foreach (var c in cands)
        {
            bool insideReach = keepStart ? c.s >= -tolerance : c.s <= len + tolerance;
            if (!insideReach)
            {
                continue;
            }

            double delta = c.s - freeS;
            if (Math.Abs(delta) < Math.Abs(bestDelta))
            {
                best = c;
                bestDelta = delta;
            }
        }

        if (double.IsPositiveInfinity(bestDelta))
        {
            return null;
        }

        if (Math.Abs(bestDelta) <= tolerance)
        {
            return new TrimExtendResult(line, TrimExtendAction.Unchanged);
        }

        bool isTrim = keepStart ? best.s < len : best.s > 0;
        var result = new LineGeometry(line.Id, line.LayerName,
            keepStart ? line.P0 : best.p, keepStart ? best.p : line.P1, line.IsVisible);
        return new TrimExtendResult(result, isTrim ? TrimExtendAction.Trimmed : TrimExtendAction.Extended);
    }

    // ---------------- Arc ----------------

    private static TrimExtendResult? TrimArc(ArcGeometry arc, IGeometryEntity boundary, TrimSide side, double tolerance)
    {
        double start = arc.StartAngleRadians;
        double sweep = arc.SweepRadians;
        bool ccw = arc.IsCounterClockwise;
        double effSweep = Math.Abs(sweep);

        // Crossings of the full circle, parameterized along the path
        // direction: delta in (-2π, 2π], 0 = start point, sweep = end point.
        var crossings = new List<(double delta, Point2 p)>();
        foreach (Point2 p in CircleCrossings(arc.Center, arc.Radius, boundary))
        {
            double ang = Math.Atan2(p.Y - arc.Center.Y, p.X - arc.Center.X);
            double delta = ccw ? WrapPositive(ang - start) : -WrapPositive(start - ang);
            crossings.Add((delta, p));
        }

        if (crossings.Count == 0)
        {
            return null;
        }

        bool keepStart = side == TrimSide.KeepStart;
        double freeDelta = keepStart ? effSweep : 0;

        // First crossing in travel direction: for KeepStart the arc runs from
        // delta 0 to effSweep and beyond; for KeepEnd it runs from effSweep
        // down through 0 and further negative.
        (double delta, Point2 p) best = default;
        double bestDelta = double.PositiveInfinity;
        foreach (var c in crossings)
        {
            bool insideReach = keepStart ? c.delta >= -tolerance : c.delta <= effSweep + tolerance;
            if (!insideReach)
            {
                continue;
            }

            double d = c.delta - freeDelta;
            if (Math.Abs(d) < Math.Abs(bestDelta))
            {
                best = c;
                bestDelta = d;
            }
        }

        if (double.IsPositiveInfinity(bestDelta))
        {
            return null;
        }

        if (Math.Abs(bestDelta) <= tolerance)
        {
            return new TrimExtendResult(arc, TrimExtendAction.Unchanged);
        }

        bool isTrim = keepStart ? best.delta < effSweep : best.delta > 0;
        double newSweep;
        double newStart;
        if (keepStart)
        {
            newStart = start;
            newSweep = ccw ? best.delta : -best.delta;
        }
        else
        {
            newStart = start + (ccw ? best.delta : -best.delta);
            newSweep = ccw ? sweep - best.delta : sweep + best.delta;
        }

        if (Math.Abs(newSweep) >= MathUtil.TwoPi)
        {
            return null; // extending would wrap a full turn
        }

        try
        {
            var result = new ArcGeometry(arc.Id, arc.LayerName, arc.Center, arc.Radius,
                newStart, newSweep, ccw, arc.IsVisible);
            return new TrimExtendResult(result, isTrim ? TrimExtendAction.Trimmed : TrimExtendAction.Extended);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    // ---------------- Polyline ----------------

    private static TrimExtendResult? TrimPolyline(PolylineGeometry poly, IGeometryEntity boundary, TrimSide side, double tolerance)
    {
        var runs = poly.Segments;
        if (runs.Count == 0)
        {
            return null;
        }

        // KeepStart adjusts the polyline's END (its last run); KeepEnd
        // adjusts the START (its first run).
        bool keepStart = side == TrimSide.KeepStart;
        int runIndex = keepStart ? runs.Count - 1 : 0;
        var run = runs[runIndex];

        TrimExtendResult? runResult = run switch
        {
            LineSegment l => TrimLine(new LineGeometry(0, poly.LayerName, l.StartPoint, l.EndPoint), boundary, side, tolerance),
            ArcSegment a => TrimArc(new ArcGeometry(0, poly.LayerName, a.Center, a.Radius, a.StartAngleRadians, a.SweepRadians, a.IsCounterClockwise), boundary, side, tolerance),
            _ => null,
        };

        if (runResult is null || runResult.Value.Entity is not (LineGeometry or ArcGeometry))
        {
            return null;
        }

        TrimExtendResult value = runResult.Value;
        if (value.Action == TrimExtendAction.Unchanged)
        {
            return new TrimExtendResult(poly, TrimExtendAction.Unchanged);
        }

        IPathSegment newRun = value.Entity switch
        {
            LineGeometry l => new LineSegment(l.P0, l.P1),
            ArcGeometry a => new ArcSegment(a.Center, a.Radius, a.StartAngleRadians, a.SweepRadians, a.IsCounterClockwise),
            _ => run,
        };

        var newRuns = new List<IPathSegment>(runs);
        newRuns[runIndex] = newRun;
        var resultPoly = new PolylineGeometry(poly.Id, poly.LayerName, newRuns, poly.IsClosed, poly.IsVisible);
        return new TrimExtendResult(resultPoly, value.Action);
    }

    // ---------------- crossing helpers ----------------

    /// <summary>
    /// Crossings of an infinite line (a, d) with the boundary's infinite
    /// extensions. Internal so <see cref="TrimSectionEngine"/> reuses the
    /// exact same boundary semantics (line → infinite line, arc → full
    /// circle, circle → full circle, polyline → each run).
    /// </summary>
    internal static List<Point2> InfiniteLineCrossings(Point2 a, Vector2 d, IGeometryEntity boundary)
    {
        var result = new List<Point2>();
        foreach (var run in RunsOf(boundary))
        {
            switch (run)
            {
                case LineSegment l:
                    AddRange(result, LineLineCross(a, d, l.StartPoint, new Vector2(l.EndPoint.X - l.StartPoint.X, l.EndPoint.Y - l.StartPoint.Y)));
                    break;

                case ArcSegment arcSeg:
                    AddRange(result, LineCircleCross(a, d, arcSeg.Center, arcSeg.Radius));
                    break;
            }
        }

        return result;
    }

    /// <summary>Crossings of a full circle (c, r) with the boundary's extensions.</summary>
    internal static List<Point2> CircleCrossings(Point2 c, double r, IGeometryEntity boundary)
    {
        var result = new List<Point2>();
        foreach (var run in RunsOf(boundary))
        {
            switch (run)
            {
                case LineSegment l:
                    AddRange(result, LineCircleCross(l.StartPoint, new Vector2(l.EndPoint.X - l.StartPoint.X, l.EndPoint.Y - l.StartPoint.Y), c, r));
                    break;

                case ArcSegment aSeg:
                    AddRange(result, CircleCircleCross(c, r, aSeg.Center, aSeg.Radius));
                    break;
            }
        }

        return result;
    }

    /// <summary>Intersection of two infinite lines; at most one point.</summary>
    internal static List<Point2> LineLineCross(Point2 a0, Vector2 d0, Point2 a1, Vector2 d1)
    {
        double det = d0.X * d1.Y - d0.Y * d1.X;
        if (Math.Abs(det) <= 1e-12)
        {
            return []; // parallel / collinear
        }

        double t = ((a1.X - a0.X) * d1.Y - (a1.Y - a0.Y) * d1.X) / det;
        return [new Point2(a0.X + t * d0.X, a0.Y + t * d0.Y)];
    }

    /// <summary>Intersections of an infinite line and a circle.</summary>
    internal static List<Point2> LineCircleCross(Point2 a, Vector2 d, Point2 c, double r)
    {
        double fx = a.X - c.X, fy = a.Y - c.Y;
        double A = d.X * d.X + d.Y * d.Y;
        if (A <= 1e-18)
        {
            return [];
        }

        double B = 2 * (fx * d.X + fy * d.Y);
        double C = fx * fx + fy * fy - r * r;
        double disc = B * B - 4 * A * C;
        if (disc < 0)
        {
            return [];
        }

        double sq = Math.Sqrt(disc);
        if (sq <= 1e-12)
        {
            double t = -B / (2 * A);
            return [new Point2(a.X + t * d.X, a.Y + t * d.Y)];
        }

        double t1 = (-B - sq) / (2 * A);
        double t2 = (-B + sq) / (2 * A);
        return
        [
            new Point2(a.X + t1 * d.X, a.Y + t1 * d.Y),
            new Point2(a.X + t2 * d.X, a.Y + t2 * d.Y),
        ];
    }

    /// <summary>Intersections of two circles.</summary>
    internal static List<Point2> CircleCircleCross(Point2 c0, double r0, Point2 c1, double r1)
    {
        double dx = c1.X - c0.X;
        double dy = c1.Y - c0.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist <= 1e-12)
        {
            return []; // concentric — degenerate
        }

        double a = (dist * dist + r0 * r0 - r1 * r1) / (2 * dist);
        double hSq = r0 * r0 - a * a;
        if (hSq < 0)
        {
            return [];
        }

        double h = Math.Sqrt(hSq);
        double mx = c0.X + a * dx / dist;
        double my = c0.Y + a * dy / dist;
        double ux = -dy / dist, uy = dx / dist;
        if (hSq <= 1e-18)
        {
            return [new Point2(mx, my)];
        }

        return
        [
            new Point2(mx + h * ux, my + h * uy),
            new Point2(mx - h * ux, my - h * uy),
        ];
    }

    internal static void AddRange(List<Point2> result, IEnumerable<Point2> points)
    {
        foreach (var p in points)
        {
            result.Add(p);
        }
    }

    private static IEnumerable<IPathSegment> RunsOf(IGeometryEntity entity) => entity switch
    {
        LineGeometry l => [new LineSegment(l.P0, l.P1)],
        ArcGeometry a => [new ArcSegment(a.Center, a.Radius, a.StartAngleRadians, a.SweepRadians, a.IsCounterClockwise)],
        CircleGeometry c => [new ArcSegment(c.Center, c.Radius, 0, MathUtil.TwoPi, true)],
        PolylineGeometry p => p.Segments,
        _ => [],
    };

    private static double WrapPositive(double angle)
    {
        angle %= MathUtil.TwoPi;
        return angle < 0 ? angle + MathUtil.TwoPi : angle;
    }
}