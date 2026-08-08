#nullable enable

using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Dxf.Abstractions;
using DxfContourStudio.Dxf.Infrastructure;

namespace DxfContourStudio.Dxf.Tests;

/// <summary>
/// End-to-end tests of the real ACadSharp-backed reader against checked-in
/// sample files under <c>testdata/dxf</c>. These do not need real hardware and
/// run in CI; they verify the whole import path (parse, units, mapping).
/// </summary>
public class AcadSharpDxfReadershipTests
{
    private const string BasicScene = "testdata/dxf/basic-scene.dxf";

    private static string SamplePath(string relative) =>
        Path.Combine(AppContext.BaseDirectory, relative);

    [Fact]
    public void Read_BasicScene_ParsesWithoutFatalError()
    {
        var reader = new AcadSharpDxfReader();
        DxfImportResult result = reader.Read(SamplePath(BasicScene));

        Assert.False(result.HasFatalError);
        Assert.NotNull(result.Entities);
    }

    [Fact]
    public void Read_BasicScene_ReportsDeclaredUnits()
    {
        var reader = new AcadSharpDxfReader();
        DxfImportResult result = reader.Read(SamplePath(BasicScene));

        Assert.Equal(LengthUnit.Millimeter, result.Report.DeclaredUnits);
    }

    [Fact]
    public void Read_BasicScene_MapsKnownLineary()
    {
        var reader = new AcadSharpDxfReader();
        DxfImportResult result = reader.Read(SamplePath(BasicScene));

        // LINEs + CIRCLE + ARC + LWPOLYLINE(closed 2-vertex)
        Assert.True(result.Entities.Count >= 5);
        Assert.Contains(result.Entities, e => e.GeometryType == GeometryType.Line);
        Assert.Contains(result.Entities, e => e.GeometryType == GeometryType.Circle);
        Assert.Contains(result.Entities, e => e.GeometryType == GeometryType.Arc);
        Assert.Contains(result.Entities, e => e.GeometryType == GeometryType.Polyline);
    }
}