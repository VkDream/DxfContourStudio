#nullable enable

using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Selection;

/// <summary>
/// Pure box-selection math (v0.3.0 UX overhaul): given a world-space
/// rectangle the user dragged, decide which entities the box selects.
///
/// Window selection (drag left → right, the rectangle is non-negated in the
/// direction of dragging) selects entities fully contained in the box; the
/// crossing selection (drag right → left) selects everything the box touches.
/// The direction decision is done by the view layer (screen x of start vs
/// end); this class only computes the membership.
///
/// The document's spatial index backs the query (a radius covering the box)
/// so box selection stays cheap for large drawings — no full-list scan.
/// </summary>
public static class BoxSelector
{
    /// <summary>
    /// The ids selected by the given world-space box. <paramref name="crossing"/>
    /// true = crossing selection (box touches the entity), false = window
    /// selection (entity fully inside the box). Order is document order.
    /// </summary>
    public static IReadOnlyList<long> SelectIds(
        CadDocument document, Bounds box, bool crossing)
    {
        ArgumentNullException.ThrowIfNull(document);

        Point2 center = new((box.MinX + box.MaxX) / 2, (box.MinY + box.MaxY) / 2);
        double radius = Math.Max(box.Width, box.Height) / 2 * 1.5 + 1e-9;
        List<long> result = [];
        foreach (IGeometryEntity entity in document.QueryNear(center, radius))
        {
            if (!document.IsVisibleForInteraction(entity))
            {
                continue;
            }

            Bounds b = entity.Bounds;
            if (crossing ? BoxesTouch(b, box) : BoxContains(box, b))
            {
                result.Add(entity.Id);
            }
        }

        return result;
    }

    /// <summary>True when <paramref name="outer"/> fully contains <paramref name="inner"/>.</summary>
    private static bool BoxContains(in Bounds outer, in Bounds inner) =>
        inner.MinX >= outer.MinX && inner.MaxX <= outer.MaxX
        && inner.MinY >= outer.MinY && inner.MaxY <= outer.MaxY;

    /// <summary>True when the two axis-aligned boxes touch or overlap.</summary>
    private static bool BoxesTouch(in Bounds a, in Bounds b) =>
        a.MinX <= b.MaxX + 1e-9 && a.MaxX >= b.MinX - 1e-9
        && a.MinY <= b.MaxY + 1e-9 && a.MaxY >= b.MinY - 1e-9;
}