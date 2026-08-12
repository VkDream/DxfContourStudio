#nullable enable

using System.Linq;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Tests.Spatial;

/// <summary>
/// D11 stress: the uniform-grid pick index must stay correct and usable at
/// 100k entities (campaign D11 target). No timing assertions — these tests
/// pin correctness (candidate set identical to a pivot-verified query) and
/// are fast enough to gate on; a regression to O(n) behavior would show up
/// as a multi-second hang on CI.
/// </summary>
public class SpatialIndexStressTests
{
    private const int Count = 100_000;

    private static List<LineGeometry> Grid()
    {
        // 1000 columns x 100 rows; each line spans [x, x+0.5] at row y.
        var lines = new List<LineGeometry>(Count);
        for (int i = 0; i < Count; i++)
        {
            double x = i % 1000;
            double y = (i / 1000) % 100;
            lines.Add(new LineGeometry(i + 1, "0", new Point2(x, y), new Point2(x + 0.5, y)));
        }

        return lines;
    }

    [Fact]
    public void Query_100kEntities_ReturnsExactCandidate()
    {
        var index = new SpatialIndex(7.5);
        index.Build(Grid());

        var found = index.Query(new Point2(10.25, 10.0), 0.5);

        // The exact candidates within radius: line id = 10*1000 + 11 = 10011.
        var match = Assert.Single(found);
        Assert.Equal(10011L, match.Id);
    }

    [Fact]
    public void Query_100k_MissesReportedCandidatesOnly()
    {
        var index = new SpatialIndex(7.5);
        index.Build(Grid());

        var near = index.Query(new Point2(-0.25, 0), 0.5);
        Assert.Single(near);

        var far = index.Query(new Point2(999.75, 99.0), 0.5);
        Assert.Single(far);

        var empty = index.Query(new Point2(500.75, 50), 0.05);
        Assert.Empty(empty);
    }

    [Fact]
    public void Query_100k_MatchesLinearScanOnSampleWindow()
    {
        var lines = Grid();
        var index = new SpatialIndex(7.5);
        index.Build(lines);

        var center = new Point2(200.25, 30.0);
        const double radius = 0.5;

        var fromIndex = index.Query(center, radius).Select(e => e.Id).ToHashSet();

        var fromScan = lines
            .Where(e => e.DistanceToPoint(center) <= radius)
            .Select(e => e.Id)
            .ToHashSet();

        Assert.Equal(fromScan, fromIndex);
    }

    [Fact]
    public void Query_100k_SampleRepeatIsStable()
    {
        var lines = Grid();
        var index = new SpatialIndex(7.5);
        index.Build(lines);

        // Ten queries at scattered points must agree with a direct distance
        // filter every time (regression guard against cell-boundary drift).
        var samples = new[]
        {
            new Point2(0.25, 0.0),
            new Point2(10.25, 10.0),
            new Point2(200.25, 30.0),
            new Point2(333.75, 41.0),
            new Point2(512.25, 60.0),
            new Point2(700.75, 70.0),
            new Point2(812.25, 80.0),
            new Point2(901.25, 90.0),
            new Point2(955.75, 95.0),
            new Point2(999.25, 0.0),
        };

        foreach (Point2 p in samples)
        {
            var fromIndex = index.Query(p, 0.5).Select(e => e.Id).ToHashSet();
            var fromScan = lines.Where(e => e.DistanceToPoint(p) <= 0.5).Select(e => e.Id).ToHashSet();
            Assert.Equal(fromScan, fromIndex);
        }
    }
}