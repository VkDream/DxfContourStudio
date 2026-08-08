#nullable enable

using DxfContourStudio.Application.Localization;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Documents;

/// <summary>
/// A single row for the properties panel.
///
/// The label and (optionally) the value are addressed by localization key
/// (<see cref="NameKey"/> / <see cref="ValueKey"/>), not by literal text, so
/// the panel is fully in the UI layer's culture. <see cref="Value"/> is used
/// only when the value is not itself a localized phrase (formatted numbers).
/// <see cref="GroupKey"/> selects the panel section (basic / geometry / bounds
/// / multi) the row is shown under.
/// </summary>
public sealed record PropertyRow(
    string NameKey,
    string GroupKey,
    string Value,
    string? ValueKey = null);

/// <summary>
/// Builds the property-panel rows for a document's current selection, all
/// from plain Core geometry so the structure is unit-testable without a UI.
///
/// Semantics:
/// - no selection → no rows; the panel shows the localized empty state
///   ("未选择图元" + hint) instead,
/// - one selection → basic rows (id/type/layer/visible) + per-kind geometry
///   and boundary rows,
/// - multi selection → "已选择 N 个图元", count and total length.
/// Numbers are formatted only through <see cref="DisplayFormat"/>.
/// </summary>
public static class EntityPropertyBuilder
{
    /// <summary>Builds the rows to display for the given selection.</summary>
    public static IReadOnlyList<PropertyRow> Build(CadDocument document, IReadOnlyCollection<IGeometryEntity>? selected)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (selected is null || selected.Count == 0)
        {
            // The empty-selection case: the panel body shows the localized
            // empty-state text; no document summary is rendered there (file
            // info lives in the Import Report tab).
            return [];
        }

        return selected.Count == 1 ? BuildSingle(selected.First()) : BuildMulti(selected);
    }

    private static IReadOnlyList<PropertyRow> BuildSingle(IGeometryEntity entity)
    {
        var rows = new List<PropertyRow>
        {
            new(LocalizationKeys.PropertyId, LocalizationKeys.GroupBasic, DisplayFormat.Count(entity.Id)),
            new(LocalizationKeys.PropertyType, LocalizationKeys.GroupBasic, "", TypeValueKey(entity)),
            new(LocalizationKeys.PropertyLayer, LocalizationKeys.GroupBasic, entity.LayerName),
            new(LocalizationKeys.PropertyVisible, LocalizationKeys.GroupBasic, "", entity.IsVisible ? LocalizationKeys.CommonYes : LocalizationKeys.CommonNo),
        };

        switch (entity)
        {
            case LineGeometry line:
                AddGeometry(rows, LocalizationKeys.PropertyLength, line.Length);
                AddBounds(rows, LocalizationKeys.PropertyStartX, LocalizationKeys.PropertyStartY, line.P0);
                AddBounds(rows, LocalizationKeys.PropertyEndX, LocalizationKeys.PropertyEndY, line.P1);
                break;

            case CircleGeometry circle:
                AddGeometry(rows, LocalizationKeys.PropertyRadius, circle.Radius);
                AddGeometry(rows, LocalizationKeys.PropertyDiameter, 2 * circle.Radius);
                AddGeometry(rows, LocalizationKeys.PropertyCircumference, circle.Length);
                AddBounds(rows, LocalizationKeys.PropertyCenterX, LocalizationKeys.PropertyCenterY, circle.Center);
                break;

            case ArcGeometry arc:
                AddGeometry(rows, LocalizationKeys.PropertyRadius, arc.Radius);
                AddGeometry(rows, LocalizationKeys.PropertyArcLength, arc.Length);
                AddGeometryAngle(rows, LocalizationKeys.PropertyStartAngle, arc.StartAngleRadians);
                AddGeometryAngle(rows, LocalizationKeys.PropertyEndAngle, arc.EndAngleRadians);
                AddGeometryAngle(rows, LocalizationKeys.PropertySweepAngle, Math.Abs(arc.SweepRadians));
                AddBounds(rows, LocalizationKeys.PropertyCenterX, LocalizationKeys.PropertyCenterY, arc.Center);
                break;

            case PolylineGeometry poly:
                AddGeometry(rows, LocalizationKeys.PropertyLength, poly.Length);
                rows.Add(new PropertyRow(
                    LocalizationKeys.PropertyClosed,
                    LocalizationKeys.GroupGeometry,
                    "",
                    poly.IsClosed ? LocalizationKeys.CommonYes : LocalizationKeys.CommonNo));
                AddGeometry(rows, LocalizationKeys.PropertySegments, poly.Segments.Count);
                break;
        }

        return rows;
    }

    private static IReadOnlyList<PropertyRow> BuildMulti(IReadOnlyCollection<IGeometryEntity> selected)
    {
        double totalLength = selected.Sum(e => e.Length);
        var types = selected
            .Select(e => e.GeometryType)
            .Distinct()
            .OrderBy(t => t)
            .Select(TypeValueKey)
            .ToList();
        // The summary rows live in the "multi" group (汇总) so the panel
        // shows a dedicated section for the whole selection.
        return
        [
            new(LocalizationKeys.PropertyCount, LocalizationKeys.GroupMulti, DisplayFormat.Count(selected.Count)),
            new(LocalizationKeys.PropertyTotalLength, LocalizationKeys.GroupMulti, DisplayFormat.Length(totalLength)),
            new(LocalizationKeys.PropertyType, LocalizationKeys.GroupMulti, "", string.Join(", ", types)),
        ];
    }

    private static string TypeValueKey(IGeometryEntity entity) => TypeValueKey(entity.GeometryType);

    private static string TypeValueKey(GeometryType type) => type switch
    {
        GeometryType.Line => LocalizationKeys.TypeLine,
        GeometryType.Arc => LocalizationKeys.TypeArc,
        GeometryType.Circle => LocalizationKeys.TypeCircle,
        GeometryType.Polyline => LocalizationKeys.TypePolyline,
        _ => LocalizationKeys.EntityOther,
    };

    private static void AddGeometry(ICollection<PropertyRow> rows, string nameKey, double value) =>
        rows.Add(new PropertyRow(nameKey, LocalizationKeys.GroupGeometry, DisplayFormat.Length(value)));

    private static void AddGeometryAngle(ICollection<PropertyRow> rows, string nameKey, double radians) =>
        rows.Add(new PropertyRow(nameKey, LocalizationKeys.GroupGeometry, DisplayFormat.AngleDegrees(radians)));

    private static void AddBounds(ICollection<PropertyRow> rows, string xKey, string yKey, Point2 point)
    {
        rows.Add(new PropertyRow(xKey, LocalizationKeys.GroupBounds, DisplayFormat.Coordinate(point.X)));
        rows.Add(new PropertyRow(yKey, LocalizationKeys.GroupBounds, DisplayFormat.Coordinate(point.Y)));
    }
}