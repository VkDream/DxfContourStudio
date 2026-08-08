#nullable enable

using System;
using System.Collections.Generic;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Diagnostics;

/// <summary>
/// Localization keys used by the diagnostics analyzers. Kept in Core (like the
/// topology keys) so Core stays free of the Application localization table;
/// the Application layer forwards them into its own key table.
/// </summary>
internal static class DiagnosticKeys
{
    public const string ZeroLength = "Diag.ZeroLength";
    public const string VerySmall = "Diag.VerySmall";
    public const string Duplicate = "Diag.Duplicate";
    public const string SelfIntersection = "Diag.SelfIntersection";
    public const string SmallGap = "Diag.SmallGap";
    public const string OpenEnd = "Diag.OpenEnd";
    public const string Branch = "Diag.Branch";
}

/// <summary>
/// Runs the geometry-level diagnostic passes over a list of entities and
/// returns every finding in a stable order. This is the entry point for the
/// "extra" diagnostics (zero length, very small, duplicates, self
/// intersection) that complement the contour/gap pass of the topology
/// analyzer. Pure geometry — no WPF, no DXF.
/// </summary>
public static class GeometryDiagnosticAnalyzer
{
    /// <summary>
    /// Analyzes all entities for degenerate / duplicate / self-intersecting
    /// geometry. <paramref name="tolerance"/> drives every numeric threshold.
    /// </summary>
    public static IReadOnlyList<GeometryDiagnostic> Analyze(
        IReadOnlyList<IGeometryEntity> entities,
        GeometryTolerance? tolerance = null)
    {
        var tol = tolerance ?? GeometryTolerance.Default;
        var diagnostics = new List<GeometryDiagnostic>();

        // Pass 1: per-entity sanity (NaN/Infinity, zero length, very small).
        foreach (IGeometryEntity e in entities)
        {
            if (GeometrySanity.HasInvalidValues(e))
            {
                diagnostics.Add(new GeometryDiagnostic(
                    DiagnosticKind.VerySmall, // placeholder, replaced below when kind known
                    DiagnosticSeverity.Error,
                    "Diag.InvalidGeometry",
                    e.Id,
                    measuredLength: e.Length,
                    detailKey: null));
                continue;
            }

            double len = e.Length;
            if (len <= tol.ZeroLengthTolerance)
            {
                diagnostics.Add(new GeometryDiagnostic(
                    DiagnosticKind.ZeroLength,
                    DiagnosticSeverity.Error,
                    DiagnosticKeys.ZeroLength,
                    e.Id,
                    measuredLength: len));
            }
            else if (len <= tol.SmallGeometryThreshold)
            {
                diagnostics.Add(new GeometryDiagnostic(
                    DiagnosticKind.VerySmall,
                    DiagnosticSeverity.Warning,
                    DiagnosticKeys.VerySmall,
                    e.Id,
                    measuredLength: len));
            }
        }

        // Pass 2: duplicate detection (pairs, tolerance aware).
        diagnostics.AddRange(DuplicateGeometryAnalyzer.FindDuplicates(entities, tol));

        return diagnostics;
    }

    /// <summary>
    /// Runs the self-intersection pass over the closed contours found by the
    /// topology analyzer. <paramref name="contourEntityIds"/> must map a
    /// contour to its ordered entity ids so adjacent segments can be excluded.
    /// </summary>
    public static IReadOnlyList<GeometryDiagnostic> AnalyzeSelfIntersections(
        IReadOnlyList<IReadOnlyList<long>> contoursInOrder,
        Func<long, IGeometryEntity> entityById,
        GeometryTolerance? tolerance = null)
    {
        var tol = tolerance ?? GeometryTolerance.Default;
        return SelfIntersectionAnalyzer.Analyze(contoursInOrder, entityById, tol);
    }
}
