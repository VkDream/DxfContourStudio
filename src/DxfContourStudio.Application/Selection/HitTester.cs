#nullable enable

using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Selection;

/// <summary>
/// Pixel-space hit testing on a <see cref="CadDocument"/>.
///
/// Selection tolerance is expressed in screen pixels so that picking feels
/// identical at every zoom level: <see cref="PixelsToWorld"/> converts the
/// fixed pixel tolerance (typically 5–7 px) into the world distance implied
/// by the current <see cref="Viewport"/> scale. This keeps "zoomed out =
/// everything is too easy to click" and "zoomed in = nothing is clickable"
/// from happening.
/// </summary>
public static class HitTester
{
    /// <summary>Default click tolerance used by the UI, in screen pixels.</summary>
    public const int DefaultPickTolerancePx = 6;

    /// <summary>Converts a pixel tolerance into the world distance at the given viewport scale.</summary>
    public static double PixelsToWorld(double pixels, Viewport viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        return viewport.PixelsToWorld(pixels);
    }

    /// <summary>
    /// Picks the closest visible entity at <paramref name="world"/> within the
    /// pixel tolerance. Backed by the document's spatial index (ADR-015):
    /// only entities near the click point are inspected, so picking stays
    /// constant-time for large drawings. The index returns entities whose
    /// exact distance is within the radius; the same distance comparison is
    /// applied again here to guarantee selection of the nearest hit. Exact
    /// ties are broken by lower entity id so the result is deterministic and
    /// independent of the index's cell-bucket visit order.
    /// </summary>
    public static IGeometryEntity? PickClosest(CadDocument document, Point2 world, double tolerancePx, Viewport viewport)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(viewport);

        double toleranceWorld = viewport.PixelsToWorld(tolerancePx);
        List<IGeometryEntity> candidates = document.Pick(world, toleranceWorld);

        IGeometryEntity? closest = null;
        double closestDistance = double.MaxValue;
        foreach (IGeometryEntity entity in candidates)
        {
            double d = entity.DistanceToPoint(world);
            if (d > toleranceWorld)
            {
                continue;
            }

            if (closest is null
                || d < closestDistance - 1e-9
                || (Math.Abs(d - closestDistance) <= 1e-9 && entity.Id < closest.Id))
            {
                closest = entity;
                closestDistance = d;
            }
        }

        return closest;
    }

    /// <summary>
    /// Returns every visible entity at <paramref name="world"/> within the
    /// pixel tolerance, ordered by distance (closest first) with exact ties
    /// broken by ascending entity id. Used by the overlap-cycling gesture:
    /// clicking the same spot repeatedly moves through this list.
    /// </summary>
    public static IReadOnlyList<IGeometryEntity> PickAll(
        CadDocument document, Point2 world, double tolerancePx, Viewport viewport)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(viewport);

        double toleranceWorld = viewport.PixelsToWorld(tolerancePx);
        List<IGeometryEntity> candidates = document.Pick(world, toleranceWorld);
        candidates.Sort((a, b) =>
        {
            double da = a.DistanceToPoint(world);
            double db = b.DistanceToPoint(world);
            if (Math.Abs(da - db) > 1e-9)
            {
                return da.CompareTo(db);
            }

            return a.Id.CompareTo(b.Id);
        });

        return candidates.Where(e => e.DistanceToPoint(world) <= toleranceWorld).ToList();
    }
}