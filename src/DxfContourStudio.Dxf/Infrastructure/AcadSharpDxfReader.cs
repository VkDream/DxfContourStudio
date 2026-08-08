#nullable enable

using ACadSharp;
using ACadSharp.IO;
using ACadSharp.Tables;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Dxf.Abstractions;

namespace DxfContourStudio.Dxf.Infrastructure;

/// <summary>
/// <see cref="IDxfReader"/> implementation backed by ACadSharp.
///
/// Lives in the Infrastructure namespace on purpose: everything the outside
/// world sees is the internal geometry model (<see cref="IGeometryEntity"/>)
/// and plain data records (layers/report). No <c>ACadSharp.*</c> type ever
/// crosses the <see cref="IDxfReader"/>/<see cref="DxfImportResult"/> boundary.
///
/// Thread-safety: a single read is an independent unit; the reader is not
/// reused across concurrent calls.
/// </summary>
public sealed class AcadSharpDxfReader : IDxfReader
{
    private readonly GeometryTolerance _tolerance;

    /// <summary>Creates a DXF reader that converts coordinates into millimeters.</summary>
    public AcadSharpDxfReader(GeometryTolerance? tolerance = null)
    {
        _tolerance = tolerance ?? GeometryTolerance.Default;
    }

    /// <inheritdoc />
    public DxfImportResult Read(string path)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var messages = new List<DxfImportMessage>();

        CadDocument doc;
        try
        {
            // Reading with a notification handler lets ACadSharp report
            // non-fatal parser problems that we forward into the report.
            var reader = new DxfReader(path, (sender, e) => ForwardNotification(e, messages));
            doc = reader.Read();
        }
        catch (Exception ex)
        {
            // Opening failed entirely => fatal error, no geometry.
            return new DxfImportResult([], [], new DxfImportReport
            {
                File = ImportedFileInfoFor(path),
                Messages = { new DxfImportMessage(DxfImportMessageLevel.Error, $"Could not read DXF file: {ex.Message}") },
                ErrorCount = 1,
            })
            {
                HasFatalError = true,
            };
        }

        sw.Stop();

        var header = doc.Header;
        LengthUnit declared = MapDeclaredUnit(header.InsUnits);
        var interpreted = InterpretUnit(declared, messages);

        var mapper = new AcadSharpEntityMapper(interpreted, _tolerance);
        int rawEntityCount = doc.Entities.Count();
        var geometry = mapper.MapAll(doc.Entities);
        var layers = doc.Layers.Select(MapLayer).ToList();

        var report = new DxfImportReport
        {
            File = ImportedFileInfoFor(path),
            DxfVersion = header.VersionString,
            DeclaredUnits = declared,
            InterpretedUnits = interpreted,
            LayerCount = layers.Count,
            TotalEntityCount = rawEntityCount,
            ImportedCount = geometry.Count,
            WarningCount = 0,
            ErrorCount = 0,
            ImportTimeSeconds = sw.Elapsed.TotalSeconds,
        };

        foreach (var m in messages)
        {
            report.Messages.Add(m);
            if (m.Level == DxfImportMessageLevel.Warning)
            {
                report.WarningCount++;
            }
            else if (m.Level == DxfImportMessageLevel.Error)
            {
                report.ErrorCount++;
            }
        }

        foreach (var m in mapper.Messages)
        {
            report.Messages.Add(m);
            if (m.Level == DxfImportMessageLevel.Warning)
            {
                report.WarningCount++;
            }
            else if (m.Level == DxfImportMessageLevel.Error)
            {
                report.ErrorCount++;
            }
        }

        report.Statistics = mapper.Statistics;

        return new DxfImportResult(geometry, layers, report);
    }

    private static ImportedLayerInfo MapLayer(Layer raw)
    {
        bool isFrozen = (raw.Flags & LayerFlags.Frozen) != 0;
        return new ImportedLayerInfo(
            raw.Name,
            raw.IsOn,
            isFrozen,
            raw.Color.Index,
            raw.Color.IsByLayer);
    }

    /// <summary>
    /// Turns the ACadSharp <c>$INSUNITS</c> value into a <see cref="LengthUnit"/>.
    /// The enum is a DXF value; the Core mapping is unit-value based so we
    /// forward the numeric value.
    /// </summary>
    private static LengthUnit MapDeclaredUnit(object insUnits)
    {
        int dxfValue = insUnits is Enum e ? Convert.ToInt32(e) : 0;
        return UnitConverter.FromDxfInsUnits(dxfValue);
    }

    /// <summary>
    /// Determines the unit actually used by the engine. Unitless files cannot
    /// be measured reliably, so they are assumed as millimeter and reported.
    /// Unknown unit codes degrade to a warning and the working assumption.
    /// </summary>
    private static LengthUnit InterpretUnit(LengthUnit declared, List<DxfImportMessage> messages)
    {
        if (declared == LengthUnit.Unknown)
        {
            messages.Add(new DxfImportMessage(
                DxfImportMessageLevel.Warning,
                "File declares no or an unknown $INSUNITS; assuming millimeters for import."));
            return LengthUnit.Millimeter;
        }

        if (declared == LengthUnit.Unitless)
        {
            messages.Add(new DxfImportMessage(
                DxfImportMessageLevel.Info,
                "File is unitless ($INSUNITS=0); assuming millimeters for import."));
            return LengthUnit.Millimeter;
        }

        return declared;
    }

    private static ImportedFileInfo ImportedFileInfoFor(string path)
    {
        FileInfo fi = new(path);
        return new ImportedFileInfo(fi.Name, fi.FullName, fi.Length, null);
    }

    /// <summary>
    /// Forwards ACadSharp parser notifications into the import report
    /// (non-fatal problems like unsupported group codes).
    /// </summary>
    private static void ForwardNotification(NotificationEventArgs e, List<DxfImportMessage> messages)
    {
        if (string.IsNullOrEmpty(e.Message))
        {
            return;
        }

        var level = e.NotificationType switch
        {
            NotificationType.Error => DxfImportMessageLevel.Error,
            NotificationType.Warning => DxfImportMessageLevel.Warning,
            _ => DxfImportMessageLevel.Info,
        };
        messages.Add(new DxfImportMessage(level, e.Message));
    }
}