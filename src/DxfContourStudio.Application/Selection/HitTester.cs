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

    /// <summary>Picks the closest visible entity at <paramref name="world"/> within the pixel tolerance.</summary>
    public static IGeometryEntity? PickClosest(CadDocument document, Point2 world, double tolerancePx, Viewport viewport)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(viewport);

        double toleranceWorld = viewport.PixelsToWorld(tolerancePx);

        IGeometryEntity? closest = null;
        double closestDistance = double.MaxValue;
        foreach (IGeometryEntity entity in document.Entities)
        {
            if (!document.IsVisibleForInteraction(entity))
            {
                continue;
            }

            double d = entity.DistanceToPoint(world);
            if (d <= toleranceWorld && d < closestDistance)
            {
                closest = entity;
                closestDistance = d;
            }
        }

        return closest;
    }
}