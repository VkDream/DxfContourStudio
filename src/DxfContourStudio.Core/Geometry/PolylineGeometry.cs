namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// A polyline composed of a sequence of <see cref="IPathSegment"/> entries,
/// optionally closed. When closed, the implicit closing run from the last
/// vertex back to the first is also part of the outline.
///
/// This type is segment-based (each entry is either a <see cref="LineSegment"/>
/// or an <see cref="ArcSegment"/>) precisely so that LWPOLYLINE bulges are
/// never silently treated as straight lines.
/// </summary>
public sealed class PolylineGeometry : GeometryEntityBase
{
    /// <summary>The ordered segments composing the polyline.</summary>
    public IReadOnlyList<IPathSegment> Segments { get; }

    /// <summary>Whether the polyline closes back to its first vertex.</summary>
    public bool IsClosed { get; }

    public PolylineGeometry(long id, string layerName, IReadOnlyList<IPathSegment> segments, bool isClosed, bool isVisible = true)
        : base(id, layerName, isVisible)
    {
        Segments = segments ?? throw new ArgumentNullException(nameof(segments));
        IsClosed = isClosed;
    }

    /// <inheritdoc />
    public override GeometryType GeometryType => GeometryType.Polyline;

    /// <inheritdoc />
    public override double Length
    {
        get
        {
            double sum = 0;
            foreach (var s in Segments)
            {
                sum += s.Length;
            }

            return sum;
        }
    }

    /// <inheritdoc />
    public override Point2 StartPoint => Segments.Count > 0 ? Segments[0].StartPoint : Point2.Origin;

    /// <inheritdoc />
    public override Point2 EndPoint
    {
        get
        {
            if (Segments.Count == 0)
            {
                return Point2.Origin;
            }

            return IsClosed ? Segments[0].StartPoint : Segments[^1].EndPoint;
        }
    }

    private double TotalLength => Length;

    /// <inheritdoc />
    public override Bounds Bounds
    {
        get
        {
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;

        void Include(Point2 p)
        {
            minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
            minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
        }

        foreach (var s in Segments)
        {
            Include(s.StartPoint);
            Include(s.EndPoint);

            if (s is ArcSegment arc)
            {
                // An arc run can curve past the axis extremes of its chord.
                for (int q = 0; q < 4; q++)
                {
                    double extremum = Math.PI / 2 * q;
                    if (ArcCoversAngle(arc, extremum))
                    {
                        Include(new Point2(
                            arc.Center.X + arc.Radius * Math.Cos(extremum),
                            arc.Center.Y + arc.Radius * Math.Sin(extremum)));
                    }
                }
            }
        }

        return new Bounds(minX, minY, maxX, maxY);
        }
    }

    private static bool ArcCoversAngle(ArcSegment arc, double angle)
    {
        double sweep = MathUtil.Normalize0To2Pi(Math.Abs(arc.SweepRadians));
        double delta = MathUtil.Normalize0To2Pi(angle - arc.StartAngleRadians);
        return delta <= sweep + 1e-12;
    }

    /// <inheritdoc />
    public override double DistanceToPoint(Point2 p)
    {
        double best = double.MaxValue;
        foreach (var s in Segments)
        {
            best = Math.Min(best, SegmentDistance(s, p));
        }

        return best;
    }

    private static double SegmentDistance(IPathSegment s, Point2 p)
    {
        if (s is LineSegment line)
        {
            return DistanceToLine(line, p);
        }

        if (s is ArcSegment arc)
        {
            double d = p.DistanceTo(arc.Center);
            if (d > 1e-12)
            {
                double angle = Math.Atan2(p.Y - arc.Center.Y, p.X - arc.Center.X);
                if (ArcCoversAngle(arc, angle))
                {
                    return Math.Abs(d - arc.Radius);
                }
            }

            return Math.Min(p.DistanceTo(arc.StartPoint), p.DistanceTo(arc.EndPoint));
        }

        return double.MaxValue;
    }

    private static double DistanceToLine(LineSegment l, Point2 p)
    {
        double dx = l.EndPoint.X - l.StartPoint.X;
        double dy = l.EndPoint.Y - l.StartPoint.Y;
        double lenSq = dx * dx + dy * dy;
        if (lenSq <= 0)
        {
            return p.DistanceTo(l.StartPoint);
        }

        double t = ((p.X - l.StartPoint.X) * dx + (p.Y - l.StartPoint.Y) * dy) / lenSq;
        t = MathUtil.Clamp(t, 0.0, 1.0);

        double px = l.StartPoint.X + t * dx;
        double py = l.StartPoint.Y + t * dy;
        return Math.Sqrt((p.X - px) * (p.X - px) + (p.Y - py) * (p.Y - py));
    }

    /// <inheritdoc />
    public override Point2 PointAtParameter(double t)
    {
        if (Segments.Count == 0)
        {
            return Point2.Origin;
        }

        if (t >= 1.0)
        {
            return EndPoint;
        }

        double total = TotalLength;
        double target = total * MathUtil.Clamp(t, 0.0, 1.0);
        double acc = 0;
        foreach (var s in Segments)
        {
            double next = acc + s.Length;
            if (next > 0 && target <= next)
            {
                double local = (target - acc) / (next - acc);
                return s.PointAtParameter(local);
            }

            acc = next;
        }

        return EndPoint;
    }

    /// <inheritdoc />
    public override Vector2 TangentAt(double t)
    {
        if (Segments.Count == 0)
        {
            return Vector2.Zero;
        }

        double total = TotalLength;
        double target = total * MathUtil.Clamp(t, 0.0, 1.0);
        double acc = 0;
        foreach (var s in Segments)
        {
            if (target <= acc + s.Length)
            {
                return SegmentTangent(s);
            }

            acc += s.Length;
        }

        return SegmentTangent(Segments[^1]);
    }

    private static Vector2 SegmentTangent(IPathSegment s)
    {
        if (s is LineSegment line)
        {
            double dx = line.EndPoint.X - line.StartPoint.X;
            double dy = line.EndPoint.Y - line.StartPoint.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            return len > 1e-12 ? new Vector2(dx / len, dy / len) : Vector2.Zero;
        }

        if (s is ArcSegment arc)
        {
            double tangentAngle = arc.StartAngleRadians + (arc.IsCounterClockwise ? Math.PI / 2 : -Math.PI / 2);
            return new Vector2(Math.Cos(tangentAngle), Math.Sin(tangentAngle));
        }

        return Vector2.Zero;
    }

    /// <inheritdoc />
    public override IGeometryEntity Clone()
    {
        return new PolylineGeometry(Id, LayerName, Segments.ToArray(), IsClosed, IsVisible);
    }

    /// <inheritdoc />
    public override IGeometryEntity Transformed(Transform2 transform)
    {
        var transformed = new List<IPathSegment>(Segments.Count);
        foreach (var s in Segments)
        {
            if (s is LineSegment line)
            {
                transformed.Add(new LineSegment(transform.Apply(line.StartPoint), transform.Apply(line.EndPoint)));
            }
            else if (s is ArcSegment arc)
            {
                transformed.Add(new ArcSegment(
                    transform.Apply(arc.Center),
                    arc.Radius,
                    arc.StartAngleRadians,
                    arc.SweepRadians,
                    arc.IsCounterClockwise));
            }
            else
            {
                throw new NotSupportedException($"Segment type '{s.GetType().Name}' is not supported by Transformed.");
            }
        }

        return new PolylineGeometry(Id, LayerName, transformed, IsClosed, IsVisible);
    }
}