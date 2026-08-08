namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// A single elementary path piece of a polyline: either a straight run or an
/// arc run (produced by a bulge). Used to compose
/// <see cref="PolylineGeometry"/>.
///
/// This model (segments instead of a raw point list) exists because a
/// LWPOLYLINE bulge must never be silently treated as a straight line —
/// the arc geometry has to survive into topology and rendering.
/// </summary>
public interface IPathSegment
{
    /// <summary>Start point of the segment, in millimeters.</summary>
    Point2 StartPoint { get; }

    /// <summary>End point of the segment, in millimeters.</summary>
    Point2 EndPoint { get; }

    /// <summary>Path length of the segment in millimeters.</summary>
    double Length { get; }

    /// <summary>The kind of this segment.</summary>
    GeometryType GeometryType { get; }

    /// <summary>Point at normalized parameter t ∈ [0,1].</summary>
    Point2 PointAtParameter(double t);
}

/// <summary>A straight polyline run.</summary>
public readonly record struct LineSegment(Point2 StartPoint, Point2 EndPoint) : IPathSegment
{
    /// <inheritdoc />
    public double Length => StartPoint.DistanceTo(EndPoint);

    /// <inheritdoc />
    public GeometryType GeometryType => GeometryType.Line;

    /// <inheritdoc />
    public Point2 PointAtParameter(double t)
    {
        t = MathUtil.Clamp(t, 0.0, 1.0);
        return new Point2(
            StartPoint.X + (EndPoint.X - StartPoint.X) * t,
            StartPoint.Y + (EndPoint.Y - StartPoint.Y) * t);
    }
}

/// <summary>
/// An arc run of a polyline, in the same canonical semantics as
/// <see cref="ArcGeometry"/> (angles in radians, CCW from +X,
/// sweep signed by <see cref="IsCounterClockwise"/>).
/// </summary>
public readonly record struct ArcSegment(Point2 Center, double Radius, double StartAngleRadians, double SweepRadians, bool IsCounterClockwise) : IPathSegment
{
    /// <inheritdoc />
    public Point2 StartPoint => PointAtAngle(StartAngleRadians);

    /// <inheritdoc />
    public Point2 EndPoint => PointAtAngle(StartAngleRadians + SweepRadians);

    /// <inheritdoc />
    public double Length => Radius * Math.Abs(SweepRadians);

    /// <inheritdoc />
    public GeometryType GeometryType => GeometryType.Arc;

    /// <inheritdoc />
    public Point2 PointAtParameter(double t)
    {
        return PointAtAngle(StartAngleRadians + SweepRadians * MathUtil.Clamp(t, 0.0, 1.0));
    }

    private Point2 PointAtAngle(double angle)
    {
        return new Point2(Center.X + Radius * Math.Cos(angle), Center.Y + Radius * Math.Sin(angle));
    }
}