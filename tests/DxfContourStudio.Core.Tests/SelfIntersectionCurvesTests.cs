#nullable enable

using System;
using System.Collections.Generic;
using DxfContourStudio.Core.Diagnostics;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Tests;

/// <summary>
/// D2 milestone tests: the self-intersection analyzer with arc support.
/// Golden cases D5 (line-arc crossing) and D6 (arc-arc crossing), plus the
/// exclusion rules: adjacent runs, closing run, and tangencies.
/// </summary>
public class SelfIntersectionCurvesTests
{
    private static readonly GeometryTolerance Tol = GeometryTolerance.Default;

    private const double Deg = Math.PI / 180.0;

    private static Point2 P(double x, double y) => new(x, y);

    private static IReadOnlyList<GeometryDiagnostic> Analyze(
        IReadOnlyList<IGeometryEntity> entities, IReadOnlyList<long> contour)
    {
        var byId = new Dictionary<long, IGeometryEntity>();
        foreach (var e in entities)
        {
            byId[e.Id] = e;
        }

        return SelfIntersectionAnalyzer.Analyze([contour], id => byId[id], Tol);
    }

    [Fact]
    public void LineArcCrossing_Detected_GoldenD5()
    {
        // Contour [arc, line2, line3, line4]: the arc (c=(2,0) r=1.5, sweep
        // 90..270) crosses run 3 (the closing straight run (6,0)→(0,0)) at
        // (3.5,0)/(0.5,0); runs 0↔3 would be adjacent-only in a 3-run area,
        // so a fourth run keeps the crossing pair non-adjacent.
        var arc = new ArcGeometry(1, "L", P(2, 0), 1.5, 90 * Deg, 270 * Deg);
        var line2 = new LineGeometry(2, "L", P(2, 1.5), P(6, 1.5));
        var line3 = new LineGeometry(3, "L", P(6, 0), P(0, 0));
        var line4 = new LineGeometry(4, "L", P(0, 0), P(0, 2));

        var findings = Analyze([arc, line2, line3, line4], [1, 2, 3, 4]);

        var f = Assert.Single(findings, d => d.Kind == DiagnosticKind.SelfIntersection);
        Assert.Equal(1, f.EntityIdA);
        Assert.Equal(3, f.EntityIdB);
        Assert.True(f.PositionA.DistanceTo(P(3.5, 0)) <= 1e-6 || f.PositionA.DistanceTo(P(0.5, 0)) <= 1e-6,
            $"unexpected crossing {f.PositionA}");
    }

    [Fact]
    public void ArcArcCrossing_Detected_GoldenD6()
    {
        // Contour [arc1, line1, arc2, line2]: arc1 (c=(0,0) r=2, 45..135) and
        // arc2 (c=(2,0) r=2, 120..180) cross once at (1, √3).
        var arc1 = new ArcGeometry(1, "L", P(0, 0), 2, 45 * Deg, 90 * Deg);
        var line1 = new LineGeometry(2, "L", P(1.414, 1.414), P(1.414, 3));
        var arc2 = new ArcGeometry(3, "L", P(2, 0), 2, 120 * Deg, 60 * Deg);
        var line2 = new LineGeometry(4, "L", P(1, 1.732), P(-1, 3));

        var findings = Analyze([arc1, line1, arc2, line2], [1, 2, 3, 4]);

        var f = Assert.Single(findings, d => d.Kind == DiagnosticKind.SelfIntersection);
        Assert.Equal(1, f.EntityIdA);
        Assert.Equal(3, f.EntityIdB);
        Assert.True(f.PositionA.DistanceTo(P(1, Math.Sqrt(3))) <= 1e-6, $"unexpected crossing {f.PositionA}");
    }

    [Fact]
    public void ArcTangentContact_NotReported()
    {
        // arc1 covers 330..30 (includes 0°), arc2 covers 150..210 (includes
        // 180°): their supporting circles are externally tangent at (2,0) —
        // a touch, not a crossing → no finding.
        var arc1 = new ArcGeometry(1, "L", P(0, 0), 2, 330 * Deg, 60 * Deg);
        var line1 = new LineGeometry(2, "L", P(1.732, -1), P(1.732, 3));
        var arc2 = new ArcGeometry(3, "L", P(4, 0), 2, 150 * Deg, 60 * Deg);
        var line2 = new LineGeometry(4, "L", P(2.268, 1), P(2.268, -1));

        var findings = Analyze([arc1, line1, arc2, line2], [1, 2, 3, 4]);

        Assert.DoesNotContain(findings, d => d.Kind == DiagnosticKind.SelfIntersection);
    }

    [Fact]
    public void BulgedPolylineArcRun_CrossingNonAdjacentLine_Detected()
    {
        // Closed polyline of 4 runs: line (0,0)→(4,0), arc c=(2,0) r=1.5
        // sweep 90..270, line (2,−1.5)→(4,−1.5), line (4,−1.5)→(0,0).
        // The closing line crosses the arc run (non-adjacent pair 1↔3).
        var poly = new PolylineGeometry(1, "L",
        [
            new LineSegment(P(0, 0), P(4, 0)),
            new ArcSegment(P(2, 0), 1.5, 90 * Deg, 180 * Deg, true),
            new LineSegment(P(2, -1.5), P(4, -1.5)),
            new LineSegment(P(4, -1.5), P(0, 0)),
        ], isClosed: true);

        var findings = Analyze([poly], [1]);

        var f = Assert.Single(findings, d => d.Kind == DiagnosticKind.SelfIntersection);
        Assert.Equal(1, f.EntityIdA);
        Assert.Equal(1, f.EntityIdB);
    }

    [Fact]
    public void CleanShapeWithBulge_NoFindings()
    {
        // A rounded rectangle-like contour: line, arc corner (right side),
        // top line, closing line — nothing crosses.
        var poly = new PolylineGeometry(1, "L",
        [
            new LineSegment(P(0, 0), P(2, 0)),
            new ArcSegment(P(2, 0), 0.5, 270 * Deg, 90 * Deg, true),
            new LineSegment(P(2.5, 0), P(3, 1)),
            new LineSegment(P(3, 1), P(0, 0)),
        ], isClosed: true);

        var findings = Analyze([poly], [1]);

        Assert.DoesNotContain(findings, d => d.Kind == DiagnosticKind.SelfIntersection);
    }

    [Fact]
    public void AdjacentArcAndLineSharingEndpoint_NotReported()
    {
        // Arc ends exactly where the next line starts: normal corner.
        var arc = new ArcGeometry(1, "L", P(0, 0), 2, 0 * Deg, 90 * Deg);
        var line = new LineGeometry(2, "L", P(0, 2), P(0, 4));

        var findings = Analyze([arc, line], [1, 2]);

        Assert.DoesNotContain(findings, d => d.Kind == DiagnosticKind.SelfIntersection);
    }

    [Fact]
    public void ArcCrossingOutsideItsSweep_NotReported()
    {
        // Supporting circles cross, but the candidate crossing lies outside
        // the arc sweep — the contour stays valid.
        var arc1 = new ArcGeometry(1, "L", P(0, 0), 2, 0 * Deg, 45 * Deg);
        var line1 = new LineGeometry(2, "L", P(1.414, 1.414), P(3, 3));
        var arc2 = new ArcGeometry(3, "L", P(2, 0), 2, 120 * Deg, 60 * Deg);
        var line2 = new LineGeometry(4, "L", P(1, 1.732), P(-2, 2));

        // arc1×arc2 supporting circles meet at (1,±√3); the 60° one is outside
        // arc1's 0..45 sweep → no crossing on the actual geometry.
        var findings = Analyze([arc1, line1, arc2, line2], [1, 2, 3, 4]);

        Assert.DoesNotContain(findings, d => d.Kind == DiagnosticKind.SelfIntersection);
    }
}