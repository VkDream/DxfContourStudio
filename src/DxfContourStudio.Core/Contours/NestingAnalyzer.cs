#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Contours;

/// <summary>
/// Containment analysis for the closed contours of one drawing.
///
/// Classification is purely nesting-based (never "biggest area = outer"):
///
///   depth 0        → <see cref="ContourRole.Outer"/>
///   depth 1        → <see cref="ContourRole.Hole"/>
///   depth 2        → <see cref="ContourRole.Island"/> (material inside a hole)
///   depth 3, 5, .. → <see cref="ContourRole.Hole"/> again
///   ...
///
/// A contains B when B is entirely inside A (sampled) and A is strictly
/// larger. Orientation (CW/CCW) plays no role in nesting.
/// </summary>
public static class NestingAnalyzer
{
    /// <summary>
    /// Classifies the given closed contours in place (fills Role / Depth /
    /// ParentContourId) and returns them sorted outermost-first (stable).
    /// </summary>
    public static IReadOnlyList<Contour> Analyze(IReadOnlyList<Contour> closedContours)
    {
        var contours = closedContours.ToList();
        var samples = new Dictionary<int, IReadOnlyList<Point2>>(contours.Count);
        foreach (Contour c in contours)
        {
            samples[c.Id] = SamplePoints(c);
        }

        foreach (Contour c in contours)
        {
            double area = c.SignedArea ?? 0.0;
            Contour? parent = null;
            int depth = 0;
            foreach (Contour other in contours)
            {
                if (other.Id == c.Id)
                {
                    continue;
                }

                double otherArea = other.SignedArea ?? 0.0;
                if (otherArea <= area)
                {
                    continue; // only strictly larger boxes can contain us
                }

                // quick reject: our first sample point must lie inside the
                // candidate's bounds before running the polygon test.
                if (!other.Bounds.Contains(samples[c.Id][0]))
                {
                    continue;
                }

                if (ContainsAll(samples[c.Id], samples[other.Id]))
                {
                    depth++;
                    if (parent is null || otherArea < parent.SignedArea)
                    {
                        parent = other;
                    }
                }
            }

            c.Depth = depth;
            c.ParentContourId = parent?.Id;
            // depth 0 → Outer; 1 → Hole; 2 → Island; 3 → Hole; 4 → Island; ...
            c.Role = depth % 2 == 0 ? (depth == 0 ? ContourRole.Outer : ContourRole.Island) : ContourRole.Hole;
        }

        return contours
            .OrderBy(c => c.Depth)
            .ThenBy(c => c.Id)
            .ToList();
    }

    /// <summary>
    /// True when every point of <paramref name="inner"/> lies inside the
    /// polygon sampled from <paramref name="outerSamples"/> (ray casting,
    /// inclusive boundary — points resting on the boundary count as inside).
    /// </summary>
    private static bool ContainsAll(IReadOnlyList<Point2> inner, IReadOnlyList<Point2> outerSamples)
    {
        foreach (Point2 p in inner)
        {
            if (!PointInPolygon(outerSamples, p))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Sampled outline of a contour (dense enough for arcs).</summary>
    private static IReadOnlyList<Point2> SamplePoints(Contour contour)
    {
        var points = new List<Point2>();
        if (contour.IsCircle && contour.CircleCenter is { } center && contour.CircleRadius is { } radius)
        {
            for (int i = 0; i < 8; i++)
            {
                double a = MathUtil.TwoPi * i / 8.0;
                points.Add(new Point2(center.X + radius * Math.Cos(a), center.Y + radius * Math.Sin(a)));
            }

            return points;
        }

        foreach (ChainStep step in contour.Steps)
        {
            Topology.TopologyEdge edge = step.Edge;
            if (edge.SegmentType == GeometryType.Line)
            {
                points.Add(step.Forward ? edge.StartPoint : edge.EndPoint);
                points.Add(step.Forward ? edge.EndPoint : edge.StartPoint);
                continue;
            }

            (Point2 c, double r, double a0, double sweep) = ContourAssembler.ArcParameters(edge);
            for (int i = 0; i < 5; i++)
            {
                double t = i / 4.0;
                double angle = step.Forward ? a0 + sweep * t : a0 + sweep * (1 - t);
                points.Add(new Point2(c.X + r * Math.Cos(angle), c.Y + r * Math.Sin(angle)));
            }
        }

        return points;
    }

    /// <summary>Standard ray-casting point-in-polygon test (crossing parity).</summary>
    private static bool PointInPolygon(IReadOnlyList<Point2> polygon, Point2 p)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Point2 a = polygon[i];
            Point2 b = polygon[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) &&
                p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
