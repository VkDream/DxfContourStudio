#nullable enable

using System;
using System.Collections.Generic;

namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// Vertex (node) editing for path entities (docs/ADR-014-Node-Editing.md):
///
/// - <see cref="NodePositions"/> lists the draggable nodes of an entity in
///   visual order: a Line has 2 (start, end); an Arc has 2 (its two path
///   endpoints); a Polyline has one node per vertex (runs count + 1, or
///   runs count when closed). Circles have NO draggable nodes.
/// - <see cref="MoveNode"/> relocates one node to an absolute target:
///   - Line nodes simply re-place the corresponding endpoint;
///   - Arc nodes keep the center and radius — the endpoint angle is changed
///     so the point lies on the same circle at the target's angle, and the
///     sweep is re-computed (direction preserved, |sweep| &lt; 2π enforced;
///     a target exactly opposite is refused);
///   - Polyline interior vertices move the vertex point itself; the two
///     surrounding runs are re-anchored to it (runs keep their kind, arcs
///     keep center/radius/ccw and re-splice their end angle).
/// - A polyline arc run's far endpoint may move onto another circle point;
///   this changes that arc's sweep but not its center or radius.
/// - Refused targets (null result): nodes out of range, arcs whose
///   re-computed sweep would be 0 or ≥ 2π, and moving a node onto itself.
/// </summary>
public static class NodeEditEngine
{
    /// <summary>Number of draggable nodes the entity exposes.</summary>
    public static int NodeCount(IGeometryEntity entity) => entity switch
    {
        LineGeometry => 2,
        ArcGeometry => 2,
        PolylineGeometry poly => poly.IsClosed ? poly.Segments.Count : poly.Segments.Count + 1,
        _ => 0,
    };

    /// <summary>Visual node positions in order (for hit-testing / rendering).</summary>
    public static IReadOnlyList<Point2> NodePositions(IGeometryEntity entity)
    {
        var nodes = new List<Point2>();
        switch (entity)
        {
            case LineGeometry line:
                nodes.Add(line.P0);
                nodes.Add(line.P1);
                break;

            case ArcGeometry arc:
                nodes.Add(arc.StartPoint);
                nodes.Add(arc.EndPoint);
                break;

            case PolylineGeometry poly:
                foreach (var run in poly.Segments)
                {
                    nodes.Add(run.StartPoint);
                }

                if (!poly.IsClosed)
                {
                    nodes.Add(poly.Segments[^1].EndPoint);
                }

                break;
        }

        return nodes;
    }

    /// <summary>
    /// Moves one node of the entity to the absolute target position.
    /// Returns a new entity on success, null when refused.
    /// </summary>
    public static IGeometryEntity? MoveNode(IGeometryEntity entity, int nodeIndex, Point2 target)
    {
        if (nodeIndex < 0)
        {
            return null;
        }

        return entity switch
        {
            LineGeometry line => MoveLineNode(line, nodeIndex, target),
            ArcGeometry arc => MoveArcNode(arc, nodeIndex, target),
            PolylineGeometry poly => MovePolylineNode(poly, nodeIndex, target),
            _ => null,
        };
    }

    /// <summary>
    /// Moves a circle to a new center (center-grip drag). Refused when the
    /// target coincides with the current center (pure no-op).
    /// </summary>
    public static IGeometryEntity? MoveCircleCenter(CircleGeometry circle, Point2 target)
    {
        if (target == circle.Center || !IsFinitePoint(target))
        {
            return null;
        }

        return new CircleGeometry(circle.Id, circle.LayerName, target, circle.Radius, circle.IsVisible);
    }

    /// <summary>
    /// Sets a new radius (radius-grip drag). Refused unless the radius stays
    /// strictly positive and finite.
    /// </summary>
    public static IGeometryEntity? SetCircleRadius(CircleGeometry circle, double radius)
    {
        if (!double.IsFinite(radius) || radius <= GeometryTolerance.Default.ZeroLengthTolerance)
        {
            return null;
        }

        return new CircleGeometry(circle.Id, circle.LayerName, circle.Center, radius, circle.IsVisible);
    }

    /// <summary>
    /// Moves an arc's center grip: the whole arc translates so its sweep,
    /// radius and direction stay identical (an arc center drag is a pure
    /// translation of the entity).
    /// </summary>
    public static IGeometryEntity? TranslateArcCenter(ArcGeometry arc, Point2 delta)
    {
        if (!IsFinitePoint(delta))
        {
            return null;
        }

        return arc.Transformed(Transform2.CreateTranslation(new Vector2(delta.X, delta.Y)));
    }

    private static bool IsFinitePoint(Point2 p) => double.IsFinite(p.X) && double.IsFinite(p.Y);

    private static IGeometryEntity? MoveLineNode(LineGeometry line, int nodeIndex, Point2 target)
    {
        return nodeIndex switch
        {
            0 when target != line.P0 => new LineGeometry(line.Id, line.LayerName, target, line.P1, line.IsVisible),
            1 when target != line.P1 => new LineGeometry(line.Id, line.LayerName, line.P0, target, line.IsVisible),
            _ => null,
        };
    }

    private static IGeometryEntity? MoveArcNode(ArcGeometry arc, int nodeIndex, Point2 target)
    {
        if (nodeIndex is not (0 or 1))
        {
            return null;
        }

        // Keep the circle; re-derive the angle of the moved endpoint.
        double targetAngle = Math.Atan2(target.Y - arc.Center.Y, target.X - arc.Center.X);
        double newSweep;
        bool ccw = arc.IsCounterClockwise;

        if (nodeIndex == 0)
        {
            // start moves: new start angle = targetAngle; end stays.
            double endAngle = arc.EndAngleRadians;
            newSweep = ccw ? WrapPositive(endAngle - targetAngle) : -WrapPositive(targetAngle - endAngle);
        }
        else
        {
            // end moves: keep start, new sweep goes to targetAngle.
            double startAngle = arc.StartAngleRadians;
            newSweep = ccw ? WrapPositive(targetAngle - startAngle) : -WrapPositive(startAngle - targetAngle);
        }

        if (Math.Abs(newSweep) < 1e-12 || Math.Abs(newSweep) >= MathUtil.TwoPi)
        {
            return null; // degenerate or full turn
        }

        double newStart = nodeIndex == 0 ? targetAngle : arc.StartAngleRadians;
        try
        {
            return new ArcGeometry(arc.Id, arc.LayerName, arc.Center, arc.Radius, newStart, newSweep, ccw, arc.IsVisible);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static IGeometryEntity? MovePolylineNode(PolylineGeometry poly, int nodeIndex, Point2 target)
    {
        int count = poly.IsClosed ? poly.Segments.Count : poly.Segments.Count + 1;
        if (nodeIndex >= count)
        {
            return null;
        }

        var runs = poly.Segments;
        if (runs.Count == 0)
        {
            return null;
        }

        if (poly.IsClosed)
        {
            // Node i sits between runs i-1 and i. Re-anchor only lines.
            return MoveClosedNode(poly, nodeIndex, target);
        }

        if (nodeIndex == 0)
        {
            return MoveOpenEnd(poly, 0, target);
        }

        if (nodeIndex == runs.Count)
        {
            return MoveOpenEnd(poly, runs.Count - 1, target, end: true);
        }

        // Interior vertex between runs[nodeIndex-1] and runs[nodeIndex].
        return MoveInteriorVertex(poly, nodeIndex, target);
    }

    private static IGeometryEntity? MoveOpenEnd(PolylineGeometry poly, int runIndex, Point2 target, bool end = false)
    {
        var runs = poly.Segments;
        var newRuns = new List<IPathSegment>(runs);
        var run = runs[runIndex];
        var moved = end ? MoveRunEnd(run, target) : MoveRunStart(run, target);
        if (moved is null)
        {
            return null;
        }

        newRuns[runIndex] = moved;
        return new PolylineGeometry(poly.Id, poly.LayerName, newRuns, poly.IsClosed, poly.IsVisible);
    }

    private static IGeometryEntity? MoveInteriorVertex(PolylineGeometry poly, int nodeIndex, Point2 target)
    {
        var runs = poly.Segments;
        var newRuns = new List<IPathSegment>(runs);
        int prevIndex = nodeIndex - 1;
        int nextIndex = nodeIndex;

        var prev = runs[prevIndex];
        var next = runs[nextIndex];
        var newPrev = MoveRunEnd(prev, target);
        var newNext = MoveRunStart(next, target);
        if (newPrev is null || newNext is null)
        {
            return null;
        }

        newRuns[prevIndex] = newPrev;
        newRuns[nextIndex] = newNext;
        return new PolylineGeometry(poly.Id, poly.LayerName, newRuns, poly.IsClosed, poly.IsVisible);
    }

    private static IGeometryEntity? MoveClosedNode(PolylineGeometry poly, int nodeIndex, Point2 target)
    {
        var runs = poly.Segments;
        var newRuns = new List<IPathSegment>(runs);
        int prevIndex = (nodeIndex - 1 + runs.Count) % runs.Count;
        int nextIndex = nodeIndex;

        var prev = runs[prevIndex];
        var next = runs[nextIndex];
        var movedPrev = MoveRunEnd(prev, target);
        var movedNext = MoveRunStart(next, target);
        if (movedPrev is null || movedNext is null)
        {
            return null;
        }

        newRuns[prevIndex] = movedPrev;
        newRuns[nextIndex] = movedNext;
        return new PolylineGeometry(poly.Id, poly.LayerName, newRuns, poly.IsClosed, poly.IsVisible);
    }

    private static IPathSegment? MoveRunStart(IPathSegment run, Point2 target) => run switch
    {
        LineSegment l => new LineSegment(target, l.EndPoint),
        ArcSegment a => ReAnchorArcStart(a, target, out var seg) ? seg : null,
        _ => null,
    };

    private static IPathSegment? MoveRunEnd(IPathSegment run, Point2 target) => run switch
    {
        LineSegment l => new LineSegment(l.StartPoint, target),
        ArcSegment a => ReAnchorArcEnd(a, target, out var seg) ? seg : null,
        _ => null,
    };

    private static bool ReAnchorArcEnd(ArcSegment a, Point2 target, out IPathSegment segment)
    {
        segment = a;
        double angle = Math.Atan2(target.Y - a.Center.Y, target.X - a.Center.X);
        double newSweep = a.IsCounterClockwise
            ? WrapPositive(angle - a.StartAngleRadians)
            : -WrapPositive(a.StartAngleRadians - angle);
        return TryArcSegment(a, a.StartAngleRadians, newSweep, out segment);
    }

    private static bool ReAnchorArcStart(ArcSegment a, Point2 target, out IPathSegment segment)
    {
        segment = a;
        double angle = Math.Atan2(target.Y - a.Center.Y, target.X - a.Center.X);
        double endAngle = a.StartAngleRadians + a.SweepRadians;
        double newSweep = a.IsCounterClockwise
            ? WrapPositive(endAngle - angle)
            : -WrapPositive(angle - endAngle);
        return TryArcSegment(a, angle, newSweep, out segment);
    }

    private static bool TryArcSegment(ArcSegment a, double start, double sweep, out IPathSegment segment)
    {
        segment = a;
        if (Math.Abs(sweep) < 1e-12 || Math.Abs(sweep) >= MathUtil.TwoPi)
        {
            return false;
        }

        segment = new ArcSegment(a.Center, a.Radius, start, sweep, a.IsCounterClockwise);
        return true;
    }

    private static double WrapPositive(double angle)
    {
        angle %= MathUtil.TwoPi;
        return angle < 0 ? angle + MathUtil.TwoPi : angle;
    }
}