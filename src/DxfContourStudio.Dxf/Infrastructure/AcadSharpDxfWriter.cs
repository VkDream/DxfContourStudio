#nullable enable

using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Tables.Collections;
using ACadSharp.Types.Units;
using CSMath;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Dxf.Abstractions;

namespace DxfContourStudio.Dxf.Infrastructure;

/// <summary>
/// <see cref="IDxfWriter"/> implementation backed by ACadSharp.
///
/// Supported output: LINE, ARC, CIRCLE, LWPOLYLINE (with bulge arcs). Every
/// unsupported internal entity is counted and reported as a warning — never
/// silently dropped.
///
/// The internal geometry is stored in millimeters; the writer converts to
/// <see cref="DxfExportOptions.OutputUnit"/> when writing. The DXF header's
/// $INSUNITS is set accordingly so a re-import interprets coordinates in the
/// same unit and yields the same millimeters.
///
/// The output version is honored by ACadSharp's writer; R12 has no
/// LWPOLYLINE, so polylines are converted to classic POLYLINE/VERTEX runs
/// there.
/// </summary>
public sealed class AcadSharpDxfWriter : IDxfWriter
{
    /// <inheritdoc />
    public DxfExportReport Write(
        string path,
        IReadOnlyList<IGeometryEntity> entities,
        IReadOnlyList<ExportedLayerInfo> layers,
        DxfExportOptions options)
    {
        var report = new DxfExportReport
        {
            OutputFile = path,
            Version = options.Version,
            OutputUnit = options.OutputUnit,
        };
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            double fromMm = UnitConverter.ToMillimetersFactor(options.OutputUnit);
            ACadVersion version = ToAcadVersion(options.Version);
            var doc = new CadDocument(version);

            EnsureLayers(doc, layers);

            foreach (IGeometryEntity e in entities)
            {
                WriteEntity(doc, e, fromMm, report);
            }

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                var writer = new DxfWriter(stream, doc, binary: false);
                try
                {
                    writer.Configuration.WriteAllHeaderVariables = true;
                    doc.Header.InsUnits = ToInsUnits(options.OutputUnit);
                    writer.Write();
                }
                finally
                {
                    writer.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            report.ErrorCount++;
            report.Messages.Add($"DXF export failed: {ex.Message}");
        }

        sw.Stop();
        report.DurationSeconds = sw.Elapsed.TotalSeconds;
        return report;
    }

    private static void WriteEntity(CadDocument doc, IGeometryEntity e, double fromMm, DxfExportReport report)
    {
        switch (e)
        {
            case LineGeometry line:
            {
                var raw = new Line(
                    new XYZ(line.P0.X / fromMm, line.P0.Y / fromMm, 0),
                    new XYZ(line.P1.X / fromMm, line.P1.Y / fromMm, 0))
                {
                    IsInvisible = !e.IsVisible,
                    Layer = doc.Layers[e.LayerName] ?? doc.Layers["0"],
                };
                doc.Entities.Add(raw);
                report.WrittenCount++;
                report.Written.Line++;
                break;
            }

            case CircleGeometry circle:
            {
                var raw = new Circle(
                    new XYZ(circle.Center.X / fromMm, circle.Center.Y / fromMm, 0),
                    circle.Radius / fromMm)
                {
                    IsInvisible = !e.IsVisible,
                    Layer = doc.Layers[e.LayerName] ?? doc.Layers["0"],
                };
                doc.Entities.Add(raw);
                report.WrittenCount++;
                report.Written.Circle++;
                break;
            }

            case ArcGeometry arc:
            {
                var raw = new Arc(
                    new XYZ(arc.Center.X / fromMm, arc.Center.Y / fromMm, 0),
                    arc.Radius / fromMm,
                    arc.StartAngleRadians,
                    arc.EndAngleRadians)
                {
                    IsInvisible = !e.IsVisible,
                    Layer = doc.Layers[e.LayerName] ?? doc.Layers["0"],
                };
                doc.Entities.Add(raw);
                report.WrittenCount++;
                report.Written.Arc++;
                break;
            }

            case PolylineGeometry poly:
            {
                WritePolyline(doc, poly, fromMm, report);
                break;
            }

            default:
                report.SkippedCount++;
                report.Skipped.Other++;
                report.WarningCount++;
                report.Messages.Add($"Unsupported entity #{e.Id} ({e.GeometryType}) skipped.");
                break;
        }
    }

    private static void WritePolyline(CadDocument doc, PolylineGeometry poly, double fromMm, DxfExportReport report)
    {
        if (poly.Segments.Count == 0)
        {
            report.SkippedCount++;
            report.WarningCount++;
            report.Messages.Add($"Polyline #{poly.Id} has no segments — skipped.");
            return;
        }

        // Convert the segment list into vertices + per-vertex bulges. The
        // bulge of a segment is the tangent of a quarter of the arc's sweep.
        var vertices = new List<LwPolyline.Vertex>();
        foreach (var seg in poly.Segments)
        {
            double x = seg.StartPoint.X / fromMm;
            double y = seg.StartPoint.Y / fromMm;
            double bulge = seg is ArcSegment arc
                ? Math.Tan(arc.SweepRadians / 4.0)
                : 0.0;
            vertices.Add(new LwPolyline.Vertex(x, y) { Bulge = bulge });
        }

        if (poly.IsClosed)
        {
            // The closing run back to the start: bulge belongs to the last
            // vertex's outgoing run only when the chain explicitly closes with
            // a segment; a closed polyline in DXF implies the closing edge.
            var first = poly.Segments[0];
            double x0 = first.StartPoint.X / fromMm;
            double y0 = first.StartPoint.Y / fromMm;
            if (vertices.Count == 0 || Math.Abs(vertices[0].Location.X - x0) > 1e-12 ||
                Math.Abs(vertices[0].Location.Y - y0) > 1e-12)
            {
                vertices.Add(new LwPolyline.Vertex(x0, y0));
            }
        }

        var raw = new LwPolyline(vertices)
        {
            IsClosed = poly.IsClosed,
            IsInvisible = !poly.IsVisible,
            Layer = doc.Layers[poly.LayerName] ?? doc.Layers["0"],
        };
        doc.Entities.Add(raw);
        report.WrittenCount++;
        report.Written.LwPolyline++;
    }

    private static ACadVersion ToAcadVersion(DxfExportVersion version) => version switch
    {
        DxfExportVersion.R12 => ACadVersion.AC1009,
        DxfExportVersion.R2000 => ACadVersion.AC1015,
        DxfExportVersion.R2010 => ACadVersion.AC1024,
        _ => ACadVersion.AC1032,
    };

    /// <summary>
    /// Ensures the document's layer table contains the given layers (plus the
    /// mandatory "0" layer). ACadSharp's LayersTable exposes Contains(name),
    /// Add(layer), TryGetValue and the [name] indexer.
    /// </summary>
    private static void EnsureLayers(CadDocument doc, IReadOnlyList<ExportedLayerInfo> layers)
    {
        LayersTable table = doc.Layers;

        if (!table.Contains("0"))
        {
            table.Add(new Layer("0"));
        }

        foreach (ExportedLayerInfo layer in layers)
        {
            if (table.Contains(layer.Name))
            {
                if (table[layer.Name] is { } existing)
                {
                    existing.IsOn = layer.IsOn;
                }

                continue;
            }

            var raw = new Layer(layer.Name)
            {
                IsOn = layer.IsOn,
                Color = new Color(layer.AciColorIndex),
            };
            table.Add(raw);
        }
    }

    /// <summary>Maps the internal unit to the DXF $INSUNITS value.</summary>
    private static UnitsType ToInsUnits(LengthUnit unit) => unit switch
    {
        LengthUnit.Inch => UnitsType.Inches,
        LengthUnit.Foot => UnitsType.Feet,
        LengthUnit.Centimeter => UnitsType.Centimeters,
        LengthUnit.Meter => UnitsType.Meters,
        _ => UnitsType.Millimeters,
    };
}
