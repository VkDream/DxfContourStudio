namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// A compilation of the low-level math used throughout the geometry engine.
/// Kept as plain static functions so algorithms read as self-evident math,
/// without hidden dependencies on WPF or System.Windows.
/// </summary>
public static class MathUtil
{
    /// <summary>Degrees-to-radians conversion.</summary>
    public const double Deg2Rad = Math.PI / 180.0;

    /// <summary>Radians-to-degrees conversion.</summary>
    public const double Rad2Deg = 180.0 / Math.PI;

    /// <summary>Two pi.</summary>
    public const double TwoPi = 2.0 * Math.PI;

    /// <summary>
    /// Normalizes an angle in radians into the half-open range [0, 2π).
    /// Used for arc sweeps where the canonical start is 0 and positive
    /// sweep goes counter-clockwise.
    /// </summary>
    public static double Normalize0To2Pi(double angle)
    {
        double a = angle % TwoPi;
        if (a < 0)
        {
            a += TwoPi;
        }

        return a;
    }

    /// <summary>
    /// Normalizes a signed angle to the range (-π, π]. Used to compare
    /// two direction differences without 0°-jumps.
    /// </summary>
    public static double NormalizeSignedPi(double angle)
    {
        double a = Normalize0To2Pi(angle);
        if (a > Math.PI)
        {
            a -= TwoPi;
        }

        return a;
    }

/// <summary>
    /// Smallest angular difference between two angles (radians),
    /// always in [0, π]. Well-defined across the 0°/360° boundary.
    /// </summary>
    public static double AngularDifference(double a, double b)
    {
        return Math.Abs(NormalizeSignedPi(a - b));
    }

    /// <summary>
    /// Clamps <paramref name="value"/> into [<paramref name="min"/>, <paramref name="max"/>].
    /// </summary>
    public static double Clamp(double value, double min, double max)
    {
        return value < min ? min : value > max ? max : value;
    }

    /// <summary>
    /// Linear interpolation between <paramref name="a"/> and <paramref name="b"/>
    /// with parameter <paramref name="t"/> in [0, 1].
    /// </summary>
    public static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
    }

    /// <summary>
    /// The area (shoelace) of a simple polygon, with positive sign when the
    /// vertices are wound counter-clockwise (Y-up math convention). Pass the
    /// vertices in order.
    /// </summary>
    public static double SignedArea2(IReadOnlyList<Point2> points)
    {
        double area = 0;
        for (int i = 0; i < points.Count; i++)
        {
            Point2 p = points[i];
            Point2 q = points[(i + 1) % points.Count];
            area += p.X * q.Y - q.X * p.Y;
        }

        return area * 0.5;
    }

    /// <summary>
    /// Whether the given points are counter-clockwise in the math (Y-up) convention.
    /// </summary>
    public static bool IsCounterClockwise(IReadOnlyList<Point2> points)
    {
        return SignedArea2(points) > 0;
    }
}