#nullable enable

using System;
using System.Collections.Generic;

namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// IntersectionEngine 2.0: curve-to-curve intersection math beyond the legacy
/// line-segment rule. All results use the semantic model
/// <see cref="CurveIntersectionResult"/> so that callers (Trim / Extend /
/// Break / self-intersection) can distinguish a crossing from a tangency from
/// an overlap — and refuse the degenerate pieces.
///
/// Supported pairs (per milestone D1): Line-Line (legacy math preserved,
/// mapped onto the new kind model), Line-Arc / Arc-Line, Arc-Arc, Line-Circle,
/// Circle-Line, Circle-Circle, Circle-Arc, Arc-Circle — both on entity level
/// (Line/Arc/Circle geometries) and run level (LineSegment / ArcSegment).
///
/// Polyline inputs are decomposed into their runs
/// (<see cref="LineSegment"/>/<see cref="ArcSegment"/>) by the callers;
/// <see cref="CollectBoundaryPoints"/> performs that split for them.
///
/// Numeric policy: a single <see cref="GeometryTolerance"/> instance drives
/// every epsilon in this file (intersection equality, tangency distance,
/// parameter interval padding). No magic literals.
/// </summary>
public static partial class IntersectionEngine
{
    // ------------------------------------------------------------------
    // Entity-level overloads (Line / Arc / Circle)
    // ------------------------------------------------------------------

    public static CurveIntersectionResult Intersect(LineGeometry line, LineGeometry other, GeometryTolerance tolerance) =>
        LineLine(line.StartPoint, line.EndPoint, other.StartPoint, other.EndPoint, tolerance);

    public static CurveIntersectionResult Intersect(LineGeometry line, ArcGeometry arc, GeometryTolerance tolerance) =>
        LineArc(line.StartPoint, line.EndPoint, arc.Center, arc.Radius, arc.StartAngleRadians, arc.SweepRadians, arc.IsCounterClockwise, tolerance);

    public static CurveIntersectionResult Intersect(ArcGeometry arc, LineGeometry line, GeometryTolerance tolerance) =>
        LineArc(line.StartPoint, line.EndPoint, arc.Center, arc.Radius, arc.StartAngleRadians, arc.SweepRadians, arc.IsCounterClockwise, tolerance);

    public static CurveIntersectionResult Intersect(ArcGeometry a, ArcGeometry b, GeometryTolerance tolerance) =>
        ArcArc(a.Center, a.Radius, a.StartAngleRadians, a.SweepRadians, a.IsCounterClockwise,
            b.Center, b.Radius, b.StartAngleRadians, b.SweepRadians, b.IsCounterClockwise, tolerance);

    public static CurveIntersectionResult Intersect(LineGeometry line, CircleGeometry circle, GeometryTolerance tolerance) =>
        LineCircle(line.StartPoint, line.EndPoint, circle.Center, circle.Radius, tolerance);

    public static CurveIntersectionResult Intersect(CircleGeometry circle, LineGeometry line, GeometryTolerance tolerance) =>
        LineCircle(line.StartPoint, line.EndPoint, circle.Center, circle.Radius, tolerance);

    public static CurveIntersectionResult Intersect(CircleGeometry a, CircleGeometry b, GeometryTolerance tolerance) =>
        CircleCircle(a.Center, a.Radius, b.Center, b.Radius, tolerance);

    public static CurveIntersectionResult Intersect(CircleGeometry circle, ArcGeometry arc, GeometryTolerance tolerance) =>
        CircleArc(circle, arc, tolerance);

    public static CurveIntersectionResult Intersect(ArcGeometry arc, CircleGeometry circle, GeometryTolerance tolerance) =>
        CircleArc(circle, arc, tolerance);

    /// <summary>
    /// Universal dispatch for the elementary element types (Line / Arc /
    /// Circle). Polyline is rejected — decompose it into runs first, see
    /// <see cref="CollectBoundaryPoints"/>.
    /// </summary>
    public static CurveIntersectionResult IntersectCurves(IGeometryEntity a, IGeometryEntity b, GeometryTolerance tolerance)
    {
        return (a, b) switch
        {
            (LineGeometry la, LineGeometry lb) => Intersect(la, lb, tolerance),
            (LineGeometry la, ArcGeometry ab) => Intersect(la, ab, tolerance),
            (ArcGeometry aa, LineGeometry lb) => Intersect(aa, lb, tolerance),
            (ArcGeometry aa, ArcGeometry ab) => Intersect(aa, ab, tolerance),
            (LineGeometry la, CircleGeometry cb) => Intersect(la, cb, tolerance),
            (CircleGeometry ca, LineGeometry lb) => Intersect(ca, lb, tolerance),
            (CircleGeometry ca, CircleGeometry cb) => Intersect(ca, cb, tolerance),
            (CircleGeometry ca, ArcGeometry ab) => Intersect(ca, ab, tolerance),
            (ArcGeometry aa, CircleGeometry cb) => Intersect(aa, cb, tolerance),
            _ => throw new ArgumentException(
                $"IntersectCurves does not support a ({a.GetType().Name}, {b.GetType().Name}) pair; " +
                "pass a LineGeometry / ArcGeometry / CircleGeometry or split polylines into runs.")
        };
    }

    // ------------------------------------------------------------------
    // Segment-level overloads (a polyline's runs)
    // ------------------------------------------------------------------

    public static CurveIntersectionResult IntersectSegments(LineSegment a, LineSegment b, GeometryTolerance tolerance)
    {
        var legacy = Intersect(a.StartPoint, a.EndPoint, b.StartPoint, b.EndPoint, tolerance.IntersectionTolerance);
        return legacy.Kind switch
        {
            LineIntersectionKind.None => CurveIntersectionResult.None,
            LineIntersectionKind.Point => CurveIntersectionResult.At(legacy.Point),
            LineIntersectionKind.Parallel => CurveIntersectionResult.Parallel(),
            // The legacy engine collapses a collinear touching pair to a single
            // point; anything wider is a genuine interval overlap.
            LineIntersectionKind.CollinearOverlap => legacy.Point.DistanceTo(legacy.EndPoint) <= tolerance.IntersectionTolerance
                ? CurveIntersectionResult.At(legacy.Point)
                : CurveIntersectionResult.Overlap(legacy.Point, legacy.EndPoint),
            _ => CurveIntersectionResult.None,
        };
    }

    public static CurveIntersectionResult IntersectSegments(LineSegment a, ArcSegment b, GeometryTolerance tolerance) =>
        LineArc(a.StartPoint, a.EndPoint, b.Center, b.Radius, b.StartAngleRadians, b.SweepRadians, b.IsCounterClockwise, tolerance);

    public static CurveIntersectionResult IntersectSegments(ArcSegment a, LineSegment b, GeometryTolerance tolerance) =>
        LineArc(b.StartPoint, b.EndPoint, a.Center, a.Radius, a.StartAngleRadians, a.SweepRadians, a.IsCounterClockwise, tolerance);

    public static CurveIntersectionResult IntersectSegments(ArcSegment a, ArcSegment b, GeometryTolerance tolerance) =>
        ArcArc(a.Center, a.Radius, a.StartAngleRadians, a.SweepRadians, a.IsCounterClockwise,
            b.Center, b.Radius, b.StartAngleRadians, b.SweepRadians, b.IsCounterClockwise, tolerance);

    /// <summary>
    /// Intersects any two polyline runs (LineSegment × LineSegment /
    /// LineSegment × ArcSegment / ArcSegment × ArcSegment).
    /// </summary>
    public static CurveIntersectionResult IntersectRuns(IPathSegment a, IPathSegment b, GeometryTolerance tolerance)
    {
        return a switch
        {
            LineSegment la when b is LineSegment lb => IntersectSegments(la, lb, tolerance),
            LineSegment la when b is ArcSegment ab => IntersectSegments(la, ab, tolerance),
            ArcSegment aa when b is LineSegment lb => IntersectSegments(aa, lb, tolerance),
            ArcSegment aa when b is ArcSegment ab => IntersectSegments(aa, ab, tolerance),
            _ => CurveIntersectionResult.None,
        };
    }

    /// <summary>
    /// Collects the distinct transversal crossings between two paths' runs.
    /// Tangencies, collinear runs and overlaps are deliberately excluded —
    /// they are not cuts; Trim / Extend / Break must refuse to split on them.
    /// Points closer than <see cref="GeometryTolerance.IntersectionTolerance"/>
    /// are de-duplicated.
    /// </summary>
    public static IReadOnlyList<Point2> CollectBoundaryPoints(
        IReadOnlyList<IPathSegment> pathA, IReadOnlyList<IPathSegment> pathB,
        GeometryTolerance tolerance)
    {
        var result = new List<Point2>();
        double eps = tolerance.IntersectionTolerance;
        foreach (var sa in pathA)
        {
            foreach (var sb in pathB)
            {
                var r = IntersectRuns(sa, sb, tolerance);

                if (r.Kind is not (CurveIntersectionKind.Point or CurveIntersectionKind.TwoPoints))
                {
                    continue;
                }

                foreach (var p in r.Points)
                {
                    bool duplicate = false;
                    for (int i = 0; i < result.Count; i++)
                    {
                        if (result[i].DistanceTo(p) <= eps)
                        {
                            duplicate = true;
                            break;
                        }
                    }

                    if (!duplicate)
                    {
                        result.Add(p);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>Collects boundary crossings between two entities (polylines are split into runs).</summary>
    public static IReadOnlyList<Point2> CollectBoundaryPoints(
        IGeometryEntity a, IGeometryEntity b, GeometryTolerance tolerance)
    {
        return CollectBoundaryPoints(RunsOf(a), RunsOf(b), tolerance);
    }

    /// <summary>Returns the run list of an entity (single-run for primitives).</summary>
    internal static IReadOnlyList<IPathSegment> RunsOf(IGeometryEntity entity) => entity switch
    {
        LineGeometry l => [new LineSegment(l.StartPoint, l.EndPoint)],
        ArcGeometry arc => [new ArcSegment(arc.Center, arc.Radius, arc.StartAngleRadians, arc.SweepRadians, arc.IsCounterClockwise)],
        PolylineGeometry p when p.Segments.Count > 0 => p.Segments,
        _ => throw new ArgumentException($"Unsupported entity type for runs: {entity.GetType().Name}"),
    };

    // ------------------------------------------------------------------
    // Core math
    // ------------------------------------------------------------------

    private static CurveIntersectionResult LineLine(Point2 a0, Point2 a1, Point2 b0, Point2 b1, GeometryTolerance tolerance)
    {
        var legacy = Intersect(a0, a1, b0, b1, tolerance.IntersectionTolerance);
        return legacy.Kind switch
        {
            LineIntersectionKind.None => CurveIntersectionResult.None,
            LineIntersectionKind.Point => CurveIntersectionResult.At(legacy.Point),
            LineIntersectionKind.Parallel => CurveIntersectionResult.Parallel(),
            LineIntersectionKind.CollinearOverlap => legacy.Point.DistanceTo(legacy.EndPoint) <= tolerance.IntersectionTolerance
                ? CurveIntersectionResult.At(legacy.Point)
                : CurveIntersectionResult.Overlap(legacy.Point, legacy.EndPoint),
            _ => CurveIntersectionResult.None,
        };
    }

    /// <summary>
    /// Line segment vs arc: hits on the supporting circle are filtered by the
    /// arc's sweep (honoring CW / CCW and sweeps crossing the 0° axis).
    /// </summary>
    private static CurveIntersectionResult LineArc(
        Point2 a0, Point2 a1,
        Point2 center, double radius, double startAngle, double sweep, bool isCcw,
        GeometryTolerance tolerance)
    {
        if (radius <= 0)
        {
            return CurveIntersectionResult.Degenerate();
        }

        var hits = LineCirclePoints(a0, a1, center, radius, tolerance);
        if (hits.Count == 0)
        {
            return CurveIntersectionResult.None;
        }

        if (hits.Tangent)
        {
            return AngleInSweep(ToArcAngle(hits.P1, center), startAngle, sweep, isCcw, AngleTolerance(radius, tolerance))
                ? CurveIntersectionResult.TangentAt(hits.P1)
                : CurveIntersectionResult.None;
        }

        // A single non-tangent crossing (only one of the supporting-circle
        // hits lies on the segment): P1 and P2 carry the same point, so the
        // two-point path below must not run — evaluate the one hit directly.
        if (hits.Count == 1)
        {
            return AngleInSweep(ToArcAngle(hits.P1, center), startAngle, sweep, isCcw, AngleTolerance(radius, tolerance))
                ? CurveIntersectionResult.At(hits.P1)
                : CurveIntersectionResult.None;
        }

        bool p1 = AngleInSweep(ToArcAngle(hits.P1, center), startAngle, sweep, isCcw, AngleTolerance(radius, tolerance));
        bool p2 = AngleInSweep(ToArcAngle(hits.P2, center), startAngle, sweep, isCcw, AngleTolerance(radius, tolerance));
        return (p1, p2) switch
        {
            (true, true) => CurveIntersectionResult.Two(hits.P1, hits.P2),
            (true, false) => CurveIntersectionResult.At(hits.P1),
            (false, true) => CurveIntersectionResult.At(hits.P2),
            _ => CurveIntersectionResult.None,
        };
    }

    private static CurveIntersectionResult CircleArc(CircleGeometry circle, ArcGeometry arc, GeometryTolerance tolerance)
    {
        if (circle.Radius <= 0 || arc.Radius <= 0)
        {
            return CurveIntersectionResult.Degenerate();
        }

        double centerDist = circle.Center.DistanceTo(arc.Center);
        if (centerDist <= tolerance.IntersectionTolerance)
        {
            // Concentric: distinct radii never intersect; equal radii share the
            // full circle — the arc lies entirely on the circle.
            return Math.Abs(circle.Radius - arc.Radius) <= tolerance.IntersectionTolerance
                ? CurveIntersectionResult.Overlap(arc.StartPoint, arc.EndPoint)
                : CurveIntersectionResult.None;
        }

        var hits = CircleCirclePoints(circle.Center, circle.Radius, arc.Center, arc.Radius, tolerance);
        if (hits.Count == 0)
        {
            return CurveIntersectionResult.None;
        }

        if (hits.Tangent)
        {
            return AngleInSweep(ToArcAngle(hits.P1, arc.Center), arc.StartAngleRadians, arc.SweepRadians, arc.IsCounterClockwise, AngleTolerance(arc.Radius, tolerance))
                ? CurveIntersectionResult.TangentAt(hits.P1)
                : CurveIntersectionResult.None;
        }

        // See LineArc: a single non-tangent hit must not fall into the
        // two-point branch (P1 and P2 are then the same point).
        if (hits.Count == 1)
        {
            return AngleInSweep(ToArcAngle(hits.P1, arc.Center), arc.StartAngleRadians, arc.SweepRadians, arc.IsCounterClockwise, AngleTolerance(arc.Radius, tolerance))
                ? CurveIntersectionResult.At(hits.P1)
                : CurveIntersectionResult.None;
        }

        bool p1 = AngleInSweep(ToArcAngle(hits.P1, arc.Center), arc.StartAngleRadians, arc.SweepRadians, arc.IsCounterClockwise, AngleTolerance(arc.Radius, tolerance));
        bool p2 = AngleInSweep(ToArcAngle(hits.P2, arc.Center), arc.StartAngleRadians, arc.SweepRadians, arc.IsCounterClockwise, AngleTolerance(arc.Radius, tolerance));
        return (p1, p2) switch
        {
            (true, true) => CurveIntersectionResult.Two(hits.P1, hits.P2),
            (true, false) => CurveIntersectionResult.At(hits.P1),
            (false, true) => CurveIntersectionResult.At(hits.P2),
            _ => CurveIntersectionResult.None,
        };
    }

    private static CurveIntersectionResult ArcArc(
        Point2 c1, double r1, double start1, double sweep1, bool ccw1,
        Point2 c2, double r2, double start2, double sweep2, bool ccw2,
        GeometryTolerance tolerance)
    {
        if (r1 <= 0 || r2 <= 0)
        {
            return CurveIntersectionResult.Degenerate();
        }

        double centerDist = c1.DistanceTo(c2);
        if (centerDist <= tolerance.IntersectionTolerance)
        {
            // Concentric arcs.
            if (Math.Abs(r1 - r2) > tolerance.IntersectionTolerance)
            {
                return CurveIntersectionResult.None;
            }

            return SameCircleOverlap(c1, r1, start1, sweep1, ccw1, start2, sweep2, ccw2, tolerance);
        }

        var hits = CircleCirclePoints(c1, r1, c2, r2, tolerance);
        if (hits.Count == 0)
        {
            return CurveIntersectionResult.None;
        }

        bool both(Point2 p)
        {
            return AngleInSweep(ToArcAngle(p, c1), start1, sweep1, ccw1, AngleTolerance(r1, tolerance))
                && AngleInSweep(ToArcAngle(p, c2), start2, sweep2, ccw2, AngleTolerance(r2, tolerance));
        }

        if (hits.Tangent)
        {
            return both(hits.P1) ? CurveIntersectionResult.TangentAt(hits.P1) : CurveIntersectionResult.None;
        }

        bool p1 = both(hits.P1);
        bool p2 = both(hits.P2);
        return (p1, p2) switch
        {
            (true, true) => CurveIntersectionResult.Two(hits.P1, hits.P2),
            (true, false) => CurveIntersectionResult.At(hits.P1),
            (false, true) => CurveIntersectionResult.At(hits.P2),
            _ => CurveIntersectionResult.None,
        };
    }

    /// <summary>
    /// Two arcs on the same supporting circle: resolved in sweep space.
    /// Returns Coincident for identical sweeps, Overlap for a shared interval
    /// of positive length, None otherwise.
    /// </summary>
    private static CurveIntersectionResult SameCircleOverlap(
        Point2 center, double radius,
        double start1, double sweep1, bool ccw1,
        double start2, double sweep2, bool ccw2,
        GeometryTolerance tolerance)
    {
        // Normalize both sweeps to CCW order in the 0..2π domain.
        double a0 = NormalizeCcwStart(start1, sweep1, ccw1);
        double aSweep = Math.Abs(sweep1);
        double b0 = NormalizeCcwStart(start2, sweep2, ccw2);
        double bSweep = Math.Abs(sweep2);
        double angleTol = AngleTolerance(radius, tolerance);

        if (MathUtil.AngularDifference(a0, b0) <= angleTol && Math.Abs(aSweep - bSweep) <= angleTol)
        {
            return CurveIntersectionResult.Coincident();
        }

        bool bStartInA = AngleInCcwSweep(b0, a0, aSweep, angleTol);
        bool bEndInA = AngleInCcwSweep(b0 + bSweep, a0, aSweep, angleTol);
        bool aStartInB = AngleInCcwSweep(a0, b0, bSweep, angleTol);
        bool aEndInB = AngleInCcwSweep(a0 + aSweep, b0, bSweep, angleTol);

        bool overlap = bStartInA || bEndInA || aStartInB || aEndInB;
        if (!overlap)
        {
            return CurveIntersectionResult.None;
        }

        // One arc fully inside the other: report the covered arc's span.
        // Partial overlap: still an interval of positive length — callers only
        // branch on the kind, not on the exact interval points.
        return CurveIntersectionResult.Overlap(
            PointAtArcAngle(center, radius, a0),
            PointAtArcAngle(center, radius, a0 + aSweep));
    }

    private static double NormalizeCcwStart(double start, double sweep, bool ccw)
    {
        // A CW arc from `start` sweeping −|sweep| ends at start − |sweep|; the
        // same geometric region runs CCW from that end angle with length |sweep|.
        return ccw ? start : MathUtil.Normalize0To2Pi(start - Math.Abs(sweep));
    }

    /// <summary>
    /// Whether an absolute angle lies inside a sweep, measured from
    /// <paramref name="start0To2Pi"/> in the given direction with the given
    /// angular tolerance. Handles sweeps crossing the 0° axis.
    /// </summary>
    private static bool AngleInSweep(double angle, double start, double sweep, bool ccw, double angleTol)
    {
        double sweepLen = Math.Abs(sweep);
        double rel = ccw
            ? MathUtil.Normalize0To2Pi(angle - start)
            : MathUtil.Normalize0To2Pi(start - angle);
        return rel <= sweepLen + angleTol;
    }

    private static bool AngleInCcwSweep(double angle, double ccwStart, double ccwSweepLen, double angleTol)
    {
        return MathUtil.Normalize0To2Pi(angle - ccwStart) <= ccwSweepLen + angleTol;
    }

    private static double ToArcAngle(Point2 p, Point2 center)
    {
        return Math.Atan2(p.Y - center.Y, p.X - center.X);
    }

    private static Point2 PointAtArcAngle(Point2 center, double radius, double angle)
    {
        return new Point2(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
    }

    /// <summary>
    /// Angular tolerance derived from the linear intersection tolerance
    /// projected onto the arc radius (plus the plain parameter tolerance).
    /// </summary>
    private static double AngleTolerance(double radius, GeometryTolerance tolerance)
    {
        return Math.Max(tolerance.ParameterTolerance, tolerance.IntersectionTolerance / Math.Max(radius, 1e-12));
    }

    // ------------------------------------------------------------------
    // Line ↔ Circle and Circle ↔ Circle point solving
    // ------------------------------------------------------------------

    private readonly record struct CircleHits(int Count, Point2 P1, Point2 P2, bool Tangent);

    private static readonly CircleHits NoHits = new(0, Point2.Origin, Point2.Origin, false);

    private static CircleHits LineCirclePoints(Point2 a0, Point2 a1, Point2 center, double radius, GeometryTolerance tolerance)
    {
        double eps = Math.Max(tolerance.IntersectionTolerance, 1e-12);
        double dx = a1.X - a0.X;
        double dy = a1.Y - a0.Y;
        double len2 = dx * dx + dy * dy;
        if (len2 <= eps * eps || radius <= 0)
        {
            return NoHits;
        }

        double len = Math.Sqrt(len2);
        double px = center.X - a0.X;
        double py = center.Y - a0.Y;
        double t = (px * dx + py * dy) / len2;                  // param of the foot of the center
        double footX = a0.X + t * dx;
        double footY = a0.Y + t * dy;
        double dist2 = (center.X - footX) * (center.X - footX) + (center.Y - footY) * (center.Y - footY);
        double dist = Math.Sqrt(dist2);

        if (dist > radius + eps)
        {
            return NoHits;
        }

        // Unit direction of the segment: the chord half-length h is measured
        // ALONG the line (foot ± h·u), not perpendicular to it.
        double ux = dx / len;
        double uy = dy / len;
        double h2 = radius * radius - dist2;
        if (h2 < 0)
        {
            h2 = 0;
        }

        double h = Math.Sqrt(h2);
        if (h <= tolerance.TangencyTolerance)
        {
            if (OnSegment(footX, footY, a0, a1, tolerance))
            {
                return new CircleHits(1, new Point2(footX, footY), new Point2(footX, footY), true);
            }

            return NoHits;
        }

        Point2 p1 = new(footX + ux * h, footY + uy * h);
        Point2 p2 = new(footX - ux * h, footY - uy * h);
        bool on1 = OnSegment(p1.X, p1.Y, a0, a1, tolerance);
        bool on2 = OnSegment(p2.X, p2.Y, a0, a1, tolerance);

        if (on1 && on2)
        {
            return new CircleHits(2, p1, p2, false);
        }

        if (on1)
        {
            return new CircleHits(1, p1, p1, false);
        }

        if (on2)
        {
            return new CircleHits(1, p2, p2, false);
        }

        return NoHits;
    }

    /// <summary>Inclusive segment test with the parameter-interval tolerance.</summary>
    private static bool OnSegment(double x, double y, Point2 a0, Point2 a1, GeometryTolerance tolerance)
    {
        double dx = a1.X - a0.X;
        double dy = a1.Y - a0.Y;
        double len2 = dx * dx + dy * dy;
        if (len2 <= 0)
        {
            return false;
        }

        double t = ((x - a0.X) * dx + (y - a0.Y) * dy) / len2;
        double paramPad = tolerance.ParameterTolerance <= 0 ? 1e-9 : tolerance.ParameterTolerance;
        return t >= -paramPad && t <= 1.0 + paramPad;
    }

    private static CircleHits CircleCirclePoints(Point2 c1, double r1, Point2 c2, double r2, GeometryTolerance tolerance)
    {
        double eps = Math.Max(tolerance.IntersectionTolerance, 1e-12);
        if (r1 <= 0 || r2 <= 0)
        {
            return NoHits;
        }

        double dx = c2.X - c1.X;
        double dy = c2.Y - c1.Y;
        double d = Math.Sqrt(dx * dx + dy * dy);
        if (d <= eps)
        {
            // Concentric: equal radii coincide (handled by caller), else none.
            return NoHits;
        }

        double rPlus = r1 + r2;
        double rMinus = Math.Abs(r1 - r2);
        if (d > rPlus + tolerance.TangencyTolerance)
        {
            return NoHits;
        }

        if (d < rMinus - tolerance.TangencyTolerance)
        {
            return NoHits;
        }

        // External tangency: contact on the line joining the centers.
        if (Math.Abs(d - rPlus) <= tolerance.TangencyTolerance)
        {
            double tx = c1.X + r1 * dx / d;
            double ty = c1.Y + r1 * dy / d;
            return new CircleHits(1, new Point2(tx, ty), new Point2(tx, ty), true);
        }

        // Internal tangency: contact on the far side of the smaller circle.
        if (Math.Abs(d - rMinus) <= tolerance.TangencyTolerance)
        {
            double side = r1 >= r2 ? 1.0 : -1.0;
            double tx = c1.X + side * r1 * dx / d;
            double ty = c1.Y + side * r1 * dy / d;
            return new CircleHits(1, new Point2(tx, ty), new Point2(tx, ty), true);
        }

        // Two crossings: standard circle-circle formula.
        double a = (r1 * r1 - r2 * r2 + d * d) / (2 * d);
        double h2 = r1 * r1 - a * a;
        double h = h2 > 0 ? Math.Sqrt(h2) : 0;
        double mx = c1.X + a * dx / d;
        double my = c1.Y + a * dy / d;
        double px = -dy / d;
        double py = dx / d;

        Point2 q1 = new(mx + h * px, my + h * py);
        Point2 q2 = new(mx - h * px, my - h * py);
        return new CircleHits(2, q1, q2, false);
    }

    private static CurveIntersectionResult LineCircle(Point2 a0, Point2 a1, Point2 center, double radius, GeometryTolerance tolerance)
    {
        var hits = LineCirclePoints(a0, a1, center, radius, tolerance);
        if (hits.Count == 0)
        {
            return CurveIntersectionResult.None;
        }

        return hits.Tangent
            ? CurveIntersectionResult.TangentAt(hits.P1)
            : hits.Count == 2
                ? CurveIntersectionResult.Two(hits.P1, hits.P2)
                : CurveIntersectionResult.At(hits.P1);
    }

    private static CurveIntersectionResult CircleCircle(Point2 c1, double r1, Point2 c2, double r2, GeometryTolerance tolerance)
    {
        if (r1 <= 0 || r2 <= 0)
        {
            return CurveIntersectionResult.Degenerate();
        }

        double d = c1.DistanceTo(c2);
        if (d <= tolerance.IntersectionTolerance)
        {
            return Math.Abs(r1 - r2) <= tolerance.IntersectionTolerance
                ? CurveIntersectionResult.Coincident()
                : CurveIntersectionResult.None;
        }

        var hits = CircleCirclePoints(c1, r1, c2, r2, tolerance);
        return hits.Count switch
        {
            0 => CurveIntersectionResult.None,
            1 => hits.Tangent
                ? CurveIntersectionResult.TangentAt(hits.P1)
                : CurveIntersectionResult.At(hits.P1),
            _ => CurveIntersectionResult.Two(hits.P1, hits.P2),
        };
    }
}