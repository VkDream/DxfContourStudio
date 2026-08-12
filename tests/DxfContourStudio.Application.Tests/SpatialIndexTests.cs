#nullable enable

using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Spatial index semantics (ADR-015): point queries return exactly the
/// entities within the radius (bounds-based cell filtering + exact distance
/// check), the document's Pick reuses the lazy index transparently, entity
/// mutations invalidate it (Pick never goes stale) and interaction
/// visibility rules still apply.
/// </summary>
public class SpatialIndexTests
{
    private static LineGeometry Line(long id, double x0, double y0, double x1, double y1) =>
        new(id, "0", new Point2(x0, y0), new Point2(x1, y1));

    private static CadDocument Document(params IGeometryEntity[] entities)
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            entities,
            [new LayerState("0", IsOn: true, IsFrozen: false, AciColorIndex: 7, IsColorByLayer: true)],
            null, null, null);
        return doc;
    }

    [Fact]
    public void Query_FindsNearbyLine_Only()
    {
        var index = new SpatialIndex(10);
        index.Add(Line(1, 0, 0, 100, 0));
        index.Add(Line(2, 10_000, 0, 10_100, 0));

        var hits = index.Query(new Point2(50, 0), 1.0);
        Assert.Single(hits);
        Assert.Equal(1L, hits[0].Id);
    }

    [Fact]
    public void Query_ExactDistance_FiltersCandidates()
    {
        var index = new SpatialIndex(5);
        index.Add(Line(1, 0, 0, 10, 0));
        index.Add(Line(2, 5, 3, 5, 4)); // inside the query box, 3 units off-axis

        Assert.Single(index.Query(new Point2(5, 0), 1.0));
        Assert.Equal(1L, index.Query(new Point2(5, 0), 1.0)[0].Id);
        Assert.Equal(2, index.Query(new Point2(5, 0), 3.5).Count);
    }

    [Fact]
    public void Query_ZeroRadius_FindsNothing()
    {
        var index = new SpatialIndex(10);
        index.Add(Line(1, 0, 0, 10, 0));
        Assert.Empty(index.Query(new Point2(5, 0), 0));
    }

    [Fact]
    public void Document_Pick_MatchesDistanceSemantics()
    {
        var doc = Document(Line(1, 0, 0, 100, 0), Line(2, 300, 0, 400, 0));
        Assert.Single(doc.Pick(new Point2(50, 0.5), 1.0));
        Assert.Empty(doc.Pick(new Point2(50, 5), 1.0));
        // (50,5) is 5 units from line1; line2 at x≈300 is far away.
        Assert.Single(doc.Pick(new Point2(50, 5), 10.0));
    }

    [Fact]
    public void Document_Pick_MutationInvalidatesIndex()
    {
        var doc = Document(Line(1, 0, 0, 100, 0));
        Assert.Single(doc.Pick(new Point2(50, 0), 0.5));

        doc.RemoveEntity(1);
        Assert.Empty(doc.Pick(new Point2(50, 0), 0.5));

        doc.AddEntity(Line(2, 40, 0, 60, 0));
        Assert.Single(doc.Pick(new Point2(50, 0), 0.5));
        Assert.Equal(2L, doc.Pick(new Point2(50, 0), 0.5)[0].Id);
    }

    [Fact]
    public void Index_HiddenLayer_HonorsVisibility()
    {
        var doc = Document(Line(1, 0, 0, 100, 0));
        doc.Pick(new Point2(50, 0), 0.5); // builds the index
        doc.HideAllLayers();
        Assert.Empty(doc.Pick(new Point2(50, 0), 0.5));
    }
}