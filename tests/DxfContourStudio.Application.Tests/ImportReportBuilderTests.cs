#nullable enable

using DxfContourStudio.Application.Imports;
using DxfContourStudio.Application.Localization;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Dxf.Abstractions;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Import Report panel rows: keyed label/value rows and the per-kind
/// statistics table (zero-count kinds are skipped).
/// </summary>
public class ImportReportBuilderTests
{
    private static DxfImportReport SampleReport() => new()
    {
        File = new ImportedFileInfo("basic-scene.dxf", @"C:\drawing\basic-scene.dxf", 4096, "AC1032"),
        DxfVersion = "AC1032",
        DeclaredUnits = LengthUnit.Millimeter,
        InterpretedUnits = LengthUnit.Millimeter,
        LayerCount = 3,
        TotalEntityCount = 6,
        ImportedCount = 5,
        IgnoredCount = 0,
        UnsupportedCount = 1,
        WarningCount = 2,
        ErrorCount = 0,
        ImportTimeSeconds = 0.125,
        Statistics = new EntityStatistics
        {
            Line = 2,
            Circle = 1,
            LwPolyline = 2,
            TextLike = 1,
        },
    };

    [Fact]
    public void Build_ProducesAllExpectedRows()
    {
        var rows = ImportReportBuilder.Build(SampleReport());

        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.ReportFile && r.Value == "basic-scene.dxf");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.ReportVersion && r.Value == "AC1032");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.ReportLayerCount && r.Value == "3");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.ReportTotalEntities && r.Value == "6");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.ReportImported && r.Value == "5");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.ReportIgnored && r.Value == "0");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.ReportUnsupported && r.Value == "1");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.ReportWarnings && r.Value == "2");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.ReportErrors && r.Value == "0");
        Assert.Contains(rows, r => r.NameKey == LocalizationKeys.ReportElapsed && r.Value == "0.125 s");
    }

    [Fact]
    public void BuildStatistics_SkipsZeroCountKinds()
    {
        var rows = ImportReportBuilder.BuildStatistics(SampleReport());

        Assert.Contains(rows, r => r.TypeKey == LocalizationKeys.EntityLine && r.Count == 2);
        Assert.Contains(rows, r => r.TypeKey == LocalizationKeys.EntityCircle && r.Count == 1);
        Assert.Contains(rows, r => r.TypeKey == LocalizationKeys.EntityLwPolyline && r.Count == 2);
        Assert.Contains(rows, r => r.TypeKey == LocalizationKeys.EntityTextLike && r.Count == 1);
        Assert.DoesNotContain(rows, r => r.TypeKey == LocalizationKeys.EntityArc);
        Assert.DoesNotContain(rows, r => r.TypeKey == LocalizationKeys.EntityOther);
    }

    [Fact]
    public void LocalizedUnit_MapsAllKnownUnits()
    {
        Assert.Equal(LocalizationKeys.UnitMillimeter, ImportReportBuilder.LocalizedUnit(LengthUnit.Millimeter));
        Assert.Equal(LocalizationKeys.UnitCentimeter, ImportReportBuilder.LocalizedUnit(LengthUnit.Centimeter));
        Assert.Equal(LocalizationKeys.UnitMeter, ImportReportBuilder.LocalizedUnit(LengthUnit.Meter));
        Assert.Equal(LocalizationKeys.UnitInch, ImportReportBuilder.LocalizedUnit(LengthUnit.Inch));
        Assert.Equal(LocalizationKeys.UnitFoot, ImportReportBuilder.LocalizedUnit(LengthUnit.Foot));
        Assert.Equal(LocalizationKeys.UnitUnitless, ImportReportBuilder.LocalizedUnit(LengthUnit.Unitless));
        Assert.Equal(LocalizationKeys.UnitUnknown, ImportReportBuilder.LocalizedUnit(LengthUnit.Unknown));
    }
}
