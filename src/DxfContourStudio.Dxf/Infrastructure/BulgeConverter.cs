using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Dxf.Infrastructure;

/// <summary>
/// The single place where an LWPOLYLINE bulge is converted into an arc
/// (isolated for unit testing without any DXF library in scope).
///
/// Convention: bulge = tan(θ/4) where θ is the included angle. Positive bulge
/// → arc runs counter-clockwise from the vertex that holds the bulge to the
/// next vertex; negative → clockwise. bulge = ±1 → semicircle.
///
/// Algebra follows the widely-replicated "Bulge to Arc" formulation by Lee Mac
/// (as also used by ezdxf), chosen because it is numerically stable for both
/// tiny and huge bulges and handles the full sweep range including &gt;180°.
/// </summary>
public static class BulgeConverter
{
    /// <summary>
    /// Converts a bulge into canonical arc parameters.
    /// Returns null when the bulge is ~0 (straight segment) or the chord is
    /// degenerate — the caller treats that as a LineSegment.
    /// </summary>
    /// <param name="start">Vertex carrying the bulge.</param>
    /// <param name="end">Next vertex (or first vertex for closing).</param>
    /// <param name="bulge">DXF bulge value.</param>
    /// <returns>
    /// center (mm), radius (mm), start angle (rad), directed sweep (rad, sign
    /// encodes CCW/CW).
    /// </returns>
    public static (Point2 Center, double Radius, double StartAngle, double Sweep)? TryConvert(Point2 start, Point2 end, double bulge)
    {
        if (Math.Abs(bulge) < 1e-12)
        {
            // Straight segment — the caller must handle this case as a line.
            return null;
        }

        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double chord = Math.Sqrt(dx * dx + dy * dy);
        if (chord < 1e-12)
        {
            // Coincident vertices cannot define an arc of finite radius.
            return null;
        }

        // Signed radius (can be negative; polar() multiplies by it so the
        // center lands on the correct side automatically).
        double signedRadius = chord * (1.0 + bulge * bulge) / (4.0 * bulge);

        double chordAngle = Math.Atan2(dy, dx);
        double a = chordAngle + (Math.PI / 2.0 - 2.0 * Math.Atan(bulge));
        Point2 center = new(
            start.X + Math.Cos(a) * signedRadius,
            start.Y + Math.Sin(a) * signedRadius);

        double startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
        double endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X);

        double radius = Math.Abs(signedRadius);
        if (radius <= 0 || double.IsInfinity(radius) || double.IsNaN(radius))
        {
            return null;
        }

        double sweep;
        if (bulge > 0)
        {
            sweep = MathUtil.Normalize0To2Pi(endAngle - startAngle);
        }
        else
        {
            // Clockwise: negative sweep relative to the same center.
            sweep = -MathUtil.Normalize0To2Pi(startAngle - endAngle);
        }

        return (center, radius, startAngle, sweep);
    }
}