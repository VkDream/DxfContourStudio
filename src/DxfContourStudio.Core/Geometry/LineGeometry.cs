namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// A straight segment between two endpoints, in world coordinates (millimeters).
/// </summary>
public sealed class LineGeometry : GeometryEntityBase
{
    /// <summary>The first endpoint.</summary>
    public Point2 P0 { get; }

    /// <summary>The second endpoint.</summary>
    public Point2 P1 { get; }

    public LineGeometry(long id, string layerName, Point2 p0, Point2 p1, bool isVisible = true)
        : base(id, layerName, isVisible)
    {
        P0 = p0;
        P1 = p1;
    }

    /// <inheritdoc />
    public override GeometryType GeometryType => GeometryType.Line;

    /// <inheritdoc />
    public override Bounds Bounds => new(
        Math.Min(P0.X, P1.X),
        Math.Min(P0.Y, P1.Y),
        Math.Max(P0.X, P1.X),
        Math.Max(P0.Y, P1.Y));

    /// <inheritdoc />
    public override double Length => P0.DistanceTo(P1);

    /// <inheritdoc />
    public override Point2 StartPoint => P0;

    /// <inheritdoc />
    public override Point2 EndPoint => P1;

    /// <inheritdoc />
    public override IGeometryEntity Clone() => new LineGeometry(Id, LayerName, P0, P1, IsVisible);

    /// <inheritdoc />
    public override IGeometryEntity Transformed(Transform2 transform) =>
        new LineGeometry(Id, LayerName, transform.Apply(P0), transform.Apply(P1), IsVisible);

    /// <summary>
    /// Distance from a point to the segment (clamped to the segment ends).
    /// Uses the projection formula and never the infinite-line distance,
    /// so hit testing on finite segments behaves correctly near corners.
    /// </summary>
    public override double DistanceToPoint(Point2 p)
    {
        double dx = P1.X - P0.X;
        double dy = P1.Y - P0.Y;
        double lenSq = dx * dx + dy * dy;

        if (lenSq <= 0)
        {
            // Degenerate zero-length segment: treat as a point.
            return P0.DistanceTo(p);
        }

        // Project p onto the segment; clamp t to [0,1].
        double t = ((p.X - P0.X) * dx + (p.Y - P0.Y) * dy) / lenSq;
        t = Math.Clamp(t, 0.0, 1.0);

        double px = P0.X + t * dx;
        double py = P0.Y + t * dy;
        return Math.Sqrt((p.X - px) * (p.X - px) + (p.Y - py) * (p.Y - py));
    }

    /// <inheritdoc />
    public override Point2 PointAtParameter(double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return new Point2(
            P0.X + (P1.X - P0.X) * t,
            P0.Y + (P1.Y - P0.Y) * t);
    }

    /// <inheritdoc />
    public override Vector2 TangentAt(double t)
    {
        double dx = P1.X - P0.X;
        double dy = P1.Y - P0.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len <= 1e-12)
        {
            return Vector2.Zero;
        }

        return new Vector2(dx / len, dy / len);
    }

    /// <summary>Returns a line with swapped endpoints (same geometry, opposite direction).</summary>
    public LineGeometry Reversed() => new(Id, LayerName, P1, P0, IsVisible);
}