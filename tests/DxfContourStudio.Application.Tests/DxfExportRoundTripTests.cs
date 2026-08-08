#nullable enable

using System.IO;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Exports;
using DxfContourStudio.Application.Imports;
using DxfContourStudio.Core.Contours;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Dxf.Abstractions;
using DxfContourStudio.Dxf.Infrastructure;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Round-trip tests for the clean DXF export path:
/// DXF 鈫?import 鈫?repair/analyze 鈫?export cleaned DXF 鈫?re-import 鈫?verify.
/// The whole cycle stays in the temp directory; nothing is written next to
/// the source files.
/// </summary>
public class DxfExportRoundTripTests
{
    private static string SamplePath(string relative) =>
        Path.Combine(AppContext.BaseDirectory, relative);

    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), $"dcs-export-{Guid.NewGuid():N}");

    private static CadDocument Import(string path)
    {
        // The file may be transiently held by the OS (AV/indexer) right after
        // the writer closes; retry briefly before failing the test.
        DxfImportOutcome? outcome = null;
        var doc = new CadDocument();
        var service = new DxfImportService(new AcadSharpDxfReader());
        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                outcome = service.Import(path, doc);
                break;
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
        }

        Assert.NotNull(outcome);
        Assert.True(outcome.IsSuccess);
        return doc;
    }

    private static (DxfExportReport Report, string Path) Export(CadDocument doc, string dir, string name, DxfExportOptions? options = null)
    {
        string path = Path.Combine(dir, name);
        var service = new DxfExportService(new AcadSharpDxfWriter());
        DxfExportReport report = service.Export(doc, path, options);
        foreach (string m in report.Messages)
        {
            Console.WriteLine($"[export-diag] {m}");
        }

        Assert.Equal(0, report.ErrorCount);
        Assert.Equal(0, report.WarningCount);
        Assert.True(File.Exists(path), $"expected {path} to exist");
        return (report, path);
    }

    [Fact]
    public void SmallGap_RepairExportReimport_OpenChainGapClosed()
    {
        string dir = TempDir();
        Directory.CreateDirectory(dir);
        try
        {
            CadDocument doc = Import(SamplePath("testdata/dxf/small_gap_003.dxf"));
            var analysis = ContourAnalyzer.Analyze(doc.Entities.ToList());
            GapDiagnostic gap = Assert.Single(analysis.Diagnostics, d => d.Kind == GapKind.SmallGap);
            Assert.Equal(0.030, gap.Distance, 9);
            Assert.True(gap.HasDistance);

            // Correct topology contract: this file is an OPEN CHAIN with an
            // internal 0.030 mm gap — NOT a contour that should close.
            Assert.Equal(0, analysis.ClosedCount);
            Assert.Equal(2, analysis.OpenCount);

            // Repair the gap, then export the cleaned geometry.
            var history = new DxfContourStudio.Application.Commands.CommandHistory();
            history.Execute(new DxfContourStudio.Application.Commands.RepairGapCommand(doc, gap));
            ContourAnalysisResult repaired = ContourAnalyzer.Analyze(doc.Entities.ToList());
            Assert.Equal(0, repaired.SmallGapCount);
            // After repair the two runs join into ONE continuous open chain.
            Assert.Equal(1, repaired.OpenCount);
            Assert.Equal(0, repaired.ClosedCount);

            (DxfExportReport report, string outPath) = Export(doc, dir, "small_gap_003_cleaned.dxf");
            Assert.Equal(2, report.WrittenCount);
            Assert.Equal(0, report.SkippedCount);

            // Re-import the cleaned file: the gap must be gone, chain stays open.
            CadDocument reloaded = Import(outPath);
            ContourAnalysisResult reanalysis = ContourAnalyzer.Analyze(reloaded.Entities.ToList());
            Assert.Equal(0, reanalysis.SmallGapCount);
            Assert.Equal(1, reanalysis.OpenCount);
            Assert.Equal(0, reanalysis.ClosedCount);

            // The endpoints sit at the midpoint.
            var l0 = Assert.IsType<LineGeometry>(reloaded.Entities[0]);
            var l1 = Assert.IsType<LineGeometry>(reloaded.Entities[1]);
            Assert.Equal(100.015, l0.P1.X, 4);
            Assert.Equal(100.015, l1.P0.X, 4);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { Thread.Sleep(200); try { Directory.Delete(dir, recursive: true); } catch { } }
        }
    }

    [Fact]
    public void RectangleGap_RepairExportReimport_StaysClosed()
    {
        string dir = TempDir();
        Directory.CreateDirectory(dir);
        try
        {
            CadDocument doc = Import(SamplePath("testdata/dxf/rectangle_gap_003.dxf"));
            var analysis = ContourAnalyzer.Analyze(doc.Entities.ToList());
            Assert.Equal(0, analysis.ClosedCount);
            GapDiagnostic gap = Assert.Single(analysis.Diagnostics, d => d.Kind == GapKind.SmallGap);
            Assert.Equal(0.030, gap.Distance, 9);

            var history = new DxfContourStudio.Application.Commands.CommandHistory();
            history.Execute(new DxfContourStudio.Application.Commands.RepairGapCommand(doc, gap));
            Assert.Equal(1, ContourAnalyzer.Analyze(doc.Entities.ToList()).ClosedCount);

            (DxfExportReport report, string outPath) = Export(doc, dir, "rectangle_gap_003_cleaned.dxf");
            Assert.Equal(4, report.WrittenCount);

            CadDocument reloaded = Import(outPath);
            ContourAnalysisResult reanalysis = ContourAnalyzer.Analyze(reloaded.Entities.ToList());
            Assert.Equal(1, reanalysis.ClosedCount);
            Assert.Equal(0, reanalysis.OpenCount);
            Assert.Equal(0, reanalysis.SmallGapCount);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { Thread.Sleep(200); try { Directory.Delete(dir, recursive: true); } catch { } }
        }
    }

    [Fact]
    public void OuterHole_ExportReimport_PreservesNesting()
    {
        string dir = TempDir();
        Directory.CreateDirectory(dir);
        try
        {
            CadDocument doc = Import(SamplePath("testdata/dxf/outer_hole.dxf"));
            ContourAnalysisResult before = ContourAnalyzer.Analyze(doc.Entities.ToList());
            Assert.Equal(1, before.OuterCount);
            Assert.Equal(1, before.HoleCount);

            (_, string outPath) = Export(doc, dir, "outer_hole_cleaned.dxf");

            CadDocument reloaded = Import(outPath);
            ContourAnalysisResult after = ContourAnalyzer.Analyze(reloaded.Entities.ToList());
            Assert.Equal(1, after.OuterCount);
            Assert.Equal(1, after.HoleCount);

            Assert.Equal(doc.OverallBounds!.Value.MinX, reloaded.OverallBounds!.Value.MinX, 4);
            Assert.Equal(doc.OverallBounds!.Value.MaxX, reloaded.OverallBounds!.Value.MaxX, 4);
            Assert.Equal(doc.OverallBounds!.Value.MinY, reloaded.OverallBounds!.Value.MinY, 4);
            Assert.Equal(doc.OverallBounds!.Value.MaxY, reloaded.OverallBounds!.Value.MaxY, 4);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { Thread.Sleep(200); try { Directory.Delete(dir, recursive: true); } catch { } }
        }
    }

    [Fact]
    public void BasicScene_ExportReimport_GeometryPrecisionPreserved()
    {
        string dir = TempDir();
        Directory.CreateDirectory(dir);
        try
        {
            CadDocument doc = Import(SamplePath("testdata/dxf/basic-scene.dxf"));
            (_, string outPath) = Export(doc, dir, "basic-scene_cleaned.dxf");

            CadDocument reloaded = Import(outPath);
            Assert.Equal(doc.Entities.Count, reloaded.Entities.Count);

            var before = doc.Entities.OfType<LineGeometry>().First();
            var after = reloaded.Entities.OfType<LineGeometry>().First();
            Assert.Equal(before.Length, after.Length, 6);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { Thread.Sleep(200); try { Directory.Delete(dir, recursive: true); } catch { } }
        }
    }

    [Fact]
    public void Export_ToSourcePath_IsRefusedByDefault()
    {
        string dir = TempDir();
        Directory.CreateDirectory(dir);
        try
        {
            CadDocument doc = Import(SamplePath("testdata/dxf/rectangle_lines.dxf"));
            var service = new DxfExportService(new AcadSharpDxfWriter());
            DxfExportReport report = service.Export(doc, doc.SourceFilePath!);
            Assert.Equal(1, report.ErrorCount);
            Assert.Contains("Refusing to overwrite", report.Messages[0]);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { Thread.Sleep(200); try { Directory.Delete(dir, recursive: true); } catch { } }
        }
    }

    [Fact]
    public void Export_InchOutput_ReimportsToSameMillimeters()
    {
        string dir = TempDir();
        Directory.CreateDirectory(dir);
        try
        {
            CadDocument doc = Import(SamplePath("testdata/dxf/rectangle_lines.dxf"));
            var options = new DxfExportOptions { OutputUnit = LengthUnit.Inch };
            (_, string outPath) = Export(doc, dir, "rect_inch.dxf", options);

            CadDocument reloaded = Import(outPath);
            Assert.Equal(LengthUnit.Inch, reloaded.Units);
            // 100 mm must come back as 100 mm (exported as ~3.937 in, re-imported 脳25.4).
            var line = Assert.IsType<LineGeometry>(reloaded.Entities[0]);
            Assert.Equal(100.0, line.Length, 6);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { Thread.Sleep(200); try { Directory.Delete(dir, recursive: true); } catch { } }
        }
    }
}

