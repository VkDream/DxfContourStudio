#nullable enable

using System.Collections.Generic;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Interaction;

/// <summary>
/// Resolves the open endpoint nearest to a click among a candidate entity
/// set, used by the interactive Join tool (D17). Deterministic by
/// construction: distance ties are broken by (entity id, parameter index),
/// never by document iteration order — so hovering the same world position
/// always produces the same endpoint.
///
/// Open-endpoint semantics: Line and Arc expose both ends; an open polyline
/// exposes its first-vertex and last-vertex ends; Circle and closed polylines
/// expose none (they have no free end to attach to).
/// </summary>
public static class JoinEndpointResolver
{
    /// <summary>One open path endpoint together with its owning entity.
    /// <paramref name="ParamIndex"/> is a stable ordinal of the end within the
    /// entity: 0 = start, 1 = end (polyline end = segment count).</summary>
    public readonly record struct OpenEndpoint(long EntityId, int ParamIndex, Point2 Point);

    /// <summary>
    /// Returns the <see cref="OpenEndpoint"/> nearest to <paramref name="world"/>
    /// among <paramref name="candidates"/>, or null when none lies within
    /// <paramref name="tolerance"/>. Equal distances pick the smaller entity
    /// id, then the smaller parameter index.
    /// </summary>
    public static OpenEndpoint? ResolveNearestOpenEndpoint(
        IReadOnlyList<IGeometryEntity> candidates, Point2 world, double tolerance)
    {
        OpenEndpoint? best = null;
        double bestDistance = double.MaxValue;

        foreach (IGeometryEntity entity in candidates)
        {
            foreach (OpenEndpoint end in OpenEndpointsOf(entity))
            {
                double distance = end.Point.DistanceTo(world);
                if (distance > tolerance)
                {
                    continue;
                }

                if (best is null || IsBetter(end, distance, best.Value, bestDistance))
                {
                    best = end;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    /// <summary>The open endpoints an entity exposes (empty for none).</summary>
    private static IEnumerable<OpenEndpoint> OpenEndpointsOf(IGeometryEntity entity)
    {
        switch (entity)
        {
            case LineGeometry line:
                yield return new OpenEndpoint(line.Id, 0, line.StartPoint);
                yield return new OpenEndpoint(line.Id, 1, line.EndPoint);
                break;

            case ArcGeometry arc:
                yield return new OpenEndpoint(arc.Id, 0, arc.StartPoint);
                yield return new OpenEndpoint(arc.Id, 1, arc.EndPoint);
                break;

            case PolylineGeometry polyline when !polyline.IsClosed && polyline.Segments.Count > 0:
                yield return new OpenEndpoint(polyline.Id, 0, polyline.StartPoint);
                yield return new OpenEndpoint(polyline.Id, polyline.Segments.Count, polyline.EndPoint);
                break;
        }
    }

    /// <summary>
    /// Deterministic comparison: closer wins; within 1e-9 mm the smaller
    /// entity id wins, then the smaller parameter index.
    /// </summary>
    private static bool IsBetter(
        OpenEndpoint candidate, double distance,
        OpenEndpoint current, double currentDistance)
    {
        const double Eps = 1e-9;

        if (distance < currentDistance - Eps)
        {
            return true;
        }

        if (distance > currentDistance + Eps)
        {
            return false;
        }

        if (candidate.EntityId != current.EntityId)
        {
            return candidate.EntityId < current.EntityId;
        }

        return candidate.ParamIndex < current.ParamIndex;
    }
}