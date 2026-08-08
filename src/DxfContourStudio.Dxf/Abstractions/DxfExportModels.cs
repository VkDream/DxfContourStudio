#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Dxf.Abstractions;

/// <summary>
/// The target DXF version to write. Mirrors ACadSharp's ACadVersion enum but
/// keeps the rest of the app independent of the concrete writer library.
/// Only versions the current writer can produce are listed.
/// </summary>
public enum DxfExportVersion
{
    /// <summary>R12 (AC1009) — most compatible, no LWPOLYLINE (falls back to POLYLINE).</summary>
    R12,

    /// <summary>R2000 (AC1015) — LWPOLYLINE supported.</summary>
    R2000,

    /// <summary>R2010 (AC1024) — modern default.</summary>
    R2010,

    /// <summary>R2018 (AC1032) — newest supported; default.</summary>
    R2018,
}

/// <summary>
/// Options controlling one DXF export. The exporter never overwrites the
/// source DXF silently: when <see cref="OverwriteSource"/> is false (default)
/// a path equal to the source file is rejected with a warning.
/// </summary>
public sealed class DxfExportOptions
{
    /// <summary>Output DXF version (defaults to <see cref="DxfExportVersion.R2018"/>).</summary>
    public DxfExportVersion Version { get; set; } = DxfExportVersion.R2018;

    /// <summary>
    /// Unit used for coordinates written into the output file. The internal
    /// geometry is always millimeters; the exporter converts back when the
    /// target unit differs. Default: millimeters.
    /// </summary>
    public LengthUnit OutputUnit { get; set; } = LengthUnit.Millimeter;

    /// <summary>
    /// When true the exporter may write over the DXF the document was
    /// imported from. Default false — the exporter refuses (reports an error)
    /// rather than silently destroying the source file.
    /// </summary>
    public bool OverwriteSource { get; set; }
}

/// <summary>
/// Per-kind written / skipped counts of one export, mirroring the import
/// statistics shape so the UI can render one table for both directions.
/// </summary>
public sealed class DxfExportReport
{
    public string? OutputFile { get; init; }

    public int WrittenCount { get; set; }

    public int SkippedCount { get; set; }

    public int WarningCount { get; set; }

    public int ErrorCount { get; set; }

    public DxfExportVersion Version { get; init; }

    public LengthUnit OutputUnit { get; init; }

    public double DurationSeconds { get; set; }

    public EntityStatistics Written { get; } = new();

    public EntityStatistics Skipped { get; } = new();

    public List<string> Messages { get; } = [];
}
