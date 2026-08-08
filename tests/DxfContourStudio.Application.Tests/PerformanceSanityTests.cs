#nullable enable

using System.Diagnostics;
using DxfContourStudio.Application.Commands;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Contours;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Coarse performance sanity: keeps large-document operations inside a
/// generous time budget so gross regressions (accidental O(n²) hot paths,
/// unbounded rebuilds) fail CI without being flaky on slow machines. These
/// are sanity budgets, not benchmarks.
/// </summary>
public sealed class PerformanceSanityTests
{
    private static CadDocument BuildDocument(int count)
    {
        var entities = new List<IGeometryEntity>(count);
        var layers = new List<LayerState> { new("0", true, false, 7, true) };
        for (int i = 0; i < count; i++)
        {
            entities.Add(new LineGeometry(i + 1, "0", new Point2(i, 0), new Point2(i + 10, 0)));
        }

        var doc = new CadDocument();
        doc.ReplaceContent(entities, layers, null, null, null);
        return doc;
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(10000)]
    public void Pick_SinglePass_StaysFast(int count)
    {
        var doc = BuildDocument(count);
        var viewport = new Selection.Viewport(1.0);

        var sw = Stopwatch.StartNew();
        Selection.HitTester.PickClosest(doc, new Point2(count / 2.0, 1), 6, viewport);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 1000, $"Pick over {count} entities took {sw.ElapsedMilliseconds} ms");
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(10000)]
    public void VisibleEntities_StaysFast(int count)
    {
        var doc = BuildDocument(count);

        var sw = Stopwatch.StartNew();
        var visible = doc.VisibleEntities;
        var bounds = doc.OverallBounds;
        sw.Stop();

        Assert.Equal(count, visible.Count);
        Assert.NotNull(bounds);
        Assert.True(sw.ElapsedMilliseconds < 500, $"Visibility scan over {count} entities took {sw.ElapsedMilliseconds} ms");
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(10000)]
    public void Move_And_Undo_StaysFast(int count)
    {
        var doc = BuildDocument(count);
        var history = new CommandHistory();
        var ids = new List<long>(count);
        for (long i = 1; i <= count; i++)
        {
            ids.Add(i);
        }

        var sw = Stopwatch.StartNew();
        history.Execute(new MoveEntitiesCommand(doc, ids, new Vector2(5, 5)));
        history.TryUndo();
        sw.Stop();

        // Every entity restored to its original position after undo.
        // Budget is generous (CI runners are slower than local dev boxes) but
        // still catches an accidental O(n²) regression in move/undo.
        Assert.Equal(new Point2(0, 0), doc.GetEntityById(1)!.StartPoint);
        Assert.True(sw.ElapsedMilliseconds < 3000, $"Move+Undo over {count} entities took {sw.ElapsedMilliseconds} ms");
    }

    [Theory]
    [InlineData(10000)]
    [InlineData(50000)]
    public void DocumentBuild_Bounds_Topology_AtScale(int count)
    {
        var sw = Stopwatch.StartNew();
        var doc = BuildDocument(count);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Building {count} entities took {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        var bounds = doc.OverallBounds;
        sw.Stop();
        Assert.NotNull(bounds);
        Assert.True(sw.ElapsedMilliseconds < 500, $"Bounds over {count} entities took {sw.ElapsedMilliseconds} ms");

        // Topology: disjoint lines at 10k are fine; at 50k keep a generous
        // budget so a real O(n²) regression still trips.
        sw.Restart();
        var analysis = ContourAnalyzer.Analyze(doc.Entities.ToList());
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 20000, $"Topology over {count} entities took {sw.ElapsedMilliseconds} ms");
        Assert.Equal(count, analysis.Graph.EdgeCount);
    }

    [Theory]
    [InlineData(10000)]
    [InlineData(50000)]
    public void RenderPreparation_AtScale(int count)
    {
        var doc = BuildDocument(count);

        // The render preparation is the visible-entities + bounds pass that
        // CadViewport.OnRender performs per frame.
        var sw = Stopwatch.StartNew();
        var visible = doc.VisibleEntities;
        var bounds = doc.OverallBounds;
        sw.Stop();

        Assert.Equal(count, visible.Count);
        Assert.NotNull(bounds);
        Assert.True(sw.ElapsedMilliseconds < 1000, $"Render prep over {count} entities took {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void HitTest_RepresentativeQueries_At50k()
    {
        var doc = BuildDocument(50000);
        var viewport = new Selection.Viewport(1.0);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 50; i++)
        {
            Selection.HitTester.PickClosest(doc, new Point2(i * 900, 1), 6, viewport);
        }

        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 2000, $"50 hit tests over 50k entities took {sw.ElapsedMilliseconds} ms");
    }
}