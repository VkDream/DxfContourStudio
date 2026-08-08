#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Dxf.Abstractions;

/// <summary>
/// Aggregated, categorized statistics about one DXF import.
/// Purely informational — used to drive the Import Report panel and the
/// diagnostics model, never to alter geometry.
/// </summary>
public sealed class DxfImportReport
{
    /// <summary>Source file information (null for non-file sources).</summary>
    public ImportedFileInfo? File { get; init; }

    /// <summary>The DXF database version string (e.g. "AC1032") as read from the header.</summary>
    public string? DxfVersion { get; init; }

    /// <summary>Unit declared by the file (<c>$INSUNITS</c>), before interpretation.</summary>
    public LengthUnit DeclaredUnits { get; init; }

    /// <summary>The unit actually applied during import (never silently assumes mm).</summary>
    public LengthUnit InterpretedUnits { get; init; }

    /// <summary>How many layers were found in the header/tables.</summary>
    public int LayerCount { get; init; }

    /// <summary>Total number of entities read from the source (all kinds).</summary>
    public int TotalEntityCount { get; init; }

    /// <summary>Number of entities successfully mapped to internal geometry.</summary>
    public int ImportedCount { get; init; }

    /// <summary>Number of entities ignored by policy (e.g. unsupported kinds).</summary>
    public int IgnoredCount { get; init; }

    /// <summary>Number of entities recognized but skipped (e.g. TEXT/MTEXT); stored for transparency.</summary>
    public int UnsupportedCount { get; init; }

    /// <summary>Number of warnings collected while mapping (zero-length, bad radius, ...).</summary>
    public int WarningCount { get; set; }

    /// <summary>Number of errors collected while mapping.</summary>
    public int ErrorCount { get; set; }

    /// <summary>Per-kind entity statistics, in DXF terms.</summary>
    public EntityStatistics Statistics { get; set; } = new();

    /// <summary>All collected notes, warnings and errors in order.</summary>
    public List<DxfImportMessage> Messages { get; } = [];

    /// <summary>Time spent in the whole import pipeline (seconds).</summary>
    public double ImportTimeSeconds { get; set; }
}

/// <summary>Counts of entities by DXF kind.</summary>
public sealed class EntityStatistics
{
    public int Line { get; set; }
    public int Arc { get; set; }
    public int Circle { get; set; }
    public int LwPolyline { get; set; }
    public int Polyline { get; set; }
    public int Spline { get; set; }
    public int Ellipse { get; set; }
    public int Insert { get; set; }
    public int TextLike { get; set; }
    public int Other { get; set; }

    /// <summary>Sum of all counters (equals <see cref="DxfImportReport.TotalEntityCount"/>).</summary>
    public int Sum =>
        Line + Arc + Circle + LwPolyline + Polyline + Spline + Ellipse + Insert + TextLike + Other;
}

/// <summary>Severity of an import message.</summary>
public enum DxfImportMessageLevel
{
    Info,
    Warning,
    Error,
}

/// <summary>A message produced during import (unknown unit, unsupported entity, ...).</summary>
public sealed record DxfImportMessage(DxfImportMessageLevel Level, string Text)
{
    public override string ToString() => $"[{Level}] {Text}";
}