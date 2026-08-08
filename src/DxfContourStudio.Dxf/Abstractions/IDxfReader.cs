using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Dxf.Abstractions;

/// <summary>
/// Contract for any DXF source. Kept independent from concrete parser
/// libraries (ACadSharp, IxMilia.Dxf, ...). The only implementation today
/// wraps ACadSharp and lives inside the Dxf.Infrastructure namespace, so the
/// rest of the application never cares which library is used — and can swap
/// it later without touching Core, geometry or the UI.
/// </summary>
public interface IDxfReader
{
    /// <summary>
    /// Reads the DXF at <paramref name="path"/> and produces the internal
    /// geometry model plus an import report. The parser's own types never
    /// leak past this boundary.
    /// </summary>
    DxfImportResult Read(string path);
}

/// <summary>
/// Result of a successful DXF import. Contains:
/// <list type="bullet">
///   <item>mapped internal geometry (<see cref="IGeometryEntity"/> list),</item>
///   <item>layer definitions,</item>
///   <item>and the full <see cref="DxfImportReport"/> with all statistics.</item>
/// </list>
/// </summary>
public sealed record DxfImportResult(
    IReadOnlyList<IGeometryEntity> Entities,
    IReadOnlyList<ImportedLayerInfo> Layers,
    DxfImportReport Report)
{
    /// <summary>True when a fatal error prevented geometry from being produced.</summary>
    public bool HasFatalError { get; init; }
}