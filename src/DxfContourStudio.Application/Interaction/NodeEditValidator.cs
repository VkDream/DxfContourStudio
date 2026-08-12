#nullable enable

using System;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Interaction;

/// <summary>
/// Structural validation of entity states produced by node-edit previews
/// (D14). The editing engines already refuse degenerate inputs where they can;
/// this is the last guard in front of the document — NaN/Infinity anywhere,
/// zero-length runs, non-positive radii and full-turn/zero sweeps must never
/// reach the document.
/// </summary>
public static class NodeEditValidator
{
    /// <summary>True when every position, length and angle of the entity is finite and non-degenerate.</summary>
    public static bool IsValid(IGeometryEntity entity)
    {
        double tol = GeometryTolerance.Default.ZeroLengthTolerance;
        switch (entity)
        {
            case LineGeometry line:
                return IsFinitePoint(line.P0)
                    && IsFinitePoint(line.P1)
                    && line.Length > tol;

            case CircleGeometry circle:
                return IsFinitePoint(circle.Center)
                    && double.IsFinite(circle.Radius)
                    && circle.Radius > tol;

            case ArcGeometry arc:
                return IsFinitePoint(arc.Center)
                    && double.IsFinite(arc.Radius)
                    && arc.Radius > tol
                    && double.IsFinite(arc.SweepRadians)
                    && Math.Abs(arc.SweepRadians) >= tol
                    && Math.Abs(arc.SweepRadians) < Math.PI * 2;

            case PolylineGeometry poly:
                if (poly.Segments.Count == 0)
                {
                    return false;
                }

                foreach (IPathSegment segment in poly.Segments)
                {
                    if (segment.Length <= tol)
                    {
                        return false; // zero-length run (degenerate vertex pair)
                    }

                    if (!IsFinitePoint(segment.StartPoint) || !IsFinitePoint(segment.EndPoint))
                    {
                        return false;
                    }

                    if (segment is ArcSegment arcSeg)
                    {
                        if (!double.IsFinite(arcSeg.Radius) || arcSeg.Radius <= tol
                            || Math.Abs(arcSeg.SweepRadians) >= Math.PI * 2)
                        {
                            return false;
                        }
                    }
                }

                return true;

            default:
                return false;
        }
    }

    private static bool IsFinitePoint(Point2 p) => double.IsFinite(p.X) && double.IsFinite(p.Y);
}