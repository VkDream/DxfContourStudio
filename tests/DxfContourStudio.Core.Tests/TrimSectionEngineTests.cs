#nullable enable

using System;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Tests;

/// <summary>
/// Golden tests for the D15 section-trim planner: which interval of the
/// target path a click removes. All cases are pure math — no document,
/// no UI. Refusals must carry an explicit reason, never throw.
/// </summary>
public class TrimSectionEngineTests
{
    private const double Tolerance = 0.05;
    private const double MinPiece = 1e-6;

    private static readonly LineGeometry Line100 = new(1, "0", new Point2(0, 0), new Point2(100, 0));

    private static LineGeometry VerticalLine(double x, long id = 90) =>
        new(id, "0", new Point2(x, -10), new Point2(x, 10));

    [Fact]
    public void Line_MiddleSectionBetweenTwoBoundaries_TwoPieces()
    {
        var plan = TrimSectionEngine.PlanSectionTrim(
            Line100, [VerticalLine(30), VerticalLine(60)], new Point2(45, 0), Tolerance, MinPiece);

        Assert.True(plan.IsValid);
        Assert.Equal(0.30, plan.Plan!.StartT, 9);
        Assert.Equal(0.60, plan.Plan.EndT, 9);
        Assert.Equal(30.0, plan.Plan.RemoveStart.X, 9);
        Assert.Equal(60.0, plan.Plan.RemoveEnd.X, 9);
        Assert.Equal(2, plan.Plan.BoundaryPoints.Count);
        Assert.Single(plan.Plan.RemovedRuns);
    }

    [Fact]
    public void Line_FirstSection_TrimsToFirstBoundary_OnePiece()
    {
        var plan = TrimSectionEngine.PlanSectionTrim(
            Line100, [VerticalLine(30)], new Point2(10, 0), Tolerance, MinPiece);

        Assert.True(plan.IsValid);
        // The removed section runs from the path start to the first cut.
        Assert.Equal(0.0, plan.Plan!.StartT, 9);
        Assert.Equal(0.30, plan.Plan.EndT, 9);
    }

    [Fact]
    public void Line_LastSection_TrimsToLastBoundary_OnePiece()
    {
        var plan = TrimSectionEngine.PlanSectionTrim(
            Line100, [VerticalLine(30)], new Point2(80, 0), Tolerance, MinPiece);

        Assert.True(plan.IsValid);
        Assert.Equal(0.30, plan.Plan!.StartT, 9);
        Assert.Equal(1.0, plan.Plan.EndT, 9);
        Assert.Equal(100.0, plan.Plan.RemoveEnd.X, 9);
    }

    [Fact]
    public void Line_ClickExactlyOnCut_RemovesSectionToTheLeft()
    {
        var plan = TrimSectionEngine.PlanSectionTrim(
            Line100, [VerticalLine(30), VerticalLine(60)], new Point2(30, 0), Tolerance, MinPiece);

        Assert.True(plan.IsValid);
        Assert.Equal(0.0, plan.Plan!.StartT, 9);
        Assert.Equal(0.30, plan.Plan.EndT, 9);
    }

    [Fact]
    public void Line_MultiBoundary_RemovesOnlyClickedSection()
    {
        // Cuts at 20/40/70 → sections 0-20, 20-40, 40-70, 70-100.
        var plan = TrimSectionEngine.PlanSectionTrim(
            Line100,
            [VerticalLine(20), VerticalLine(40), VerticalLine(70)],
            new Point2(55, 0), Tolerance, MinPiece);

        Assert.True(plan.IsValid);
        Assert.Equal(0.40, plan.Plan!.StartT, 9);
        Assert.Equal(0.70, plan.Plan.EndT, 9);
        Assert.Equal(3, plan.Plan.BoundaryPoints.Count);
    }

    [Fact]
    public void Line_BoundaryAtPathEnd_OnlySectionSpansWholePath_Refused()
    {
        var outcome = TrimSectionEngine.PlanSectionTrim(
            Line100, [VerticalLine(100)], new Point2(20, 0), Tolerance, MinPiece);

        Assert.False(outcome.IsValid);
        Assert.Equal(TrimSectionRefusalReason.DegenerateRemoval, outcome.Reason);
    }

    [Fact]
    public void Line_TangentContact_CountsAsBoundary()
    {
        // Circle tangent to the target at (13,0): a single touching point is
        // a valid cut at t = 13/100 = 0.13; clicking before it removes
        // [0, 0.13].
        var tangency = new CircleGeometry(2, "0", new Point2(13, 5), 5);
        var plan = TrimSectionEngine.PlanSectionTrim(
            Line100, [tangency], new Point2(10, 0), Tolerance, MinPiece);

        Assert.True(plan.IsValid);
        Assert.Equal(0.0, plan.Plan!.StartT, 9);
        Assert.Equal(0.13, plan.Plan.EndT, 9);
        Assert.Equal(0.0, plan.Plan.RemoveStart.X, 6);
        Assert.Equal(13.0, plan.Plan.RemoveEnd.X, 6);
    }

    [Fact]
    public void Line_CrossingOutsideToleranceBand_IsNotABoundary()
    {
        // Far parallel boundary never crosses the target's supporting line.
        var outcome = TrimSectionEngine.PlanSectionTrim(
            Line100, [VerticalLine(500)], new Point2(20, 0), Tolerance, MinPiece);

        Assert.False(outcome.IsValid);
        Assert.Equal(TrimSectionRefusalReason.NoBoundary, outcome.Reason);
    }

    [Fact]
    public void Line_CoincidentCuts_MergeIntoOne()
    {
        var plan = TrimSectionEngine.PlanSectionTrim(
            Line100,
            [VerticalLine(30), VerticalLine(30.0000001)],
            new Point2(20, 0), Tolerance, MinPiece);

        Assert.True(plan.IsValid);
        Assert.Single(plan.Plan!.BoundaryPoints);
        // Click at t=0.2 lies before the merged cut → remove [0, 0.3].
        Assert.Equal(0.0, plan.Plan.StartT, 6);
        Assert.Equal(0.30, plan.Plan.EndT, 6);
    }

    [Fact]
    public void KeptPiece_BelowMinimumLength_RefusedNoResidue()
    {
        // Keeping [0, 30] = 30 mm < 40 mm minimum → refused.
        var outcome = TrimSectionEngine.PlanSectionTrim(
            Line100, [VerticalLine(30)], new Point2(60, 0), Tolerance, minPieceLength: 40);

        Assert.False(outcome.IsValid);
        Assert.Equal(TrimSectionRefusalReason.TinyKeptPiece, outcome.Reason);
    }

    [Fact]
    public void PolylineTarget_Unsupported_Refused()
    {
        var poly = new PolylineGeometry(1, "0",
            [new LineSegment(new Point2(0, 0), new Point2(100, 0))], isClosed: false);

        var outcome = TrimSectionEngine.PlanSectionTrim(
            poly, [VerticalLine(30)], new Point2(45, 0), Tolerance, MinPiece);

        Assert.False(outcome.IsValid);
        Assert.Equal(TrimSectionRefusalReason.UnsupportedTarget, outcome.Reason);
    }

    [Fact]
    public void ClickOffPath_Refused()
    {
        var outcome = TrimSectionEngine.PlanSectionTrim(
            Line100, [VerticalLine(30)], new Point2(45, 30), Tolerance, MinPiece);

        Assert.False(outcome.IsValid);
        Assert.Equal(TrimSectionRefusalReason.NotOnTarget, outcome.Reason);
    }

    [Fact]
    public void NoBoundariesAtAll_Refused()
    {
        var outcome = TrimSectionEngine.PlanSectionTrim(
            Line100, [], new Point2(45, 0), Tolerance, MinPiece);

        Assert.False(outcome.IsValid);
        Assert.Equal(TrimSectionRefusalReason.NoBoundary, outcome.Reason);
    }

    // ---- arcs ----

    private static ArcGeometry Semicircle(long id = 1)
    {
        // CCW half circle: (10,0) → (-10,0) through (0,10).
        return new ArcGeometry(id, "0", new Point2(0, 0), 10, 0, Math.PI, true);
    }

    [Fact]
    public void Arc_FirstSection_RemovalRunsFollowTheArc()
    {
        // Boundary line x = 5 cuts the arc at 60° (delta = π/3). Clicking at
        // 30° (delta = π/6) removes [0, π/3] along the true arc.
        var plan = TrimSectionEngine.PlanSectionTrim(
            Semicircle(), [VerticalLine(5)], new Point2(8.66, 5), Tolerance, MinPiece);

        Assert.True(plan.IsValid);
        Assert.Equal(0.0, plan.Plan!.StartT, 9);
        Assert.Equal(1.0 / 3.0, plan.Plan.EndT, 9);
        Assert.Single(plan.Plan.RemovedRuns);
        var run = Assert.IsType<ArcSegment>(plan.Plan.RemovedRuns[0]);
        Assert.Equal(10.0, run.Radius, 9);
        Assert.Equal(Math.PI / 3, run.SweepRadians, 9);
        Assert.True(run.IsCounterClockwise);
    }

    [Fact]
    public void Arc_MiddleSection_TwoPieces()
    {
        // Boundaries at 45° and 90° (cuts 0.25 / 0.5); click at 67.5° → the
        // middle section is removed, two arc pieces stay.
        var boundary45 = LineThroughAngle(45, 90);
        var boundary90 = LineThroughAngle(90, 90);
        var plan = TrimSectionEngine.PlanSectionTrim(
            Semicircle(), [boundary45, boundary90], new Point2(3.8268, 9.2388), Tolerance, MinPiece);

        Assert.True(plan.IsValid);
        Assert.Equal(0.25, plan.Plan!.StartT, 9);
        Assert.Equal(0.50, plan.Plan.EndT, 9);
        Assert.Equal(2, plan.Plan.BoundaryPoints.Count);
    }

    [Fact]
    public void Arc_LastSection_TrimsToLastBoundary()
    {
        var plan = TrimSectionEngine.PlanSectionTrim(
            Semicircle(), [VerticalLine(5)], new Point2(-5, 8.66), Tolerance, MinPiece);

        Assert.True(plan.IsValid);
        Assert.Equal(1.0 / 3.0, plan.Plan!.StartT, 9);
        Assert.Equal(1.0, plan.Plan.EndT, 9);
        Assert.Equal(-10.0, plan.Plan.RemoveEnd.X, 9);
    }

    /// <summary>A line through the arc center tilted at the given polar angle —
    /// its full-circle crossings with the arc sit exactly at that angle.</summary>
    private static LineGeometry LineThroughAngle(double deg, long id)
    {
        double rad = deg * Math.PI / 180.0;
        var d = new Point2(100 * Math.Cos(rad), 100 * Math.Sin(rad));
        return new LineGeometry(id, "0", new Point2(-d.X, -d.Y), d);
    }
}