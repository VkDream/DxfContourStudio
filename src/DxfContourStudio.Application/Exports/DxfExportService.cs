#nullable enable

using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Dxf.Abstractions;

namespace DxfContourStudio.Application.Exports;

/// <summary>
/// Application-level DXF export. Owns the configured <see cref="IDxfWriter"/>
/// and turns a <see cref="CadDocument"/> into an export call — the Application
/// layer never touches a concrete writer library.
///
/// Guard rails:
///  - the source DXF is never overwritten unless
///    <see cref="DxfExportOptions.OverwriteSource"/> is explicitly set
///    (first-version default: refuse);
///  - the output unit defaults to the document's unit (usually mm);
///  - a failed write reports errors instead of throwing to the UI.
/// </summary>
public sealed class DxfExportService
{
    private readonly IDxfWriter _writer;

    public DxfExportService(IDxfWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    /// <summary>
    /// Exports the document's entities to <paramref name="path"/>. Returns the
    /// export report; on a refused overwrite the report carries an error and
    /// no file is written.
    /// </summary>
    public DxfExportReport Export(CadDocument document, string path, DxfExportOptions? options = null)
    {
        var opts = options ?? new DxfExportOptions();

        bool isSourcePath = document.SourceFilePath is { } src &&
                            string.Equals(Path.GetFullPath(src), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
        if (isSourcePath && !opts.OverwriteSource)
        {
            var refused = new DxfExportReport
            {
                OutputFile = path,
                Version = opts.Version,
                OutputUnit = opts.OutputUnit,
                ErrorCount = 1,
            };
            refused.Messages.Add(
                "Refusing to overwrite the source DXF. Choose a different output path or enable overwrite explicitly.");
            return refused;
        }

        var layers = document.Layers
            .Select(l => new ExportedLayerInfo(l.Name, l.IsOn, l.AciColorIndex, l.IsColorByLayer))
            .ToList();

        return _writer.Write(path, document.Entities.ToList(), layers, opts);
    }
}
