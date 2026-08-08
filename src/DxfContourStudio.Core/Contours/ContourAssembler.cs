#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Contours;

/// <summary>
/// Turns a <see cref="ContourChain"/> into a measured <see cref="Contour"/>:
/// length, bounds, signed area (shoelace with exact arc correction) and
/// orientation. Circles become closed contours directly from their entity.
///
/// Arc area contribution (exact, from the line-integral ½∮(x dy − y dx)):
///   A = ½ · [ r²·(b−a) + r·cx·(sin b − sin a) − r·cy·(cos b − cos a) ]
/// with angles a→b along the traversal direction. This keeps areas exact for
/// arc segments instead of approximating bulges by chords.
/// </summary>
public static class ContourAssembler
{
    /// <summary>Assembles the chain; <paramref name="entities"/> is used to resolve circle entities.</summary>
    public static Contour Assemble(ContourChain chain, IReadOnlyDictionary<long, IGeometryEntity> entities)
    {
        if (chain.IsCircle)
        {
            return AssembleCircle(chain, entities);
        }

        var steps = chain.Steps;
        double length = chain.Length;
        double area = 0.0;
        var bounds = Bounds.Empty;

        foreach (ChainStep step in steps)
        {
            Topology.TopologyEdge edge = step.Edge;
            Point2 p0 = step.Forward ? edge.StartPoint : edge.EndPoint;
            Point2 p1 = step.Forward ? edge.EndPoint : edge.StartPoint;
            bounds = bounds.Include(p0).Include(p1);
            area += AreaContribution(edge, step.Forward);
            if (edge.SegmentType == GeometryType.Arc)
            {
                (Point2 center, double radius, double startAngle, double sweep) = ArcParameters(edge);
                for (int i = 1; i <= 4; i++)
                {
                    double t = i / 5.0;
                    double angle = step.Forward ? startAngle + sweep * t : startAngle + sweep * (1 - t);
                    bounds = bounds.Include(PointAt(center, radius, angle));
                }
            }
        }

        var contour = new Contour
        {
            Steps = steps,
            IsClosed = chain.IsClosed,
            Length = length,
            Bounds = bounds,
            Warnings = chain.IsClosed ? [] : ["Contours.Warning.Open"],
        };

        if (chain.IsClosed && steps.Count > 0)
        {
            contour.SignedArea = area;
            contour.Orientation = area > 0 ? ContourOrientation.CounterClockwise : ContourOrientation.Clockwise;
        }

        return contour;
    }

    private static Contour AssembleCircle(ContourChain chain, IReadOnlyDictionary<long, IGeometryEntity> entities)
    {
        CircleGeometry circle = (CircleGeometry)entities[chain.CircleEntityId];
        double radius = circle.Radius;
        return new Contour
        {
            Steps = [],
            IsClosed = true,
            IsCircle = true,
            CircleEntityId = chain.CircleEntityId,
            CircleRadius = radius,
            CircleCenter = circle.Center,
            Length = MathUtil.TwoPi * radius,
            Bounds = new Bounds(circle.Center.X - radius, circle.Center.Y - radius, circle.Center.X + radius, circle.Center.Y + radius),
            SignedArea = Math.PI * radius * radius,
            Orientation = ContourOrientation.CounterClockwise,
            Warnings = [],
        };
    }

    /// <summary>Signed area contribution of one edge crossed in the given direction.</summary>
    private static double AreaContribution(Topology.TopologyEdge edge, bool forward)
    {
        if (edge.SegmentType == GeometryType.Line)
        {
            Point2 p0 = forward ? edge.StartPoint : edge.EndPoint;
            Point2 p1 = forward ? edge.EndPoint : edge.StartPoint;
            return 0.5 * (p0.X * p1.Y - p1.X * p0.Y);
        }

        // arc
        (Point2 c, double r, double a0, double sweep) = ArcParameters(edge);
        double a = forward ? a0 : a0 + sweep;
        double b = forward ? a0 + sweep : a0;
        return 0.5 * (
            r * r * (b - a) +
            r * c.X * (Math.Sin(b) - Math.Sin(a)) -
            r * c.Y * (Math.Cos(b) - Math.Cos(a)));
    }

    /// <summary>
    /// Extracts the arc parameters of an edge's source run. The sweep is
    /// signed (positive = CCW), matching <see cref="ArcSegment"/> semantics.
    /// </summary>
    internal static (Point2 Center, double Radius, double StartAngle, double Sweep) ArcParameters(Topology.TopologyEdge edge)
    {
        switch (edge.SourceEntity)
        {
            case ArcGeometry arc:
                return (arc.Center, arc.Radius, arc.StartAngleRadians, arc.SweepRadians);

            case PolylineGeometry polyline when edge.SegmentIndex < polyline.Segments.Count
                                              && polyline.Segments[edge.SegmentIndex] is ArcSegment arcSegment:
                return (arcSegment.Center, arcSegment.Radius, arcSegment.StartAngleRadians, arcSegment.SweepRadians);

            default:
                throw new InvalidOperationException($"Edge {edge.Id} claims an arc but its source has none.");
        }
    }

    private static Point2 PointAt(Point2 center, double radius, double angle) =>
        new(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
}
