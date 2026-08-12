#nullable enable

using System.Collections.Generic;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Tests;

/// <summary>
/// D1 milestone tests for the IntersectionEngine 2.0 curve math: Line-Arc /
/// Arc-Arc / Line-Circle / Circle-Circle / Circle-Arc semantics including
/// tangencies, sweeps crossing the 0° axis, CW arcs, endpoint touches,
/// overlaps and coincidences. See docs/INTERSECTIONS.md (Golden cases D1-D4).
/// </summary>
public class IntersectionEngineCurvesTests
{
    private static readonly GeometryTolerance Tol = GeometryTolerance.Default;

    private const double Deg = System.Math.PI / 180.0;

    private static Point2 P(double x, double y) => new(x, y);

    private static LineGeometry Line(long id, Point2 a, Point2 b) => new(id, "L", a, b);

    private static ArcGeometry Arc(long id, Point2 center, double r, double startDeg, double sweepDeg, bool ccw = true)
        => new(id, "L", center, r, startDeg * Deg, sweepDeg * Deg, ccw);

    private static CircleGeometry Circle(long id, Point2 center, double r) => new(id, "L", center, r);

    private static CurveIntersectionResult Intersect(LineGeometry l, ArcGeometry a) => IntersectionEngine.Intersect(l, a, Tol);

    private static CurveIntersectionResult Intersect(ArcGeometry a, LineGeometry l) => IntersectionEngine.Intersect(a, l, Tol);

    private static CurveIntersectionResult Intersect(ArcGeometry a, ArcGeometry b) => IntersectionEngine.Intersect(a, b, Tol);

    private static CurveIntersectionResult Intersect(LineGeometry l, CircleGeometry c) => IntersectionEngine.Intersect(l, c, Tol);

    private static CurveIntersectionResult Intersect(CircleGeometry c, LineGeometry l) => IntersectionEngine.Intersect(c, l, Tol);

    private static CurveIntersectionResult Intersect(CircleGeometry a, CircleGeometry b) => IntersectionEngine.Intersect(a, b, Tol);

    private static CurveIntersectionResult Intersect(CircleGeometry c, ArcGeometry a) => IntersectionEngine.Intersect(c, a, Tol);

    private static CurveIntersectionResult IntersectSegments(LineSegment a, LineSegment b) => IntersectionEngine.IntersectSegments(a, b, Tol);

    private static CurveIntersectionResult IntersectSegments(LineSegment a, ArcSegment b) => IntersectionEngine.IntersectSegments(a, b, Tol);

    private static void AssertPoint(CurveIntersectionResult r, Point2 expected)
    {
        Assert.Equal(CurveIntersectionKind.Point, r.Kind);
        Assert.Equal(expected.X, r.Point1.X, 6);
        Assert.Equal(expected.Y, r.Point1.Y, 6);
    }

    private static void AssertTwoPoints(CurveIntersectionResult r, Point2 p1, Point2 p2)
    {
        Assert.Equal(CurveIntersectionKind.TwoPoints, r.Kind);
        // Order is not contractual; verify the unordered pair.
        double d1 = r.Point1.DistanceTo(p1);
        double d2 = r.Point1.DistanceTo(p2);
        Assert.True(d1 <= 1e-6 || d2 <= 1e-6, $"Point1 {r.Point1} matches neither {p1} nor {p2}");
        Point2 other = d1 <= 1e-6 ? p2 : p1;
        Assert.True(r.Point2.DistanceTo(other) <= 1e-6, $"Point2 {r.Point2} does not match {other}");
    }

    // ------------------------------------------------------------------
    // Line × Arc
    // ------------------------------------------------------------------

    [Fact]
    public void LineArc_LineCutsArcTwiceWithinSweep_TwoPoints()
    {
        // Line y=x through origin; arc on circle r=2 centered origin,
        // sweep 45°..315° CCW — both crossings (45°, 225°) lie inside.
        var r = Intersect(
            Line(1, P(-2, -2), P(2, 2)),
            Arc(2, P(0, 0), 2, 45, 270));
        AssertTwoPoints(r, P(System.Math.Sqrt(2), System.Math.Sqrt(2)), P(-System.Math.Sqrt(2), -System.Math.Sqrt(2)));
    }

    [Fact]
    public void LineArc_LineCrossesArcOnlyOneCandidateInsideSweep_ReturnsPoint()
    {
        // Line y = -1 with circle r=2: crossings at 210° (in 45..225) and 330° (out).
        var r = Intersect(
            Line(1, P(-3, -1), P(3, -1)),
            Arc(2, P(0, 0), 2, 45, 180));
        AssertPoint(r, P(-System.Math.Sqrt(3), -1));
    }

    [Fact]
    public void LineArc_TangentInsideSweep_ReturnsTangent()
    {
        // Line y=2 tangent to circle r=2 at (0,2) = 90°; arc covers 90..180.
        var r = Intersect(
            Line(1, P(-3, 2), P(3, 2)),
            Arc(2, P(0, 0), 2, 90, 90));
        Assert.Equal(CurveIntersectionKind.Tangent, r.Kind);
        Assert.Equal(0, r.Point1.X, 6);
        Assert.Equal(2, r.Point1.Y, 6);
    }

    [Fact]
    public void LineArc_TangencyOutsideSweep_ReturnsNone()
    {
        // Tangency at 90° but the arc only covers 135..225.
        var r = Intersect(
            Line(1, P(-3, 2), P(3, 2)),
            Arc(2, P(0, 0), 2, 135, 90));
        Assert.Equal(CurveIntersectionKind.None, r.Kind);
    }

    [Fact]
    public void LineArc_LineEndpointSitsOnArc_ReturnsPoint()
    {
        // Line from inside the circle (0,0) to (2,0); arc sweep 0..60°; contact
        // at (2,0) is the line's end endpoint and lies on the arc start.
        var r = Intersect(
            Line(1, P(0, 0), P(2, 0)),
            Arc(2, P(0, 0), 2, 0, 60));
        AssertPoint(r, P(2, 0));
    }

    [Fact]
    public void LineArc_ArcEndpointTouchesLine_ReturnsPoint()
    {
        // Arc ends at 90° = (0,2); vertical line x=0 crosses at exactly that point.
        var r = Intersect(
            Line(1, P(0, 1), P(0, 3)),
            Arc(2, P(0, 0), 2, 0, 90));
        AssertPoint(r, P(0, 2));
    }

    [Fact]
    public void LineArc_Miss_ReturnsNone()
    {
        var r = Intersect(
            Line(1, P(-3, 3), P(3, 3)),
            Arc(2, P(0, 0), 2, 45, 180));
        Assert.Equal(CurveIntersectionKind.None, r.Kind);
    }

    [Fact]
    public void LineArc_LineBeyondArcExtentOnSameCircle_ReturnsNone()
    {
        // Both crossings on the supporting circle exist, but the arc sweep
        // (10..30°) contains neither.
        var r = Intersect(
            Line(1, P(-3, 0), P(3, 0)),
            Arc(2, P(0, 0), 2, 10, 20));
        Assert.Equal(CurveIntersectionKind.None, r.Kind);
    }

    [Fact]
    public void LineArc_SweepCrossesZeroDegrees_TwoPointsIncluded()
    {
        // Arc from 350° to 10° (CCW, 20° long, crossing 0°). Vertical line
        // x = 1.98 on circle r=2 hits angles ±acos(0.99) ≈ 8.1° and 351.9° —
        // both inside [350°,370°).
        var r = Intersect(
            Line(1, P(1.98, -0.5), P(1.98, 0.5)),
            Arc(2, P(0, 0), 2, 350, 20));
        Assert.Equal(CurveIntersectionKind.TwoPoints, r.Kind);
        double a = System.Math.Acos(0.99);
        AssertTwoPoints(r, P(1.98, 2 * System.Math.Sin(a)), P(1.98, -2 * System.Math.Sin(a)));
    }

    [Fact]
    public void LineArc_SweepCrossesZeroDegrees_SinglePointInside()
    {
        // Same arc 350°..10°; horizontal line y = 0.17 hits the circle at
        // ≈ 4.87° (inside) and 175° (outside) → single point.
        var r = Intersect(
            Line(1, P(-3, 0.17), P(3, 0.17)),
            Arc(2, P(0, 0), 2, 350, 20));
        Assert.Equal(CurveIntersectionKind.Point, r.Kind);
        Assert.True(r.Point1.X > 1.99, $"expected crossing near 0° but got {r.Point1}");
    }

    [Fact]
    public void LineArc_CwArc_CrossingInsideSweep_ReturnsPoint()
    {
        // CW arc from 45° sweeping −60° (i.e. 45°→345°, covering 345..45);
        // crossing at 0°=360° is inside, the 180° one is not.
        var r = Intersect(
            Line(1, P(-3, 0), P(3, 0)),
            Arc(2, P(0, 0), 2, 45, -60, ccw: false));
        AssertPoint(r, P(2, 0));
    }

    [Fact]
    public void LineArc_NearlyTangentialPair_StillTwoPoints()
    {
        // Very shallow cut: distance from center to line = 1.999 (of r=2).
        // Two well-separated crossings inside a wide sweep.
        var r = Intersect(
            Line(1, P(-3, 1.999), P(3, 1.999)),
            Arc(2, P(0, 0), 2, 0, 270));
        Assert.Equal(CurveIntersectionKind.TwoPoints, r.Kind);
    }

    [Fact]
    public void LineArc_ZeroRadius_ReturnsDegenerate()
    {
        var r = Intersect(
            Line(1, P(0, 0), P(1, 0)),
            Arc(2, P(5, 5), 0, 0, 90));
        Assert.Equal(CurveIntersectionKind.Degenerate, r.Kind);
    }

    // ------------------------------------------------------------------
    // Arc × Arc
    // ------------------------------------------------------------------

    [Fact]
    public void ArcArc_TwoCrossingsInsideBothSweeps_TwoPoints()
    {
        // c1=(0,0) r2, c2=(2,0) r2 → crossings (1, ±√3).
        // arc1 sweep 60°..300° covers both; arc2 (from (2,0)) sweep 120°..360° covers both.
        var r = Intersect(
            Arc(1, P(0, 0), 2, 60, 240),
            Arc(2, P(2, 0), 2, 120, 240));
        AssertTwoPoints(r, P(1, System.Math.Sqrt(3)), P(1, -System.Math.Sqrt(3)));
    }

[Fact]
    public void ArcArc_SingleCrossing_ReturnsPoint()
    {
        // arc1 sweep 0..90 covers (1,+√3) at 60°; arc2 (from (2,0)) sweep
        // 120..180 covers its 120° viewpoint onto the same point — the net
        // unique crossing is (1,+√3). The (1,−√3) point lies at 300° (out of
        // arc1) / 240° (out of arc2).
        var r = Intersect(
            Arc(1, P(0, 0), 2, 0, 90),
            Arc(2, P(2, 0), 2, 120, 60));
        AssertPoint(r, P(1, System.Math.Sqrt(3)));
    }

    [Fact]
    public void ArcArc_ExternalTangency_ReturnsTangent()
    {
        // c1=(0,0) r2, c2=(4,0) r2 → external tangent at (2,0).
        // arc1 covers 0..90 (includes 0°), arc2 (from (4,0)) covers 180..270 (includes 180°).
        var r = Intersect(
            Arc(1, P(0, 0), 2, 0, 90),
            Arc(2, P(4, 0), 2, 180, 90));
        Assert.Equal(CurveIntersectionKind.Tangent, r.Kind);
        Assert.Equal(2, r.Point1.X, 6);
        Assert.Equal(0, r.Point1.Y, 6);
    }

    [Fact]
    public void ArcArc_InternalTangency_ReturnsTangent()
    {
        // c1=(0,0) r3, c2=(1,0) r2 → internal tangency at (3,0).
        var r = Intersect(
            Arc(1, P(0, 0), 3, 0, 90),
            Arc(2, P(1, 0), 2, 0, 90));
        Assert.Equal(CurveIntersectionKind.Tangent, r.Kind);
        Assert.Equal(3, r.Point1.X, 6);
        Assert.Equal(0, r.Point1.Y, 6);
    }

    [Fact]
    public void ArcArc_CirclesApart_ReturnsNone()
    {
        var r = Intersect(
            Arc(1, P(0, 0), 1, 0, 90),
            Arc(2, P(5, 0), 1, 0, 90));
        Assert.Equal(CurveIntersectionKind.None, r.Kind);
    }

    [Fact]
    public void ArcArc_CirclesCrossButCrossingsOutsideSweeps_ReturnsNone()
    {
        // Supporting circles cross at (1, ±√3) (angles 60° / 300° from c1;
        // 120° / 240° from c2). arc1 covers 45..65 (60° inside, 300° out);
        // arc2 covers 90..110 (neither 120° nor 240°) → no net crossing.
        var r = Intersect(
            Arc(1, P(0, 0), 2, 45, 20),
            Arc(2, P(2, 0), 2, 90, 20));
        Assert.Equal(CurveIntersectionKind.None, r.Kind);
    }

    [Fact]
    public void ArcArc_SharedEndpointTouch_ReturnsTangent()
    {
        // Both arcs end/start at (0,2): c1=(0,0) r2 arc 0..90, c2=(0,4) r2
        // arc 270..360. The two supporting circles are externally tangent at
        // (0,2) — a single tangency, reported as Tangent.
        var r = Intersect(
            Arc(1, P(0, 0), 2, 0, 90),
            Arc(2, P(0, 4), 2, 270, 90));
        Assert.Equal(CurveIntersectionKind.Tangent, r.Kind);
        Assert.Equal(0, r.Point1.X, 6);
        Assert.Equal(2, r.Point1.Y, 6);
    }

    [Fact]
    public void ArcArc_ConcentricOverlap_ReturnsOverlap()
    {
        var r = Intersect(
            Arc(1, P(0, 0), 2, 0, 90),
            Arc(2, P(0, 0), 2, 45, 90));
        Assert.Equal(CurveIntersectionKind.Overlap, r.Kind);
    }

    [Fact]
    public void ArcArc_ConcentricIdentical_ReturnsCoincident()
    {
        var r = Intersect(
            Arc(1, P(0, 0), 2, 0, 90),
            Arc(2, P(0, 0), 2, 0, 90));
        Assert.Equal(CurveIntersectionKind.Coincident, r.Kind);
    }

    [Fact]
    public void ArcArc_ConcentricIdenticalCwVsCcw_ReturnsCoincident()
    {
        // Same geometric span, one CW one CCW — still the same arc region.
        var r = Intersect(
            Arc(1, P(0, 0), 2, 0, 90),
            Arc(2, P(0, 0), 2, 90, -90, ccw: false));
        Assert.Equal(CurveIntersectionKind.Coincident, r.Kind);
    }

    [Fact]
    public void ArcArc_ConcentricDisjointSweeps_ReturnsNone()
    {
        var r = Intersect(
            Arc(1, P(0, 0), 2, 0, 90),
            Arc(2, P(0, 0), 2, 180, 90));
        Assert.Equal(CurveIntersectionKind.None, r.Kind);
    }

    [Fact]
    public void ArcArc_ConcentricDifferentRadii_ReturnsNone()
    {
        var r = Intersect(
            Arc(1, P(0, 0), 2, 0, 90),
            Arc(2, P(0, 0), 3, 45, 90));
        Assert.Equal(CurveIntersectionKind.None, r.Kind);
    }

    [Fact]
    public void ArcArc_ConcentricSweepCrossingZero_OverlapDetected()
    {
        // arc1 covers 350°..10°, arc2 covers 0°..20° → shared interval [0°,10°].
        var r = Intersect(
            Arc(1, P(0, 0), 2, 350, 20),
            Arc(2, P(0, 0), 2, 0, 20));
        Assert.Equal(CurveIntersectionKind.Overlap, r.Kind);
    }

    [Fact]
    public void ArcArc_CwCcwArcPair_TwoCrossingsDetected()
    {
        // arc1 is CW 60°→300° (covers 300°..60°): both crossings (1,±√3) at
        // 60°/300° are inside. arc2 is CCW 120°..240° (from (2,0)): its
        // viewpoint angles 120°/240° are inside too → two net crossings.
        var r = Intersect(
            Arc(1, P(0, 0), 2, 60, -120, ccw: false),
            Arc(2, P(2, 0), 2, 120, 120));
        AssertTwoPoints(r, P(1, System.Math.Sqrt(3)), P(1, -System.Math.Sqrt(3)));
    }

    [Fact]
    public void ArcArc_TangencyOutsideSweep_ReturnsNone()
    {
        // External tangent at (2,0) (arc1 covers 180..270 — no 0° — arc2 covers
        // 0..90 — its 180° not covered) → none.
        var r = Intersect(
            Arc(1, P(0, 0), 2, 180, 90),
            Arc(2, P(4, 0), 2, 0, 90));
        Assert.Equal(CurveIntersectionKind.None, r.Kind);
    }

    // ------------------------------------------------------------------
    // Line × Circle
    // ------------------------------------------------------------------

    [Fact]
    public void LineCircle_Secant_TwoPoints()
    {
        var r = Intersect(
            Line(1, P(-3, 1), P(3, 1)),
            Circle(2, P(0, 0), 2));
        Assert.Equal(CurveIntersectionKind.TwoPoints, r.Kind);
        AssertTwoPoints(r, P(-System.Math.Sqrt(3), 1), P(System.Math.Sqrt(3), 1));
    }

    [Fact]
    public void LineCircle_Diameter_TwoPoints()
    {
        var r = Intersect(
            Line(1, P(-3, 0), P(3, 0)),
            Circle(2, P(0, 0), 2));
        Assert.Equal(CurveIntersectionKind.TwoPoints, r.Kind);
        AssertTwoPoints(r, P(-2, 0), P(2, 0));
    }

    [Fact]
    public void LineCircle_Tangent_ReturnsTangent()
    {
        var r = Intersect(
            Line(1, P(-3, 2), P(3, 2)),
            Circle(2, P(0, 0), 2));
        Assert.Equal(CurveIntersectionKind.Tangent, r.Kind);
        Assert.Equal(0, r.Point1.X, 6);
        Assert.Equal(2, r.Point1.Y, 6);
    }

    [Fact]
    public void LineCircle_Miss_ReturnsNone()
    {
        var r = Intersect(
            Line(1, P(-3, 3), P(3, 3)),
            Circle(2, P(0, 0), 2));
        Assert.Equal(CurveIntersectionKind.None, r.Kind);
    }

    [Fact]
    public void LineCircle_SegmentFromInsideToOutside_SingleCrossing()
    {
        // Segment starts inside the circle (0,0) and exits at (2,0): exactly
        // one crossing lies on the segment (the −2 crossing is behind the start).
        var r = Intersect(
            Line(1, P(0, 0), P(3, 0)),
            Circle(2, P(0, 0), 2));
        Assert.Equal(CurveIntersectionKind.Point, r.Kind);
        Assert.Equal(2, r.Point1.X, 6);
        Assert.Equal(0, r.Point1.Y, 6);
    }

    [Fact]
    public void LineCircle_ZeroLengthLine_ReturnsNone()
    {
        var r = Intersect(
            Line(1, P(1, 1), P(1, 1)),
            Circle(2, P(0, 0), 2));
        Assert.Equal(CurveIntersectionKind.None, r.Kind);
    }

    // ------------------------------------------------------------------
    // Circle × Circle
    // ------------------------------------------------------------------

    [Fact]
    public void CircleCircle_TwoCrossings_TwoPoints()
    {
        var r = Intersect(
            Circle(1, P(0, 0), 2),
            Circle(2, P(2, 0), 2));
        Assert.Equal(CurveIntersectionKind.TwoPoints, r.Kind);
        AssertTwoPoints(r, P(1, System.Math.Sqrt(3)), P(1, -System.Math.Sqrt(3)));
    }

    [Fact]
    public void CircleCircle_ExternalTangent_ReturnsTangent()
    {
        var r = Intersect(
            Circle(1, P(0, 0), 2),
            Circle(2, P(4, 0), 2));
        Assert.Equal(CurveIntersectionKind.Tangent, r.Kind);
        Assert.Equal(2, r.Point1.X, 6);
        Assert.Equal(0, r.Point1.Y, 6);
    }

    [Fact]
    public void CircleCircle_InternalTangent_ReturnsTangent()
    {
        var r = Intersect(
            Circle(1, P(0, 0), 3),
            Circle(2, P(1, 0), 2));
        Assert.Equal(CurveIntersectionKind.Tangent, r.Kind);
        Assert.Equal(3, r.Point1.X, 6);
        Assert.Equal(0, r.Point1.Y, 6);
    }

    [Fact]
    public void CircleCircle_ConcentricEqual_ReturnsCoincident()
    {
        var r = Intersect(
            Circle(1, P(0, 0), 2),
            Circle(2, P(0, 0), 2));
        Assert.Equal(CurveIntersectionKind.Coincident, r.Kind);
    }

    [Fact]
    public void CircleCircle_ConcentricDifferentRadii_ReturnsNone()
    {
        var r = Intersect(
            Circle(1, P(0, 0), 2),
            Circle(2, P(0, 0), 3));
        Assert.Equal(CurveIntersectionKind.None, r.Kind);
    }

    [Fact]
    public void CircleCircle_Disjoint_ReturnsNone()
    {
        var r = Intersect(
            Circle(1, P(0, 0), 1),
            Circle(2, P(5, 0), 1));
        Assert.Equal(CurveIntersectionKind.None, r.Kind);
    }

    [Fact]
    public void CircleCircle_OneInsideOtherWithoutTouch_ReturnsNone()
    {
        var r = Intersect(
            Circle(1, P(0, 0), 5),
            Circle(2, P(1, 0), 1));
        Assert.Equal(CurveIntersectionKind.None, r.Kind);
    }

    [Fact]
    public void CircleCircle_NearlyExternalTangent_NearToleranceRespectsTangencyDistance()
    {
        // Centers 4.0000005 apart, r=2 each: within TangencyTolerance (1e-6)
        // of external tangency → reported as Tangent.
        var r = Intersect(
            Circle(1, P(0, 0), 2),
            Circle(2, P(4.0000005, 0), 2));
        Assert.Equal(CurveIntersectionKind.Tangent, r.Kind);
    }

    // ------------------------------------------------------------------
    // Circle × Arc
    // ------------------------------------------------------------------

    [Fact]
    public void CircleArc_TwoCrossingsInsideArcSweep_TwoPoints()
    {
        var r = Intersect(
            Circle(1, P(0, 0), 2),
            Arc(2, P(2, 0), 2, 120, 240));
        Assert.Equal(CurveIntersectionKind.TwoPoints, r.Kind);
        AssertTwoPoints(r, P(1, System.Math.Sqrt(3)), P(1, -System.Math.Sqrt(3)));
    }

    [Fact]
    public void CircleArc_SingleCrossingInSweep_ReturnsPoint()
    {
        // Arc (from (2,0)) covers 240..300: the (1,−√3) crossing at 240° from
        // c2 is in; (1,+√3) at 120° is out.
        var r = Intersect(
            Circle(1, P(0, 0), 2),
            Arc(2, P(2, 0), 2, 240, 60));
        AssertPoint(r, P(1, -System.Math.Sqrt(3)));
    }

    [Fact]
    public void CircleArc_TangencyInsideSweep_ReturnsTangent()
    {
        var r = Intersect(
            Circle(1, P(0, 0), 2),
            Arc(2, P(4, 0), 2, 180, 90));
        Assert.Equal(CurveIntersectionKind.Tangent, r.Kind);
        Assert.Equal(2, r.Point1.X, 6);
        Assert.Equal(0, r.Point1.Y, 6);
    }

    [Fact]
    public void CircleArc_ConcentricSameRadius_ReturnsOverlap()
    {
        // The arc lies entirely on the circle.
        var r = Intersect(
            Circle(1, P(0, 0), 2),
            Arc(2, P(0, 0), 2, 45, 90));
        Assert.Equal(CurveIntersectionKind.Overlap, r.Kind);
    }

    [Fact]
    public void CircleArc_ConcentricDifferentRadius_ReturnsNone()
    {
        var r = Intersect(
            Circle(1, P(0, 0), 2),
            Arc(2, P(0, 0), 3, 0, 90));
        Assert.Equal(CurveIntersectionKind.None, r.Kind);
    }

    // ------------------------------------------------------------------
    // Run-level (polyline segment) pairs
    // ------------------------------------------------------------------

    [Fact]
    public void SegmentPairs_MixedLineAndArc_TwoPoints()
    {
        var r = IntersectSegments(
            new LineSegment(P(-2, -2), P(2, 2)),
            new ArcSegment(P(0, 0), 2, 45 * Deg, 270 * Deg, true));
        Assert.Equal(CurveIntersectionKind.TwoPoints, r.Kind);
    }

    [Fact]
    public void SegmentPairs_LineSegmentParallel_ReturnsParallel()
    {
        var r = IntersectSegments(
            new LineSegment(P(0, 0), P(10, 0)),
            new LineSegment(P(0, 5), P(10, 5)));
        Assert.Equal(CurveIntersectionKind.Parallel, r.Kind);
    }

    [Fact]
    public void SegmentPairs_CollinearOverlap_ReturnsOverlap()
    {
        var r = IntersectSegments(
            new LineSegment(P(0, 0), P(10, 0)),
            new LineSegment(P(5, 0), P(15, 0)));
        Assert.Equal(CurveIntersectionKind.Overlap, r.Kind);
    }

    [Fact]
    public void CollectBoundaryPoints_MixedPolylineVsLine_Deduplicates()
    {
        // Path A: a polyline made of one line run (x = −3, parallel to the
        // query line) plus one arc run covering 90°..150° (crosses the query
        // line once at angle 138.6°) — one distinct boundary point result.
        IReadOnlyList<IPathSegment> pathA =
        [
            new LineSegment(P(-3, 3), P(-3, -3)),
            new ArcSegment(P(0, 0), 2, 90 * Deg, 60 * Deg, true),
        ];
        IReadOnlyList<IPathSegment> pathB =
        [
            new LineSegment(P(-1.5, 3), P(-1.5, -3)),
        ];

        var pts = IntersectionEngine.CollectBoundaryPoints(pathA, pathB, Tol);
        Assert.Single(pts);
        Assert.Equal(-1.5, pts[0].X, 6);
        double expectedY = 2 * System.Math.Sin(System.Math.Acos(-0.75));
        Assert.Equal(expectedY, pts[0].Y, 6);
    }

    [Fact]
    public void CollectBoundaryPoints_SamePointFoundFromMultipleSegmentPairs_Deduped()
    {
        // Two crossing segments pairs sharing the exact crossing point must
        // yield a single entry.
        IReadOnlyList<IPathSegment> pathA = [new LineSegment(P(-1, 0), P(1, 0))];
        IReadOnlyList<IPathSegment> pathB =
        [
            new LineSegment(P(0, -1), P(0, 1)),
            new LineSegment(P(0, 1), P(0, -1)),
        ];
        var pts = IntersectionEngine.CollectBoundaryPoints(pathA, pathB, Tol);
        Assert.Single(pts);
        Assert.Equal(0, pts[0].X, 6);
        Assert.Equal(0, pts[0].Y, 6);
    }

    // ------------------------------------------------------------------
    // Universal dispatch
    // ------------------------------------------------------------------

    [Fact]
    public void IntersectCurves_DispatchLineArc()
    {
        var r = IntersectionEngine.IntersectCurves(
            Line(1, P(-2, -2), P(2, 2)),
            Arc(2, P(0, 0), 2, 45, 270), Tol);
        Assert.Equal(CurveIntersectionKind.TwoPoints, r.Kind);
    }

    [Fact]
    public void IntersectCurves_DispatchCircleArc()
    {
        var r = IntersectionEngine.IntersectCurves(
            Circle(1, P(0, 0), 2),
            Arc(2, P(2, 0), 2, 120, 240), Tol);
        Assert.Equal(CurveIntersectionKind.TwoPoints, r.Kind);
    }

    [Fact]
    public void IntersectCurves_PolylineRejected()
    {
        var poly = new PolylineGeometry(1, "L",
            [new LineSegment(P(0, 0), P(1, 0))], true);
        Assert.Throws<System.ArgumentException>(() =>
            IntersectionEngine.IntersectCurves(poly, Circle(2, P(0, 0), 1), Tol));
    }
}