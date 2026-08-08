namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// A 2D vector with double precision. Distinct from <see cref="Point2"/> so
/// the direction/length concept is explicit and typable.
/// </summary>
public readonly record struct Vector2(double X, double Y)
{
    /// <summary>The zero vector.</summary>
    public static readonly Vector2 Zero = new(0, 0);

    /// <summary>Euclidean length of this vector.</summary>
    public double Length => Math.Sqrt(X * X + Y * Y);

    /// <summary>Squared length (cheaper than <see cref="Length"/>).</summary>
    public double LengthSquared => X * X + Y * Y;

    /// <summary>
    /// Returns a unit vector in the same direction. Degenerate (zero length)
    /// vectors return the zero vector and should be handled by callers via
    /// <see cref="GeometryTolerance.ZeroLengthTolerance"/>.
    /// </summary>
    public Vector2 Normalized()
    {
        double len = Length;
        if (len <= 0)
        {
            return Zero;
        }

        double inv = 1.0 / len;
        return new Vector2(X * inv, Y * inv);
    }

    /// <summary>Vector with the same direction but length clamped to the given radius.</summary>
    public Vector2 WithLength(double radius)
    {
        Vector2 n = Normalized();
        return new Vector2(n.X * radius, n.Y * radius);
    }

    /// <summary>Dot product.</summary>
    public double Dot(Vector2 other) => X * other.X + Y * other.Y;

    /// <summary>2D cross product (scalar, z = x1*y2 - y1*x2).</summary>
    public double Cross(Vector2 other) => X * other.Y - Y * other.X;

    /// <summary>Perpendicular vector rotated +90 degrees (counter-clockwise in math convention).</summary>
    public Vector2 Perpendicular() => new(-Y, X);

    /// <summary>Negates the direction.</summary>
    public Vector2 Negated() => new(-X, -Y);

    /// <summary>Vector addition.</summary>
    public Vector2 Plus(Vector2 other) => new(X + other.X, Y + other.Y);

    /// <summary>Vector subtraction.</summary>
    public Vector2 Minus(Vector2 other) => new(X - other.X, Y - other.Y);

    /// <summary>Scalar multiplication.</summary>
    public Vector2 ScaledBy(double factor) => new(X * factor, Y * factor);

    /// <summary>
    /// Angle in radians from the +X axis in the math convention
    /// (counter-clockwise positive, i.e. Y up). Range (-π, π].
    /// </summary>
    public double AngleRadians()
    {
        return Math.Atan2(Y, X);
    }

    /// <summary>Angle from the +X axis in degrees, math convention. Range (-180, 180].</summary>
    public double AngleDegrees()
    {
        return AngleRadians() * 180.0 / Math.PI;
    }
}