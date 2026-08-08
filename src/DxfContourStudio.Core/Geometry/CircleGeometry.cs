namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// A full circle. A circle has no natural start/end point, so
/// <see cref="StartPoint"/> / <see cref="EndPoint"/> report the point at
/// angle 0 (the "east" point); they exist only to satisfy the common
/// geometry contract. Topology must treat a circle as an intrinsically
/// closed contour and must not split it into artificial endpoints.
/// </summary>
public sealed class CircleGeometry : GeometryEntityBase
{
    /// <summary>Center in millimeters.</summary>
    public Point2 Center { get; }

    /// <summary>Radius in millimeters.</summary>
    public double Radius { get; }

    public CircleGeometry(long id, string layerName, Point2 center, double radius, bool isVisible = true)
        : base(id, layerName, isVisible)
    {
        if (radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Circle radius must be non-negative.");
        }

        Center = center;
        Radius = radius;
    }

    /// <inheritdoc />
    public override GeometryType GeometryType => GeometryType.Circle;

    /// <inheritdoc />
    public override double Length => MathUtil.TwoPi * Radius;

    /// <inheritdoc />
    public override Point2 StartPoint => PointAtAngle(0.0);

    /// <inheritdoc />
    public override Point2 EndPoint => StartPoint;

    /// <summary>Point on the circumference at the given angle (radians).</summary>
    public Point2 PointAtAngle(double angle)
    {
        return new Point2(Center.X + Radius * Math.Cos(angle), Center.Y + Radius * Math.Sin(angle));
    }

    /// <inheritdoc />
    public override Point2 PointAtParameter(double t)
    {
        return PointAtAngle(MathUtil.TwoPi * MathUtil.Clamp(t, 0.0, 1.0));
    }

    /// <inheritdoc />
    public override Vector2 TangentAt(double t)
    {
        double angle = MathUtil.TwoPi * MathUtil.Clamp(t, 0.0, 1.0) + Math.PI / 2;
        return new Vector2(Math.Cos(angle), Math.Sin(angle));
    }

    /// <inheritdoc />
    public override Bounds Bounds => new(Center.X - Radius, Center.Y - Radius, Center.X + Radius, Center.Y + Radius);

    /// <inheritdoc />
    public override double DistanceToPoint(Point2 p)
    {
        return Math.Abs(p.DistanceTo(Center) - Radius);
    }

    /// <inheritdoc />
    public override IGeometryEntity Clone()
    {
        return new CircleGeometry(Id, LayerName, Center, Radius, IsVisible);
    }

    /// <inheritdoc />
    public override IGeometryEntity Transformed(Transform2 transform)
    {
        var (sx, sy) = transform.Scale();
        if (Math.Abs(sx - sy) > 1e-9)
        {
            throw new NotSupportedException(
                "Non-uniform scaling of a CircleGeometry is not supported; it would produce an ellipse. " +
                "Tessellate the circle before applying such a transform.");
        }

        return new CircleGeometry(Id, LayerName, transform.Apply(Center), Radius * sx, IsVisible);
    }
}