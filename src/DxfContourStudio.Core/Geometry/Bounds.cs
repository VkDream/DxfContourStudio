namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// An axis-aligned 2D bounding rectangle. Infinite or empty boxes are not
/// supported by construction; a bounds is always finite. Never mutated 鈥?/// produce new instances via <see cref="Union"/> or <see cref="Include"/>.
/// </summary>
public readonly record struct Bounds(double MinX, double MinY, double MaxX, double MaxY)
{
    /// <summary>An "empty" bounds used as the neutral start for accumulation.</summary>
    public static readonly Bounds Empty = new(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);

    /// <summary>Whether the box is the special Empty sentinel.</summary>
    public bool IsEmpty => MinX > MaxX || MinY > MaxY || double.IsInfinity(MinX);

    /// <summary>Width along X (always &gt;= 0 for a non-empty box).</summary>
    public double Width => MaxX - MinX;

    /// <summary>Height along Y (always &gt;= 0 for a non-empty box).</summary>
    public double Height => MaxY - MinY;

    /// <summary>Center point of the box (NaN-safe only for a non-empty box; check <see cref="IsEmpty"/> first).</summary>
    public Point2 Center => new((MinX + MaxX) * 0.5, (MinY + MaxY) * 0.5);

    /// <summary>The diagonal length of the box. Zero for degenerate boxes.</summary>
    public double DiagonalLength => Math.Sqrt(Width * Width + Height * Height);

    /// <summary>
    /// Whether the given point lies inside the box (inclusive boundaries).
    /// </summary>
    public bool Contains(Point2 p)
    {
        return p.X >= MinX && p.X <= MaxX && p.Y >= MinY && p.Y <= MaxY;
    }

    /// <summary>
    /// Whether this box overlaps another including touching edges.
    /// </summary>
    public bool Intersects(Bounds other)
    {
        return MinX <= other.MaxX && MaxX >= other.MinX && MinY <= other.MaxY && MaxY >= other.MinY;
    }

    /// <summary>
    /// Returns the box expanded to also cover <paramref name="p"/>.
    /// </summary>
    public Bounds Include(Point2 p)
    {
        return new Bounds(
            Math.Min(MinX, p.X),
            Math.Min(MinY, p.Y),
            Math.Max(MaxX, p.X),
            Math.Max(MaxY, p.Y));
    }

    /// <summary>Returns the box that also covers <paramref name="other"/>.</summary>
    public Bounds Include(Bounds other)
    {
        if (other.IsEmpty)
        {
            return this;
        }

        return new Bounds(
            Math.Min(MinX, other.MinX),
            Math.Min(MinY, other.MinY),
            Math.Max(MaxX, other.MaxX),
            Math.Max(MaxY, other.MaxY));
    }

    /// <summary>Union of two boxes (empty operand is ignored).</summary>
    public static Bounds Union(Bounds a, Bounds b) => a.IsEmpty ? b : a.Include(b);

    /// <summary>
    /// Whether the given point is strictly inside (exclusive boundaries)
    /// with a small padding, used by containment tests for contours.
    /// </summary>
    public bool ContainsStrictly(Point2 p, double tolerance)
    {
        return p.X > MinX + tolerance && p.X < MaxX - tolerance &&
               p.Y > MinY + tolerance && p.Y < MaxY - tolerance;
    }

    /// <summary>
    /// Grows the box by a fixed amount on every side.
    /// </summary>
    public Bounds Inflated(double amount)
    {
        if (IsEmpty)
        {
            return this;
        }

        return new Bounds(MinX - amount, MinY - amount, MaxX + amount, MaxY + amount);
    }
}