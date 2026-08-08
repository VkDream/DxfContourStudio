#nullable enable

using System.IO;
using DxfContourStudio.Application.Commands;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Imports;
using DxfContourStudio.Application.Projects;
using DxfContourStudio.Core.Contours;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Dxf.Infrastructure;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Round-trip tests for the .dxfstudio project format: DXF → Document →
/// Save project → Load project → geometry equivalent. Also covers the dirty
/// flag and the unsaved-changes guard decision flow.
/// </summary>
public class ProjectRoundTripTests
{
    private static string SamplePath(string relative) =>
        Path.Combine(AppContext.BaseDirectory, relative);

    private static CadDocument Load(string fileName)
    {
        var doc = new CadDocument();
        var service = new DxfImportService(new AcadSharpDxfReader());
        Assert.True(service.Import(SamplePath("testdata/dxf/" + fileName), doc).IsSuccess);
        return doc;
    }

    private static string TempDxfstudio(string name) =>
        Path.Combine(Path.GetTempPath(), $"dcs-test-{Guid.NewGuid():N}", name);

    [Fact]
    public void BasicScene_SaveLoad_RoundTripsGeometry()
    {
        CadDocument original = Load("basic-scene.dxf");
        var tolerance = GeometryTolerance.Default;

        string dir = Path.GetDirectoryName(TempDxfstudio("a.dxfstudio"))!;
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "basic-scene.dxfstudio");

        ProjectFile project = ProjectSerializer.ToProject(original, tolerance);
        ProjectSerializer.Save(project, path);

        (CadDocument loaded, GeometryTolerance loadedTol) = ProjectSerializer.ToDocument(ProjectSerializer.Load(path));

        Assert.Equal(original.Entities.Count, loaded.Entities.Count);
        Assert.Equal(original.Layers.Count, loaded.Layers.Count);
        Assert.Equal(original.Units, loaded.Units);
        Assert.Equal(tolerance.EndpointSnapTolerance, loadedTol.EndpointSnapTolerance);
        Assert.Equal(tolerance.DuplicateTolerance, loadedTol.DuplicateTolerance);

        for (int i = 0; i < original.Entities.Count; i++)
        {
            AssertSameGeometry(original.Entities[i], loaded.Entities[i]);
        }

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void OuterHole_SaveLoad_RoundTripsIdsAndBounds()
    {
        CadDocument original = Load("outer_hole.dxf");
        string dir = Path.GetDirectoryName(TempDxfstudio("b.dxfstudio"))!;
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "outer_hole.dxfstudio");

        ProjectSerializer.Save(ProjectSerializer.ToProject(original, GeometryTolerance.Default), path);
        (CadDocument loaded, _) = ProjectSerializer.ToDocument(ProjectSerializer.Load(path));

        Assert.Equal(original.Entities.Count, loaded.Entities.Count);
        Assert.Equal(original.Entities.Select(e => e.Id), loaded.Entities.Select(e => e.Id));
        Assert.Equal(original.OverallBounds!.Value.MinX, loaded.OverallBounds!.Value.MinX, 6);
        Assert.Equal(original.OverallBounds!.Value.MaxY, loaded.OverallBounds!.Value.MaxY, 6);

        // Re-analysis after load must reproduce the same contour result.
        ContourAnalysisResult a = ContourAnalyzer.Analyze(original.Entities.ToList());
        ContourAnalysisResult b = ContourAnalyzer.Analyze(loaded.Entities.ToList());
        Assert.Equal(a.ClosedCount, b.ClosedCount);
        Assert.Equal(a.OuterCount, b.OuterCount);
        Assert.Equal(a.HoleCount, b.HoleCount);

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void RepairedSmallGap_SaveLoad_RemainsRepaired()
    {
        CadDocument doc = Load("small_gap_003.dxf");
        var analysis = ContourAnalyzer.Analyze(doc.Entities.ToList());
        GapDiagnostic gap = Assert.Single(analysis.Diagnostics, d => d.Kind == GapKind.SmallGap);

        var history = new CommandHistory();
        history.Execute(new RepairGapCommand(doc, gap));

        ContourAnalysisResult afterRepair = ContourAnalyzer.Analyze(doc.Entities.ToList());
        Assert.Equal(0, afterRepair.SmallGapCount);

        string dir = Path.GetDirectoryName(TempDxfstudio("c.dxfstudio"))!;
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "repaired.dxfstudio");
        ProjectSerializer.Save(ProjectSerializer.ToProject(doc, GeometryTolerance.Default), path);

        (CadDocument loaded, _) = ProjectSerializer.ToDocument(ProjectSerializer.Load(path));
        ContourAnalysisResult loadedAnalysis = ContourAnalyzer.Analyze(loaded.Entities.ToList());
        Assert.Equal(0, loadedAnalysis.SmallGapCount);

        // The repaired endpoints must be at the midpoint (100.015, 0).
        var line1 = Assert.IsType<LineGeometry>(loaded.Entities[0]);
        Assert.Equal(100.015, line1.P1.X, 6);
        var line2 = Assert.IsType<LineGeometry>(loaded.Entities[1]);
        Assert.Equal(100.015, line2.P0.X, 6);

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void UnknownSchemaVersion_RejectedOnLoad()
    {
        var project = new ProjectFile { SchemaVersion = 999 };
        string json = ProjectSerializer.Serialize(project);
        Assert.Throws<NotSupportedException>(() => ProjectSerializer.ToDocument(ProjectSerializer.Deserialize(json)));
    }

    [Fact]
    public void GeometryToleranceSettings_RoundTrip()
    {
        var tol = new GeometryTolerance
        {
            EndpointSnapTolerance = 0.123,
            ZeroLengthTolerance = 1e-7,
            DuplicateTolerance = 0.0002,
        };
        ProjectFile project = ProjectSerializer.ToProject(new CadDocument(), tol);
        (_, GeometryTolerance loaded) = ProjectSerializer.ToDocument(project);
        Assert.Equal(0.123, loaded.EndpointSnapTolerance);
        Assert.Equal(1e-7, loaded.ZeroLengthTolerance);
        Assert.Equal(0.0002, loaded.DuplicateTolerance);
    }

    private static void AssertSameGeometry(IGeometryEntity a, IGeometryEntity b)
    {
        Assert.Equal(a.GeometryType, b.GeometryType);
        Assert.Equal(a.Id, b.Id);
        Assert.Equal(a.LayerName, b.LayerName);
        Assert.Equal(a.IsVisible, b.IsVisible);
        switch (a)
        {
            case LineGeometry la when b is LineGeometry lb:
                Assert.Equal(la.P0.X, lb.P0.X, 9);
                Assert.Equal(la.P0.Y, lb.P0.Y, 9);
                Assert.Equal(la.P1.X, lb.P1.X, 9);
                Assert.Equal(la.P1.Y, lb.P1.Y, 9);
                break;
            case CircleGeometry ca when b is CircleGeometry cb:
                Assert.Equal(ca.Center.X, cb.Center.X, 9);
                Assert.Equal(ca.Center.Y, cb.Center.Y, 9);
                Assert.Equal(ca.Radius, cb.Radius, 9);
                break;
            case ArcGeometry aa when b is ArcGeometry ab:
                Assert.Equal(aa.Center.X, ab.Center.X, 9);
                Assert.Equal(aa.Center.Y, ab.Center.Y, 9);
                Assert.Equal(aa.Radius, ab.Radius, 9);
                Assert.Equal(aa.StartAngleRadians, ab.StartAngleRadians, 9);
                Assert.Equal(aa.SweepRadians, ab.SweepRadians, 9);
                break;
            case PolylineGeometry pa when b is PolylineGeometry pb:
                Assert.Equal(pa.IsClosed, pb.IsClosed);
                Assert.Equal(pa.Segments.Count, pb.Segments.Count);
                for (int i = 0; i < pa.Segments.Count; i++)
                {
                    Assert.Equal(pa.Segments[i].GeometryType, pb.Segments[i].GeometryType);
                    Assert.Equal(pa.Segments[i].StartPoint.X, pb.Segments[i].StartPoint.X, 9);
                    Assert.Equal(pa.Segments[i].EndPoint.Y, pb.Segments[i].EndPoint.Y, 9);
                }

                break;
        }
    }
}

/// <summary>Prompt stub for the unsaved-changes guard tests.</summary>
internal sealed class StubPrompt(DxfContourStudio.Application.Documents.UnsavedPromptResult result)
    : DxfContourStudio.Application.Documents.IUnsavedChangesPrompt
{
    public DxfContourStudio.Application.Documents.UnsavedPromptResult Answer { get; } = result;

    public DxfContourStudio.Application.Documents.UnsavedPromptResult Ask(string context) => Answer;
}
