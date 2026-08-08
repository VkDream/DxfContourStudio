#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Dxf.Abstractions;

/// <summary>
/// A single layer definition imported from a DXF file, kept free of any
/// third-party DXF library type. Color is stored by the generic AutoCAD ACI
/// color index (1-255; 0 means ByBlock, 256 means ByLayer) because the Core
/// layer must not depend on a specific library's color representation.
/// </summary>
public sealed record ImportedLayerInfo(
    string Name,
    bool IsOn,
    bool IsFrozen,
    short AciColorIndex,
    bool IsColorByLayer);

/// <summary>
/// Minimal metadata of the source DXF file that is relevant for the import
/// report and later project persistence.
/// </summary>
public sealed record ImportedFileInfo(
    string FileName,
    string FilePath,
    long FileSizeBytes,
    string? DxfVersionRaw);