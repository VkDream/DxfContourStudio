namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// A circular arc defined by center, radius, start angle and angular sweep.
///
/// Canonical internal semantics: angles are in radians in the math convention
/// (counter-clockwise from +X, matching a Y-up CAD plane). 
/// <see cref="IsCounterClockwise"/> selects between a CCW (true, the DXF
/// "arc is drawn counterclockwise from start to end") or CW (false) sweep
/// between the same start/end angles.
///
/// A full turn is never stored here — that is a <see cref="CircleGeometry"/>.
/// The import pipeline must detect a full circle and emit a Circle instead
/// of a degenerate 2π arc.
/// </summary>
public sealed class ArcGeometry : GeometryEntityBase
{
    /// <summary>Center of the underlying circle, in millimeters.</summary>
    public Point2 Center { get; }

    /// <summary>Radius, in millimeters. Never negative.</summary>
    public double Radius { get; }

    /// <summary>Start angle in radians, CCW from +X, normalized to [0, 2π).</summary>
    public double StartAngleRadians { get; }

    /// <summary>
    /// Directed angular extent in radians. For CCW arcs it is in (0, 2π);
    /// for CW arcs in (-2π, 0). Never zero.
    /// </summary>
    public double SweepRadians { get; }

    /// <summary>True when the arc runs counter-clockwise.</summary>
    public bool IsCounterClockwise { get; }

    /// <summary>End angle of the directed arc in radians (equal to StartAngleRadians + SweepRadians).</summary>
    public double EndAngleRadians => StartAngleRadians + SweepRadians;

    public ArcGeometry(
        long id,
        string layerName,
        Point2 center,
        double radius,
        double startAngleRadians,
        double sweepRadians,
        bool isCounterClockwise = true,
        bool isVisible = true)
        : base(id, layerName, isVisible)
    {
        if (radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Arc radius must be non-negative.");
        }

        if (Math.Abs(sweepRadians) < 1e-12 || Math.Abs(sweepRadians) >= MathUtil.TwoPi)
        {
            throw new ArgumentOutOfRangeException(nameof(sweepRadians),
                "Arc sweep must be non-zero and strictly smaller than a full turn. Use CircleGeometry for full circles.");
        }

        Center = center;
        Radius = radius;
        StartAngleRadians = MathUtil.Normalize0To2Pi(startAngleRadians);
        SweepRadians = isCounterClockwise
            ? Math.Abs(sweepRadians)
            : -Math.Abs(sweepRadians);
        IsCounterClockwise = isCounterClockwise;
    }

    /// <inheritdoc />
    public override GeometryType GeometryType => GeometryType.Arc;

    /// <inheritdoc />
    public override double Length => Radius * Math.Abs(SweepRadians);

    /// <inheritdoc />
    public override Point2 StartPoint => PointAtAngle(StartAngleRadians);

    /// <inheritdoc />
    public override Point2 EndPoint => PointAtAngle(EndAngleRadians);

    /// <summary>Point on the circle at a given angle (radians).</summary>
    public Point2 PointAtAngle(double angle)
    {
        return new Point2(
            Center.X + Radius * Math.Cos(angle),
            Center.Y + Radius * Math.Sin(angle));
    }

    /// <inheritdoc />
    public override Point2 PointAtParameter(double t)
    {
        double clamped = MathUtil.Clamp(t, 0.0, 1.0);
        return PointAtAngle(StartAngleRadians + SweepRadians * clamped);
    }

    /// <inheritdoc />
    public override Vector2 TangentAt(double t)
    {
        double angle = StartAngleRadians + SweepRadians * MathUtil.Clamp(t, 0.0, 1.0);
        double tangentAngle = angle + (IsCounterClockwise ? Math.PI / 2 : -Math.PI / 2);
        return new Vector2(Math.Cos(tangentAngle), Math.Sin(tangentAngle));
    }

    /// <inheritdoc />
    public override Bounds Bounds => ComputeBounds();

    private Bounds ComputeBounds()
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;

        void Include(Point2 p)
        {
            minX = Math.Min(minX, p.X);
            maxX = Math.Max(maxX, p.X);
            minY = Math.Min(minY, p.Y);
            maxY = Math.Max(maxY, p.Y);
        }

        Include(StartPoint);
        Include(EndPoint);

        // An arc that crosses one of the four axis directions reaches its
        // max/min X or Y exactly there. Only include it when it is crossed.
        for (int q = 0; q < 4; q++)
        {
            if (CoversAngle(Math.PI / 2 * q))
            {
                Include(PointAtAngle(Math.PI / 2 * q));
            }
        }

        return new Bounds(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Whether the given absolute angle is inside the directed arc span.
    /// </summary>
    private bool CoversAngle(double angle)
    {
        double a0 = StartAngleRadians;
        double a1 = EndAngleRadians;
        if (IsCounterClockwise)
        {
            // Sweep >= 0. Handles wrap-around across 0°.
            return IsAngleInCcwSweep(a0, a1, angle);
        }
        else
        {
            // Convert to CCW-equivalent for the same physical arc.
            return IsAngleInCcwSweep(a1, a0, angle);
        }
    }

    /// <summary>
    /// Whether angle ∈ [start, end] travelling counter-clockwise; handles
    /// the case start + sweep crosses the 0°/360° line.
    /// </summary>
    private static bool IsAngleInCcwSweep(double start, double end, double angle)
    {
        double sweep = MathUtil.Normalize0To2Pi(end - start);
        double delta = MathUtil.Normalize0To2Pi(angle - start);
        return delta <= sweep + 1e-12;
    }

    /// <inheritdoc />
    public override double DistanceToPoint(Point2 p)
    {
        double d = p.DistanceTo(Center);

        // Radial distance when the point is angularly inside the sweep range,
        // otherwise the nearest endpoint distance.
        if (d > 1e-12)
        {
            double angle = Math.Atan2(p.Y - Center.Y, p.X - Center.X);
            if (CoversAngle(angle))
            {
                return Math.Abs(d - Radius);
            }
        }

        return Math.Min(p.DistanceTo(StartPoint), p.DistanceTo(EndPoint));
    }

    /// <inheritdoc />
    public override IGeometryEntity Clone()
    {
        return new ArcGeometry(Id, LayerName, Center, Radius, StartAngleRadians, SweepRadians, IsCounterClockwise, IsVisible);
    }

    /// <inheritdoc />
    public override IGeometryEntity Transformed(Transform2 transform)
    {
        Point2 newCenter = transform.Apply(Center);

        // Under any affine transform, the mapping of the arc direction at its
        // start gives the new tangent; a circle stays a circle under
        // similarity transforms (uniform scale, rotation, flip), which covers
        // the transforms this geometry layer uses (Y-flip and unit scaling).
        // For general non-uniform transforms the arc would become an ellipse,
        // which we do not fake here.
        Vector2 startDir = new Vector2(Math.Cos(StartAngleRadians), Math.Sin(StartAngleRadians));
        Vector2 newStartDir = transform.ApplyVector(startDir);

        double newRadius = Radius * newStartDir.Length;
        bool flips = transform.Determinant < 0;

        double newStart = Math.Atan2(newStartDir.Y, newStartDir.X);

        if (!(Math.Abs(transform.Scale().ScaleY - transform.Scale().ScaleX) < 1e-9))
        {
            throw new NotSupportedException(
                "Non-uniform scaling of an ArcGeometry is not supported; it would produce an ellipse. " +
                "Tessellate the arc before applying such a transform.");
        }

        return new ArcGeometry(
            Id, LayerName, newCenter, newRadius, newStart,
            IsCounterClockwise == flips ? SweepRadians : -SweepRadians,
            IsCounterClockwise == flips, IsVisible);
    }
}