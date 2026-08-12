#nullable enable

using System;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Tests;

/// <summary>
/// D3 milestone tests for <see cref="SnapEngine"/>: endpoint / midpoint /
/// center / intersection / nearest snapping, kind priority and the
/// pixel-to-world tolerance contract (engine takes world-mm tolerance).
/// Golden cases D7 (endpoint snap) and D8 (intersection snap).
/// </summary>
public class SnapEngineTests
{
    private static readonly GeometryTolerance Tol = GeometryTolerance.Default;

    private static Point2 P(double x, double y) => new(x, y);

    private static LineGeometry Line(long id, Point2 a, Point2 b) => new(id, "L", a, b);

    [Fact]
    public void EndpointSnap_NearLineEnd_AttractsToEndpoint_GoldenD7()
    {
        // Query 5px-units away (world tolerance 0.5) from the line end (100,0).
        var line = Line(1, P(0, 0), P(100, 0));
        var result = SnapEngine.Snap([line], P(100.3, 0.2), 0.5, Tol);

        Assert.True(result.IsValid);
        Assert.Equal(SnapKind.Endpoint, result.Kind);
        Assert.Equal(100, result.WorldPoint.X, 6);
        Assert.Equal(0, result.WorldPoint.Y, 6);
        Assert.Equal([1L], result.SourceEntityIds);
    }

    [Fact]
    public void EndpointSnap_ClosestOfTwoEndpointsWins()
    {
        var a = Line(1, P(0, 0), P(10, 0));
        var b = Line(2, P(20, 0), P(30, 0));
        var result = SnapEngine.Snap([a, b], P(9.9, 0.05), 1.0, Tol);

        Assert.True(result.IsValid);
        Assert.Equal(SnapKind.Endpoint, result.Kind);
        Assert.Equal(10, result.WorldPoint.X, 6);
    }

    [Fact]
    public void EndpointSnap_ArcEndpointsSnapped()
    {
        // Arc sweep 0..90 → endpoints (2,0) and (0,2).
        var arc = new ArcGeometry(1, "L", P(0, 0), 2, 0, Math.PI / 2);
        var result = SnapEngine.Snap([arc], P(0.05, 1.95), 0.2, Tol);

        Assert.True(result.IsValid);
        Assert.Equal(SnapKind.Endpoint, result.Kind);
        Assert.Equal(0, result.WorldPoint.X, 6);
        Assert.Equal(2, result.WorldPoint.Y, 6);
    }

    [Fact]
    public void MidpointSnap_SnapsToLineMiddle()
    {
        var line = Line(1, P(0, 0), P(10, 0));
        var result = SnapEngine.Snap([line], P(5.1, 0.3), 0.8, Tol, SnapKinds.Midpoint);

        Assert.True(result.IsValid);
        Assert.Equal(SnapKind.Midpoint, result.Kind);
        Assert.Equal(5, result.WorldPoint.X, 6);
        Assert.Equal(0, result.WorldPoint.Y, 6);
    }

    [Fact]
    public void MidpointSnap_ArcMiddleSnapped()
    {
        var arc = new ArcGeometry(1, "L", P(0, 0), 2, 0, Math.PI / 2);
        var result = SnapEngine.Snap([arc], P(1.41, 1.41), 0.1, Tol, SnapKinds.Midpoint);

        Assert.True(result.IsValid);
        Assert.Equal(SnapKind.Midpoint, result.Kind);
        Assert.Equal(2 * Math.Cos(Math.PI / 4), result.WorldPoint.X, 6);
        Assert.Equal(2 * Math.Sin(Math.PI / 4), result.WorldPoint.Y, 6);
    }

    [Fact]
    public void CenterSnap_CircleCenterSnapped()
    {
        var circle = new CircleGeometry(1, "L", P(5, 5), 3);
        var result = SnapEngine.Snap([circle], P(5.2, 4.8), 0.5, Tol, SnapKinds.Center);

        Assert.True(result.IsValid);
        Assert.Equal(SnapKind.Center, result.Kind);
        Assert.Equal(5, result.WorldPoint.X, 6);
        Assert.Equal(5, result.WorldPoint.Y, 6);
    }

    [Fact]
    public void CenterSnap_ArcCenterSnapped()
    {
        var arc = new ArcGeometry(1, "L", P(2, 3), 2, 0, 1);
        var result = SnapEngine.Snap([arc], P(2.1, 2.9), 0.3, Tol, SnapKinds.Center);

        Assert.True(result.IsValid);
        Assert.Equal(SnapKind.Center, result.Kind);
        Assert.Equal(2, result.WorldPoint.X, 6);
        Assert.Equal(3, result.WorldPoint.Y, 6);
    }

    [Fact]
    public void IntersectionSnap_NearCrossing_SnapsToCrossing_GoldenD8()
    {
        var a = Line(1, P(0, 0), P(10, 10));
        var b = Line(2, P(0, 10), P(10, 0));
        var result = SnapEngine.Snap([a, b], P(5.05, 4.95), 0.2, Tol, SnapKinds.Intersection);

        Assert.True(result.IsValid);
        Assert.Equal(SnapKind.Intersection, result.Kind);
        Assert.Equal(5, result.WorldPoint.X, 6);
        Assert.Equal(5, result.WorldPoint.Y, 6);
        Assert.Equal([1L, 2L], result.SourceEntityIds);
    }

    [Fact]
    public void IntersectionSnap_LineArcCrossing_Snapped()
    {
        var line = Line(1, P(-2, 1), P(2, 1));
        var arc = new ArcGeometry(2, "L", P(0, 0), 2, 0, Math.PI * 1.5);
        var result = SnapEngine.Snap([line, arc], P(1.6, 1.05), 0.3, Tol, SnapKinds.Intersection);

        Assert.True(result.IsValid);
        Assert.Equal(SnapKind.Intersection, result.Kind);
        Assert.Equal(Math.Sqrt(3), result.WorldPoint.X, 5);
        Assert.Equal(1, result.WorldPoint.Y, 5);
    }

    [Fact]
    public void NearestSnap_ProjectionOnLine_EvenWithoutCornerSnaps()
    {
        var line = Line(1, P(0, 0), P(10, 0));
        var result = SnapEngine.Snap([line], P(3, 0.4), 1.0, Tol, SnapKinds.Nearest);

        Assert.True(result.IsValid);
        Assert.Equal(SnapKind.Nearest, result.Kind);
        Assert.Equal(3, result.WorldPoint.X, 6);
        Assert.Equal(0, result.WorldPoint.Y, 6);
    }

    [Fact]
    public void Priority_EndpointBeatsMidpointAndIntersection()
    {
        // Query close to line a's end; line b ends there too (T shape).
        var a = Line(1, P(0, 0), P(10, 0));
        var b = Line(2, P(10, 0), P(10, 10));
        var result = SnapEngine.Snap([a, b], P(9.99, 0.02), 0.5, Tol, SnapKinds.Default);

        Assert.True(result.IsValid);
        Assert.Equal(SnapKind.Endpoint, result.Kind);
        Assert.Equal(10, result.WorldPoint.X, 6);
        Assert.Equal(0, result.WorldPoint.Y, 6);
    }

    [Fact]
    public void Priority_IntersectionBeatsMidpoint()
    {
        var a = Line(1, P(0, 0), P(10, 10));
        var b = Line(2, P(0, 10), P(10, 0));
        // Query between midpoint (5.9,5.9 on a?) and crossing (5,5).
        var result = SnapEngine.Snap([a, b], P(5.05, 5.05), 1.0, Tol,
            SnapKinds.Endpoint | SnapKinds.Midpoint | SnapKinds.Intersection);

        Assert.True(result.IsValid);
        Assert.Equal(SnapKind.Intersection, result.Kind);
        Assert.Equal(5, result.WorldPoint.X, 6);
        Assert.Equal(5, result.WorldPoint.Y, 6);
    }

    [Fact]
    public void Tolerance_QueryOutsideRadius_ReturnsNone()
    {
        var line = Line(1, P(0, 0), P(100, 0));
        var result = SnapEngine.Snap([line], P(103, 0.7), 0.5, Tol);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void DisabledKinds_NotEvaluated()
    {
        // Only Midpoint is enabled and the midpoint is out of tolerance → none.
        var line = Line(1, P(0, 0), P(10, 0));
        var result = SnapEngine.Snap([line], P(0.05, 0.02), 0.5, Tol, SnapKinds.Midpoint);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Circle_EndpointAndMidpointNotAvailable_OnlyCenterAndNearest()
    {
        var circle = new CircleGeometry(1, "L", P(0, 0), 5);
        // 'on the rim near (5,0)' — endpoint/midpoint are skipped for circles,
        // so with only those kinds enabled nothing snaps.
        var snapped = SnapEngine.Snap([circle], P(4.9, 0.2), 1.0, Tol, SnapKinds.Endpoint | SnapKinds.Midpoint);

        Assert.False(snapped.IsValid);
    }

    [Fact]
    public void EmptyCandidates_ReturnsNone()
    {
        var result = SnapEngine.Snap([], P(0, 0), 1.0, Tol);
        Assert.False(result.IsValid);
    }
}