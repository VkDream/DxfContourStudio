#nullable enable

using DxfContourStudio.Core.Contours;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Tests;

/// <summary>
/// Topology → contour → gap diagnostics → nesting tests (scenarios A–M of
/// the v0.1.0 batch). Every contour below is built from plain geometry
/// entities (as the DXF reader would produce them), never from fake
/// "already connected" data.
/// </summary>
public class TopologyContourTests
{
    private static LineGeometry Line(long id, double x0, double y0, double x1, double y1) =>
        new(id, "0", new Point2(x0, y0), new Point2(x1, y1));

    private static CircleGeometry Circle(long id, double cx, double cy, double r) =>
        new(id, "0", new Point2(cx, cy), r);

    private static PolylineGeometry Polyline(long id, bool isClosed, params (double X, double Y)[] vertices) =>
        new(id, "0",
            Enumerable.Range(0, vertices.Length - 1)
                .Select(i => (IPathSegment)new LineSegment(
                    new Point2(vertices[i].X, vertices[i].Y),
                    new Point2(vertices[i + 1].X, vertices[i + 1].Y)))
                .ToList(),
            isClosed);

    // ---- A: four LINEs in file order form one closed rectangle ----
    [Fact]
    public void A_FourLines_Rectangle_OneClosedContour()
    {
        var entities = new IGeometryEntity[]
        {
            Line(1, 0, 0, 100, 0),
            Line(2, 100, 0, 100, 100),
            Line(3, 100, 100, 0, 100),
            Line(4, 0, 100, 0, 0),
        };

        ContourAnalysisResult result = ContourAnalyzer.Analyze(entities);

        Assert.Equal(1, result.ClosedCount);
        Assert.Equal(0, result.OpenCount);
        Contour contour = Assert.Single(result.Contours);
        Assert.True(contour.IsClosed);
        Assert.Equal(400.0, contour.Length, 9);
        Assert.Equal(10000.0, contour.SignedArea!.Value, 6);
        Assert.Equal(ContourRole.Outer, contour.Role);
        Assert.Equal(0, contour.Depth);
        // four corners, each shared by two endpoints → 4 nodes
        Assert.Equal(4, result.Graph.NodeCount);
        Assert.Equal(4, result.Graph.EdgeCount);
    }

    // ---- B: the same rectangle with scrambled entity order ----
    [Fact]
    public void B_FourLines_ScrambledOrder_OneClosedContour()
    {
        var entities = new IGeometryEntity[]
        {
            Line(1, 0, 100, 0, 0),
            Line(2, 100, 100, 0, 100),
            Line(3, 0, 0, 100, 0),
            Line(4, 100, 0, 100, 100),
        };

        ContourAnalysisResult result = ContourAnalyzer.Analyze(entities);

        Assert.Equal(1, result.ClosedCount);
        Assert.Equal(400.0, Assert.Single(result.Contours).Length, 9);
    }

    // ---- C: one LINE of the rectangle reversed ----
    [Fact]
    public void C_FourLines_OneReversed_StillOneClosedContour()
    {
        var entities = new IGeometryEntity[]
        {
            Line(1, 0, 0, 100, 0),
            Line(2, 100, 0, 100, 100),
            Line(3, 0, 100, 100, 100), // reversed source direction
            Line(4, 0, 100, 0, 0),
        };

        ContourAnalysisResult result = ContourAnalyzer.Analyze(entities);

        Assert.Equal(1, result.ClosedCount);
        Assert.Equal(400.0, Assert.Single(result.Contours).Length, 9);
    }

    // ---- D: mixed LINE + ARC closed shape (rectangle with semicircular lid) ----
    [Fact]
    public void D_LineArcMixed_ClosedContour()
    {
        var entities = new IGeometryEntity[]
        {
            Line(1, 0, 0, 100, 0),
            Line(2, 100, 0, 100, 50),
            new ArcGeometry(3, "0", new Point2(50, 50), 50, 0, Math.PI, isCounterClockwise: true),
            Line(4, 0, 50, 0, 0),
        };

        ContourAnalysisResult result = ContourAnalyzer.Analyze(entities);

        Assert.Equal(1, result.ClosedCount);
        Contour contour = Assert.Single(result.Contours);
        Assert.Equal(200.0 + 50 * Math.PI, contour.Length, 6);
        // rectangle 100x50 + semicircle r=50
        Assert.Equal(5000.0 + 0.5 * Math.PI * 50 * 50, contour.SignedArea!.Value, 6);
    }

    // ---- E: a circle is intrinsically closed ----
    [Fact]
    public void E_Circle_ClosedContour()
    {
        var entities = new IGeometryEntity[] { Circle(1, 10, 20, 25) };

        ContourAnalysisResult result = ContourAnalyzer.Analyze(entities);

        Assert.Equal(1, result.ClosedCount);
        Contour contour = Assert.Single(result.Contours);
        Assert.True(contour.IsCircle);
        Assert.Equal(MathUtil.TwoPi * 25, contour.Length, 9);
        Assert.Equal(Math.PI * 25 * 25, contour.SignedArea!.Value, 6);
        Assert.Equal(ContourRole.Outer, contour.Role);
    }

    // ---- F: a closed polyline is a closed contour ----
    [Fact]
    public void F_ClosedPolyline_ClosedContour()
    {
        var entities = new IGeometryEntity[]
        {
            Polyline(1, isClosed: true, (0, 0), (100, 0), (100, 100), (0, 100)),
        };

        ContourAnalysisResult result = ContourAnalyzer.Analyze(entities);

        Assert.Equal(1, result.ClosedCount);
        Assert.Equal(400.0, Assert.Single(result.Contours).Length, 9);
    }

    // ---- G: an open polyline stays open ----
    [Fact]
    public void G_OpenPolyline_OpenContour()
    {
        var entities = new IGeometryEntity[]
        {
            Polyline(1, isClosed: false, (0, 0), (100, 0), (100, 100)),
        };

        ContourAnalysisResult result = ContourAnalyzer.Analyze(entities);

        Assert.Equal(0, result.ClosedCount);
        Assert.Equal(1, result.OpenCount);
        // both free ends are far apart (141 mm) → two open-end diagnostics
        Assert.Equal(2, result.Diagnostics.Count(d => d.Kind == GapKind.OpenContourEnd));
    }

    // ---- H: a 0.02 mm gap (tolerance 0.05) is a repairable small gap ----
    [Fact]
    public void H_SmallGapWithinTolerance_IsRepairableCandidate()
    {
        var entities = new IGeometryEntity[]
        {
            Line(1, 0, 0, 100, 0),
            Line(2, 100.02, 0, 200, 0),
        };

        ContourAnalysisResult result = ContourAnalyzer.Analyze(entities);

        // the two lines are separate open chains; the near ends form the gap,
        // the two far ends stay unmatched open ends
        Assert.Equal(2, result.OpenCount);
        GapDiagnostic gap = Assert.Single(result.Diagnostics, d => d.Kind == GapKind.SmallGap);
        Assert.True(gap.CanAutoRepair);
        Assert.True(gap.HasDistance);
        Assert.Equal(0.02, gap.Distance, 6);
        Assert.Equal(2, result.OpenEndCount);
    }

    // ---- H2: an isolated open end has NO measurable distance (no sentinel) ----
    [Fact]
    public void H2_IsolatedOpenEnd_HasNoDistance()
    {
        var entities = new IGeometryEntity[]
        {
            Line(1, 0, 0, 100, 0),
        };

        ContourAnalysisResult result = ContourAnalyzer.Analyze(entities);

        Assert.Equal(1, result.OpenCount);
        var openEnds = result.Diagnostics.Where(d => d.Kind == GapKind.OpenContourEnd).ToList();
        Assert.Equal(2, openEnds.Count);
        foreach (GapDiagnostic d in openEnds)
        {
            // No other endpoint anywhere nearby: the open end must not carry a
            // double.MaxValue sentinel that could leak into the UI.
            Assert.False(d.HasDistance);
            Assert.False(double.IsNaN(d.Distance));
            Assert.False(double.IsInfinity(d.Distance));
            Assert.NotEqual(double.MaxValue, d.Distance);
        }
    }

    // ---- I: a 1 mm gap is beyond the repair tolerance ----
    [Fact]
    public void I_LargeGapBeyondTolerance_NotRepairable()
    {
        var entities = new IGeometryEntity[]
        {
            Line(1, 0, 0, 100, 0),
            Line(2, 101, 0, 200, 0),
        };

        ContourAnalysisResult result = ContourAnalyzer.Analyze(entities);

        Assert.Equal(0, result.SmallGapCount);
        // all four free ends are unmatched (nearest gap is 1 mm > tolerance)
        Assert.Equal(4, result.Diagnostics.Count(d => d.Kind == GapKind.OpenContourEnd));
        Assert.All(result.Diagnostics, d => Assert.False(d.CanAutoRepair));
    }

    // ---- J: a T-junction is a branch node ----
    [Fact]
    public void J_TJunction_BranchNodeDetected()
    {
        var entities = new IGeometryEntity[]
        {
            Line(1, 0, 50, 50, 50),
            Line(2, 50, 50, 100, 50),
            Line(3, 50, 50, 50, 100),
        };

        ContourAnalysisResult result = ContourAnalyzer.Analyze(entities);

        Assert.Equal(1, result.Graph.BranchNodeCount);
        Assert.Equal(1, result.BranchCount);
        Assert.Equal(0, result.ClosedCount);
        Assert.Equal(3, result.OpenCount);
    }

    // ---- K: two separate rectangles are two contours ----
    [Fact]
    public void K_TwoRectangles_TwoClosedContours()
    {
        var entities = new IGeometryEntity[]
        {
            Line(1, 0, 0, 100, 0),
            Line(2, 100, 0, 100, 100),
            Line(3, 100, 100, 0, 100),
            Line(4, 0, 100, 0, 0),
            Line(5, 200, 0, 300, 0),
            Line(6, 300, 0, 300, 100),
            Line(7, 300, 100, 200, 100),
            Line(8, 200, 100, 200, 0),
        };

        ContourAnalysisResult result = ContourAnalyzer.Analyze(entities);

        Assert.Equal(2, result.ClosedCount);
        Assert.Equal(0, result.OpenCount);
        Assert.Equal(2, result.OuterCount);
        Assert.Equal(0, result.HoleCount);
    }

    // ---- L: outer rectangle + inner circle → outer + hole ----
    [Fact]
    public void L_RectangleWithHole_OuterAndHole()
    {
        var entities = new IGeometryEntity[]
        {
            Line(1, 0, 0, 100, 0),
            Line(2, 100, 0, 100, 100),
            Line(3, 100, 100, 0, 100),
            Line(4, 0, 100, 0, 0),
            Circle(5, 50, 50, 15),
        };

        ContourAnalysisResult result = ContourAnalyzer.Analyze(entities);

        Assert.Equal(2, result.ClosedCount);
        Assert.Equal(1, result.OuterCount);
        Assert.Equal(1, result.HoleCount);
        Contour hole = result.ClosedContours.Single(c => c.Role == ContourRole.Hole);
        Contour outer = result.ClosedContours.Single(c => c.Role == ContourRole.Outer);
        Assert.Equal(1, hole.Depth);
        Assert.Equal(outer.Id, hole.ParentContourId);
    }

    // ---- M: outer → hole → island nesting depths ----
    [Fact]
    public void M_OuterHoleIsland_DepthsAndRoles()
    {
        var entities = new IGeometryEntity[]
        {
            Line(1, 0, 0, 100, 0),
            Line(2, 100, 0, 100, 100),
            Line(3, 100, 100, 0, 100),
            Line(4, 0, 100, 0, 0),
            Circle(5, 50, 50, 25),          // hole
            Polyline(6, isClosed: true, (40, 40), (60, 40), (60, 60), (40, 60)), // island
        };

        ContourAnalysisResult result = ContourAnalyzer.Analyze(entities);

        Assert.Equal(3, result.ClosedCount);
        Assert.Equal(1, result.OuterCount);
        Assert.Equal(1, result.HoleCount);
        Assert.Equal(1, result.IslandCount);

        Contour outer = result.ClosedContours.Single(c => c.Role == ContourRole.Outer);
        Contour hole = result.ClosedContours.Single(c => c.Role == ContourRole.Hole);
        Contour island = result.ClosedContours.Single(c => c.Role == ContourRole.Island);

        Assert.Equal(0, outer.Depth);
        Assert.Equal(1, hole.Depth);
        Assert.Equal(2, island.Depth);
        Assert.Equal(hole.Id, island.ParentContourId);
        Assert.Equal(outer.Id, hole.ParentContourId);
    }
}
