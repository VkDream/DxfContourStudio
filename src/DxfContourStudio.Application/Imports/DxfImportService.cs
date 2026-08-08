#nullable enable

using DxfContourStudio.Application.Documents;
using DxfContourStudio.Dxf.Abstractions;

namespace DxfContourStudio.Application.Imports;

/// <summary>
/// Application-level DXF import. Owns the configured <see cref="IDxfReader"/>
/// and produces a <see cref="CadDocument"/> plus a report the UI can show.
///
/// The reader implementation is injected; the Application layer never touches
/// a concrete parser library, so swapping DXF backends does not leak out of
/// the Dxf project.
/// </summary>
public sealed class DxfImportService
{
    private readonly IDxfReader _reader;

    public DxfImportService(IDxfReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    /// <summary>
    /// Reads <paramref name="path"/>, maps it into geometry and stamps the
    /// result into the supplied <paramref name="document"/>.
    /// </summary>
    public DxfImportOutcome Import(string path, CadDocument document)
    {
        DxfImportResult result;
        try
        {
            result = _reader.Read(path);
        }
        catch (Exception ex)
        {
            document.ReplaceContent([], [], null, null, ex.Message);
            return DxfImportOutcome.Failed(ex.Message);
        }

        var layers = result.Layers
            .Select(l => new LayerState(l.Name, l.IsOn, l.IsFrozen, l.AciColorIndex, l.IsColorByLayer))
            .ToList();

        document.ReplaceContent(
            result.Entities,
            layers,
            result.Report.File?.FilePath,
            BuildSummary(result),
            result.HasFatalError ? "The DXF could not be imported completely. See report for details." : null);

        document.Units = result.Report.InterpretedUnits;

        if (result.HasFatalError)
        {
            return DxfImportOutcome.Failed("The DXF could not be imported completely.");
        }

        return DxfImportOutcome.Ok(result.Report);
    }

    private static string BuildSummary(DxfImportResult result) =>
        $"{result.Report.DeclaredUnits} declared, interpreted as {result.Report.InterpretedUnits}; " +
        $"{result.Report.ImportedCount} of {result.Report.TotalEntityCount} entities imported, " +
        $"{result.Report.WarningCount} warning(s).";
}

/// <summary>The outcome of <see cref="DxfImportService.Import"/>.</summary>
public sealed class DxfImportOutcome
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public DxfImportReport? Report { get; init; }

    public static DxfImportOutcome Ok(DxfImportReport report) => new() { IsSuccess = true, Report = report };
    public static DxfImportOutcome Failed(string message) => new() { IsSuccess = false, ErrorMessage = message };
}