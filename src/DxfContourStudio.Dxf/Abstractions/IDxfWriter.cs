#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Dxf.Abstractions;

/// <summary>
/// Contract for writing internal geometry back out to a DXF file. Kept
/// library-independent exactly like <see cref="IDxfReader"/>: the only
/// implementation wraps ACadSharp and lives in Dxf.Infrastructure, so the
/// Application layer never sees a third-party writer type.
/// </summary>
public interface IDxfWriter
{
    /// <summary>
    /// Writes the given entities to <paramref name="path"/> using
    /// <paramref name="options"/> and returns the export report. Must not
    /// throw for a user-cancelled / invalid operation; reports errors instead.
    /// </summary>
    DxfExportReport Write(
        string path,
        IReadOnlyList<IGeometryEntity> entities,
        IReadOnlyList<ExportedLayerInfo> layers,
        DxfExportOptions options);
}

/// <summary>Layer definition passed to the exporter (library-free).</summary>
public sealed record ExportedLayerInfo(string Name, bool IsOn, short AciColorIndex, bool IsColorByLayer);
