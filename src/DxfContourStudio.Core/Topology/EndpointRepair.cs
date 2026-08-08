#nullable enable

using DxfContourStudio.Core.Contours;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Topology;

/// <summary>
/// Moves one endpoint of an entity to a target point, returning a NEW entity
/// (entities are immutable). Used by the gap repair command: both gap ends
/// are moved to their midpoint, which closes the chain.
///
/// Semantics per type:
/// - LINE        : the moved endpoint becomes the target directly;
/// - ARC         : the moved endpoint becomes the point on the same circle in
///                 the target's direction; radius, center and sweep magnitude
///                 are preserved (the sweep sign / direction is unchanged);
/// - POLYLINE    : only the addressed run is rebuilt; all other runs and the
///                 interior joints stay untouched, so the chain keeps its
///                 connectivity on every other side;
/// - CIRCLE      : has no endpoint — not applicable.
/// </summary>
public static class EndpointRepair
{
    /// <summary>
    /// Returns a new entity identical to <paramref name="entity"/> except
    /// that the endpoint identified by (<paramref name="segmentIndex"/>,
    /// <paramref name="isStart"/>) is moved to <paramref name="target"/>.
    /// </summary>
    public static IGeometryEntity MoveEndpoint(IGeometryEntity entity, int segmentIndex, bool isStart, Point2 target)
    {
        switch (entity)
        {
            case LineGeometry line:
                return new LineGeometry(line.Id, line.LayerName, isStart ? target : line.P0, isStart ? line.P1 : target, line.IsVisible);

            case ArcGeometry arc:
                return MoveArc(arc, target, isStart);

            case PolylineGeometry polyline:
                return MovePolylineSegment(polyline, segmentIndex, isStart, target);

            default:
                throw new NotSupportedException(
                    $"Endpoint repair is not defined for entity type {entity.GetType().Name}.");
        }
    }

    private static ArcGeometry MoveArc(ArcGeometry arc, Point2 target, bool isStart)
    {
        double newAngle = ProjectAngle(arc.Center, target, arc.StartAngleRadians);
        double sweep = arc.SweepRadians; // signed: +CCW / -CW, magnitude kept
        double newStart = isStart ? newAngle : newAngle - sweep;
        return new ArcGeometry(arc.Id, arc.LayerName, arc.Center, arc.Radius, newStart, sweep, arc.IsCounterClockwise, arc.IsVisible);
    }

    private static PolylineGeometry MovePolylineSegment(PolylineGeometry polyline, int segmentIndex, bool isStart, Point2 target)
    {
        if (segmentIndex < 0 || segmentIndex >= polyline.Segments.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentIndex));
        }

        var segments = new IPathSegment[polyline.Segments.Count];
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i] = polyline.Segments[i];
        }

        segments[segmentIndex] = polyline.Segments[segmentIndex] switch
        {
            LineSegment ls => new LineSegment(isStart ? target : ls.StartPoint, isStart ? ls.EndPoint : target),
            ArcSegment arcSegment => MoveArcSegment(arcSegment, target, isStart),
            var other => other,
        };

        return new PolylineGeometry(polyline.Id, polyline.LayerName, segments, polyline.IsClosed, polyline.IsVisible);
    }

    private static ArcSegment MoveArcSegment(ArcSegment arc, Point2 target, bool isStart)
    {
        double newAngle = ProjectAngle(arc.Center, target, arc.StartAngleRadians);
        double sweep = arc.SweepRadians;
        double newStart = isStart ? newAngle : newAngle - sweep;
        return new ArcSegment(arc.Center, arc.Radius, newStart, sweep, arc.IsCounterClockwise);
    }

    /// <summary>Angle of the ray from center toward the target; falls back to the current angle when the target coincides with the center.</summary>
    private static double ProjectAngle(Point2 center, Point2 target, double fallbackAngle)
    {
        double dx = target.X - center.X;
        double dy = target.Y - center.Y;
        if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9)
        {
            return fallbackAngle;
        }

        return Math.Atan2(dy, dx);
    }
}
