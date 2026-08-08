namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// A 2D point with double precision in world coordinates.
///
/// This is the domain-side replacement for <c>System.Windows.Point</c> so
/// that the Core layer never depends on WPF.
/// </summary>
public readonly record struct Point2(double X, double Y)
{
    /// <summary>The zero point (0, 0).</summary>
    public static readonly Point2 Origin = new(0, 0);

    /// <summary>
    /// Distance to another point.
    /// </summary>
    public double DistanceTo(Point2 other) => Math.Sqrt(DistanceSquaredTo(other));

    /// <summary>Squared distance to another point (cheaper than <see cref="DistanceTo"/>).</summary>
    public double DistanceSquaredTo(Point2 other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return dx * dx + dy * dy;
    }

    /// <summary>Returns this point translated by a vector.</summary>
    public Point2 Plus(Vector2 v) => new(X + v.X, Y + v.Y);

    /// <summary>Returns this point translated by the negation of a vector.</summary>
    public Point2 Minus(Vector2 v) => new(X - v.X, Y - v.Y);

    /// <summary>Vector from this point to another.</summary>
    public Vector2 VectorTo(Point2 other) => new(other.X - X, other.Y - Y);

    /// <summary>Midpoint between two points.</summary>
    public static Point2 Midpoint(Point2 a, Point2 b) => new((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);

    /// <summary>
    /// Whether this point is within <paramref name="tolerance"/> of <paramref name="other"/>.
    /// Uses squared distance for efficiency so the comparison is robust.
    /// </summary>
    public bool IsCoincident(Point2 other, double tolerance)
    {
        return DistanceSquaredTo(other) <= tolerance * tolerance;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"({X:0.####}, {Y:0.####})";
    }
}