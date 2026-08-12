#nullable enable

using System.Collections.Generic;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Interaction;

/// <summary>
/// Produces the grip descriptors of a single entity. Pure derivation — no
/// WPF, no document mutation — so the Application test suite can pin the
/// exact grip contracts every tool relies on (D13B/D14).
/// </summary>
public static class GripBuilder
{
    /// <summary>Builds the grips of one entity in a stable order.</summary>
    public static IReadOnlyList<GripDescriptor> Build(IGeometryEntity entity)
    {
        var grips = new List<GripDescriptor>();
        long id = entity.Id;
        switch (entity)
        {
            case LineGeometry line:
                grips.Add(new GripDescriptor(id, GripKind.LineStart, line.P0));
                grips.Add(new GripDescriptor(id, GripKind.LineEnd, line.P1));
                break;

            case CircleGeometry circle:
                // Radius grip: the circle at angle 0 (the canonical handle a
                // user grabs to resize). Center grip moves the whole circle.
                grips.Add(new GripDescriptor(id, GripKind.CircleCenter, circle.Center));
                grips.Add(new GripDescriptor(id, GripKind.CircleRadius,
                    new Point2(circle.Center.X + circle.Radius, circle.Center.Y)));
                break;

            case ArcGeometry arc:
                grips.Add(new GripDescriptor(id, GripKind.ArcCenter, arc.Center));
                grips.Add(new GripDescriptor(id, GripKind.ArcStart, arc.StartPoint));
                grips.Add(new GripDescriptor(id, GripKind.ArcEnd, arc.EndPoint));
                break;

            case PolylineGeometry poly:
                for (int i = 0; i < poly.Segments.Count; i++)
                {
                    // Every segment starts at a vertex. For a closed polyline
                    // the last vertex is the first segment's start (the closing
                    // run reuses it), so no duplicate terminal grip appears.
                    grips.Add(new GripDescriptor(id, GripKind.PolylineVertex, poly.Segments[i].StartPoint, i));
                }

                if (!poly.IsClosed && poly.Segments.Count > 0)
                {
                    grips.Add(new GripDescriptor(
                        id, GripKind.PolylineVertex, poly.Segments[^1].EndPoint, poly.Segments.Count));
                }

                break;
        }

        return grips;
    }
}