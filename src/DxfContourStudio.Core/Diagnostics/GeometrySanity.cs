#nullable enable

using System;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Diagnostics;

/// <summary>
/// Guards against NaN / Infinity silently propagating from bad geometry into
/// the renderer or the topology engine. Every analyzer and the import mapper
/// should run entities through <see cref="HasInvalidValues"/> before trusting
/// their numbers.
/// </summary>
public static class GeometrySanity
{
    /// <summary>
    /// True when the entity exposes any non-finite coordinate or length, or an
    /// inverted bounds. This is the "do not trust this entity" test.
    /// </summary>
    public static bool HasInvalidValues(IGeometryEntity entity)
    {
        if (!IsFinite(entity.Length) ||
            !IsFinite(entity.Bounds.MinX) || !IsFinite(entity.Bounds.MinY) ||
            !IsFinite(entity.Bounds.MaxX) || !IsFinite(entity.Bounds.MaxY) ||
            !IsFinite(entity.StartPoint.X) || !IsFinite(entity.StartPoint.Y) ||
            !IsFinite(entity.EndPoint.X) || !IsFinite(entity.EndPoint.Y))
        {
            return true;
        }

        if (entity.Bounds.MinX > entity.Bounds.MaxX || entity.Bounds.MinY > entity.Bounds.MaxY)
        {
            return true;
        }

        return entity switch
        {
            CircleGeometry c => !IsFinite(c.Radius) || c.Radius < 0,
            ArcGeometry a => !IsFinite(a.Radius) || a.Radius < 0 ||
                             !IsFinite(a.StartAngleRadians) || !IsFinite(a.SweepRadians),
            PolylineGeometry p => HasInvalidSegments(p),
            _ => false,
        };
    }

    private static bool HasInvalidSegments(PolylineGeometry poly)
    {
        foreach (var s in poly.Segments)
        {
            if (!IsFinite(s.StartPoint.X) || !IsFinite(s.StartPoint.Y) ||
                !IsFinite(s.EndPoint.X) || !IsFinite(s.EndPoint.Y) ||
                !IsFinite(s.Length))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
}
