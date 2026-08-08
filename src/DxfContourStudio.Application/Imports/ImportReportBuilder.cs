#nullable enable

using DxfContourStudio.Application.Localization;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Dxf.Abstractions;

namespace DxfContourStudio.Application.Imports;

/// <summary>
/// A single label/value row of the Import Report panel. <see cref="NameKey"/>
/// is the localization key of the label; <see cref="Value"/> is the formatted
/// display text (numbers formatted through <see cref="DisplayFormat"/>).
/// </summary>
public sealed record ReportRow(string NameKey, string Value);

/// <summary>
/// One row of the per-entity-type statistics table (type name + count).
/// <see cref="TypeKey"/> selects the localized entity display name.
/// </summary>
public sealed record EntityStatRow(string TypeKey, int Count);

/// <summary>
/// Builds the rows for the Import Report panel from a
/// <see cref="DxfImportReport"/>. Purely presentational: converts the import
/// outcome into keyed rows the UI binds to. No UI types are used, so the
/// mapping is unit-testable without a window.
/// </summary>
public static class ImportReportBuilder
{
    /// <summary>Builds the label/value rows (file info, counts, time).</summary>
    public static IReadOnlyList<ReportRow> Build(DxfImportReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var rows = new List<ReportRow>
        {
            new(LocalizationKeys.ReportFile, report.File?.FileName ?? "-"),
            new(LocalizationKeys.ReportVersion, report.DxfVersion ?? "-"),
            new(LocalizationKeys.ReportDeclaredUnit, LocalizedUnit(report.DeclaredUnits)),
            new(LocalizationKeys.ReportInterpretedUnit, LocalizedUnit(report.InterpretedUnits)),
            new(LocalizationKeys.ReportLayerCount, DisplayFormat.Count(report.LayerCount)),
            new(LocalizationKeys.ReportTotalEntities, DisplayFormat.Count(report.TotalEntityCount)),
            new(LocalizationKeys.ReportImported, DisplayFormat.Count(report.ImportedCount)),
            new(LocalizationKeys.ReportIgnored, DisplayFormat.Count(report.IgnoredCount)),
            new(LocalizationKeys.ReportUnsupported, DisplayFormat.Count(report.UnsupportedCount)),
            new(LocalizationKeys.ReportWarnings, DisplayFormat.Count(report.WarningCount)),
            new(LocalizationKeys.ReportErrors, DisplayFormat.Count(report.ErrorCount)),
            new(LocalizationKeys.ReportElapsed, DisplayFormat.ElapsedSeconds(report.ImportTimeSeconds)),
        };
        return rows;
    }

    /// <summary>
    /// Builds the per-kind statistics rows, skipping kinds with zero count so
    /// the table only shows what the file actually contains.
    /// </summary>
    public static IReadOnlyList<EntityStatRow> BuildStatistics(DxfImportReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        EntityStatistics s = report.Statistics;
        var rows = new List<EntityStatRow>(10)
        {
            new(LocalizationKeys.EntityLine, s.Line),
            new(LocalizationKeys.EntityArc, s.Arc),
            new(LocalizationKeys.EntityCircle, s.Circle),
            new(LocalizationKeys.EntityLwPolyline, s.LwPolyline),
            new(LocalizationKeys.EntityPolyline, s.Polyline),
            new(LocalizationKeys.EntitySpline, s.Spline),
            new(LocalizationKeys.EntityEllipse, s.Ellipse),
            new(LocalizationKeys.EntityInsert, s.Insert),
            new(LocalizationKeys.EntityTextLike, s.TextLike),
            new(LocalizationKeys.EntityOther, s.Other),
        };
        return rows.Where(r => r.Count > 0).ToList();
    }

    /// <summary>Maps a <see cref="LengthUnit"/> to its localized display key.</summary>
    public static string LocalizedUnit(LengthUnit unit) => unit switch
    {
        LengthUnit.Millimeter => LocalizationKeys.UnitMillimeter,
        LengthUnit.Centimeter => LocalizationKeys.UnitCentimeter,
        LengthUnit.Meter => LocalizationKeys.UnitMeter,
        LengthUnit.Inch => LocalizationKeys.UnitInch,
        LengthUnit.Foot => LocalizationKeys.UnitFoot,
        LengthUnit.Unitless => LocalizationKeys.UnitUnitless,
        _ => LocalizationKeys.UnitUnknown,
    };
}
