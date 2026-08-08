#nullable enable

using ACadSharp.Entities;
using CSMath;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Dxf.Abstractions;

namespace DxfContourStudio.Dxf.Infrastructure;

/// <summary>
/// Maps ACadSharp result entities into the internal geometry model
/// (<see cref="IGeometryEntity"/>). This is the only place allowed to know
/// <c>ACadSharp.*</c> types; everything else consumes only Core geometry.
///
/// Coordinates are converted to millimeters according to the interpreted
/// <see cref="LengthUnit"/>. Unsupported entity kinds are never crashed on;
/// they are counted and reported (see <see cref="EntityStatistics"/>).
/// </summary>
internal sealed class AcadSharpEntityMapper
{
    private readonly double _toMm;
    private long _nextId = 1;

    internal GeometryTolerance Tolerance { get; }
    internal EntityStatistics Statistics { get; } = new();
    internal List<DxfImportMessage> Messages { get; } = [];

    public AcadSharpEntityMapper(LengthUnit interpretedUnit, GeometryTolerance tolerance)
    {
        Tolerance = tolerance;
        _toMm = UnitConverter.ToMillimetersFactor(interpretedUnit);
    }

    /// <summary>
    /// Maps all entities of a document into internal geometry.
    /// Entities are visited in a stable order (by their numeric handle).
    /// </summary>
    public IReadOnlyList<IGeometryEntity> MapAll(IEnumerable<Entity> rawEntities)
    {
        var result = new List<IGeometryEntity>();
        foreach (var raw in rawEntities.OrderBy(e => e.Handle))
        {
            var mapped = MapOne(raw);
            if (mapped is not null)
            {
                result.Add(mapped);
            }
        }

        return result;
    }

    private Point2 ToMm(double x, double y) => new(x * _toMm, y * _toMm);

    private string LayerName(Entity raw) => raw.Layer?.Name ?? string.Empty;

    private bool OnXyPlane(Entity raw)
    {
        return raw switch
        {
            Line l => Math.Abs(l.Normal.Z) > 0.999,
            Arc a => Math.Abs(a.Normal.Z) > 0.999,
            Circle c => Math.Abs(c.Normal.Z) > 0.999,
            _ => true,
        };
    }

    private IGeometryEntity? MapOne(Entity raw)
    {
        switch (raw)
        {
            case Line line:
            {
                Statistics.Line++;
                Point2 p0 = ToMm(line.StartPoint.X, line.StartPoint.Y);
                Point2 p1 = ToMm(line.EndPoint.X, line.EndPoint.Y);
                if (!OnXyPlane(line))
                {
                    Messages.Add(Info($"LINE on layer '{LayerName(line)}' lies in a non-XY plane — projected to XY."));
                }

                if (p0.DistanceTo(p1) <= Tolerance.ZeroLengthTolerance)
                {
                    Messages.Add(Warning($"Zero-length LINE on layer '{LayerName(line)}' — ignored."));
                    return null;
                }

                return new LineGeometry(_nextId++, LayerName(line), p0, p1, !line.IsInvisible);
            }

            case Arc arc:
            {
                Statistics.Arc++;
                if (arc.Radius <= Tolerance.ZeroLengthTolerance)
                {
                    Messages.Add(Warning($"Invalid ARC radius on layer '{LayerName(arc)}' — ignored."));
                    return null;
                }

                if (!OnXyPlane(arc))
                {
                    Messages.Add(Info($"ARC on layer '{LayerName(arc)}' lies in a non-XY plane — projected to XY."));
                }

                // ACadSharp reports ARC angles in radians (see its XML docs).
                // Convert to the internal radians convention: CCW from +X.
                double startRad = arc.StartAngle;
                double sweepRad = ArcSweepRadians(startRad, arc.EndAngle);

                return new ArcGeometry(
                    _nextId++,
                    LayerName(arc),
                    ToMm(arc.Center.X, arc.Center.Y),
                    arc.Radius * _toMm,
                    startRad,
                    sweepRad,
                    isCounterClockwise: true,
                    !arc.IsInvisible);
            }

            case Circle circle:
            {
                Statistics.Circle++;
                if (circle.Radius <= Tolerance.ZeroLengthTolerance)
                {
                    Messages.Add(Warning($"Invalid CIRCLE radius on layer '{LayerName(circle)}' — ignored."));
                    return null;
                }

                if (!OnXyPlane(circle))
                {
                    Messages.Add(Info($"CIRCLE on layer '{LayerName(circle)}' lies in a non-XY plane — projected to XY."));
                }

                return new CircleGeometry(
                    _nextId++,
                    LayerName(circle),
                    ToMm(circle.Center.X, circle.Center.Y),
                    circle.Radius * _toMm,
                    !circle.IsInvisible);
            }

            case LwPolyline lw:
            {
                Statistics.LwPolyline++;
                return MapLwPolyline(lw);
            }

            case Polyline2D poly:
            {
                Statistics.Polyline++;
                return MapPolyline2D(poly);
            }

            case Ellipse:
                Statistics.Ellipse++;
                Messages.Add(Info($"ELLIPSE on layer '{LayerName(raw)}' — unsupported in P1, ignored."));
                return null;

            case Spline:
                Statistics.Spline++;
                Messages.Add(Info($"SPLINE on layer '{LayerName(raw)}' — unsupported in P1, ignored."));
                return null;

            case Insert:
                Statistics.Insert++;
                Messages.Add(Info($"INSERT/BLOCK on layer '{LayerName(raw)}' — unsupported in P1, ignored."));
                return null;

            default:
                Statistics.Other++;
                return null;
        }
    }

    /// <summary>
    /// Returns the CCW sweep in (0, 2π) radians from a normalized start angle
    /// to a normalized end angle, i.e. a positive arc in DXF convention.
    /// ACadSharp angles are already radians.
    /// </summary>
    internal static double ArcSweepRadians(double startRad, double endRad)
    {
        double start = MathUtil.Normalize0To2Pi(startRad);
        double end = MathUtil.Normalize0To2Pi(endRad);
        double sweep = end - start;
        if (sweep <= 0)
        {
            sweep += MathUtil.TwoPi;
        }

        return sweep;
    }

    private IGeometryEntity? MapLwPolyline(LwPolyline lw)
    {
        var verts = lw.Vertices;
        if (verts.Count == 0)
        {
            Messages.Add(Warning($"LWPOLYLINE on layer '{LayerName(lw)}' has no vertices — ignored."));
            return null;
        }

        var segments = BuildSegments(
            verts.Select(v => v.Location),
            verts.Select(v => v.Bulge),
            lw.IsClosed);

        if (segments.Count == 0)
        {
            return null;
        }

        return new PolylineGeometry(_nextId++, LayerName(lw), segments, lw.IsClosed, !lw.IsInvisible);
    }

    private IGeometryEntity? MapPolyline2D(Polyline2D poly)
    {
        var vertices = poly.Vertices.OfType<Vertex2D>().ToList();
        if (vertices.Count == 0)
        {
            Messages.Add(Warning($"POLYLINE on layer '{LayerName(poly)}' has no vertices — ignored."));
            return null;
        }

        var segments = BuildSegments(
            vertices.Select(v => new XY(v.Location.X, v.Location.Y)),
            vertices.Select(v => v.Bulge),
            poly.IsClosed);

        if (segments.Count == 0)
        {
            return null;
        }

        return new PolylineGeometry(_nextId++, LayerName(poly), segments, poly.IsClosed, !poly.IsInvisible);
    }

    /// <summary>
    /// Builds the segment list from an ordered set of vertices and per-vertex
    /// bulges. A closing segment is emitted for a closed chain.
    /// </summary>
    private List<IPathSegment> BuildSegments(IEnumerable<XY> positions, IEnumerable<double> bulges, bool isClosed)
    {
        var pts = positions.ToList();
        var bul = bulges.ToList();
        int n = pts.Count;
        if (n == 0)
        {
            return [];
        }

        // For a closed chain every vertex contributes a segment (last → first).
        // For an open chain the last vertex is just the chain end.
        int segCount = isClosed ? n : Math.Max(n - 1, 0);
        var result = new List<IPathSegment>(segCount);

        for (int i = 0; i < segCount; i++)
        {
            XY v = pts[i];
            XY vn = pts[(i + 1) % n];
            Point2 p = ToMm(v.X, v.Y);
            Point2 q = ToMm(vn.X, vn.Y);

            if (p.DistanceTo(q) <= Tolerance.ZeroLengthTolerance)
            {
                continue;
            }

            double b = i < bul.Count ? bul[i] : 0.0;
            if (Math.Abs(b) < 1e-12)
            {
                result.Add(new LineSegment(p, q));
            }
            else
            {
                var arc = BulgeConverter.TryConvert(p, q, b);
                if (arc is { } a)
                {
                    result.Add(new ArcSegment(a.Center, a.Radius, a.StartAngle, a.Sweep, a.Sweep > 0));
                }
                else
                {
                    result.Add(new LineSegment(p, q));
                }
            }
        }

        return result;
    }

    private DxfImportMessage Warning(string text) => new(DxfImportMessageLevel.Warning, text);
    private DxfImportMessage Info(string text) => new(DxfImportMessageLevel.Info, text);
}