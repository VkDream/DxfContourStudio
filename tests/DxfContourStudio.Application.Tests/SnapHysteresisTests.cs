#nullable enable

using DxfContourStudio.Application.Interaction;
using DxfContourStudio.Core.Geometry;
using Xunit;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Hysteresis behaviour of <see cref="SnapHoverController"/>: a snap marker
/// must appear at the acquire radius, stay while the cursor remains within
/// the (larger) release radius, switch immediately to a strictly higher
/// priority candidate, and resist flicker between equal-priority candidates
/// at the same spot. Pure math — no window involved.
/// </summary>
public class SnapHysteresisTests
{
    private const double PixelsPerWorld = 1.0; // world1 == pixel1 keeps math readable

    private static SnapHoverController NewController() => new(new InteractionSettings
    {
        SnapAcquireRadiusPx = 8.0,
        SnapReleaseRadiusPx = 12.0,
        StickyMinDeltaPx = 1.0,
    });

    private static SnapResult SnapAt(SnapKind kind, Point2 world, double distanceWorld) =>
        SnapResult.At(kind, world, [], distanceWorld, distanceWorld);

    [Fact]
    public void CandidateWithinAcquireRadius_ShowsMarker()
    {
        var c = NewController();

        c.Update(new Point2(5, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 5.0), PixelsPerWorld);

        Assert.NotNull(c.Current);
        Assert.Equal(SnapKind.Endpoint, c.Current!.Value.Kind);
    }

    [Fact]
    public void CandidateBeyondAcquireRadius_NoMarker()
    {
        var c = NewController();

        c.Update(new Point2(9, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 9.0), PixelsPerWorld);

        Assert.Null(c.Current);
    }

    [Fact]
    public void HeldMarker_StaysWithinReleaseRadius_AfterAcquire()
    {
        var c = NewController();
        c.Update(new Point2(5, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 5.0), PixelsPerWorld);
        Assert.NotNull(c.Current);

        // Cursor drifts out to 10px: beyond acquire but inside release → the
        // marker must NOT vanish or flicker.
        c.Update(new Point2(10, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 10.0), PixelsPerWorld);

        Assert.NotNull(c.Current);
    }

    [Fact]
    public void HeldMarker_ReleasesBeyondReleaseRadius()
    {
        var c = NewController();
        c.Update(new Point2(5, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 5.0), PixelsPerWorld);

        c.Update(new Point2(13, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 13.0), PixelsPerWorld);

        Assert.Null(c.Current);
    }

    [Fact]
    public void HeldMarker_SurvivesFrameWithNoCandidate_WhileWithinRelease()
    {
        var c = NewController();
        c.Update(new Point2(5, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 5.0), PixelsPerWorld);

        // Engine found nothing this frame (e.g. master still on but out of
        // engine tolerance) yet the cursor is still near the held marker.
        IGeometryEntity[] doc = [];
        var none = SnapEngine.Snap(doc, new Point2(9, 0), 4.0, GeometryTolerance.Default, SnapKinds.None);
        c.Update(new Point2(9, 0), none, PixelsPerWorld);

        Assert.NotNull(c.Current);
    }

    [Fact]
    public void NoCandidate_ReleasesWhenCursorFar()
    {
        var c = NewController();
        c.Update(new Point2(5, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 5.0), PixelsPerWorld);

        IGeometryEntity[] doc = [];
        var none = SnapEngine.Snap(doc, new Point2(20, 0), 4.0, GeometryTolerance.Default, SnapKinds.None);
        c.Update(new Point2(20, 0), none, PixelsPerWorld);

        Assert.Null(c.Current);
    }

    [Fact]
    public void EqualPriorityCandidate_MustBeatStickyByDelta()
    {
        var c = NewController();
        // Two collinear endpoints at the power point (0,0) and (100,0);
        // cursor is at 5 → endpoint at (0,0), 5px away, wins.
        c.Update(new Point2(5, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 5.0), PixelsPerWorld);
        Assert.Equal(new Point2(0, 0), c.Current!.Value.WorldPoint);

        // Cursor nudges to 10: the other endpoint is now farther (90px) and
        // the held one is 10px — still within release. Same kind, and the
        // new candidate is NOT > 1px closer than the held one? It is actually
        // nowhere near — the marker must keep pointing at (0,0).
        c.Update(new Point2(10, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 10.0), PixelsPerWorld);
        Assert.Equal(new Point2(0, 0), c.Current!.Value.WorldPoint);

        // Cursor moves near the OTHER endpoint (95): engine now reports the
        // far endpoint; distance 5px vs held marker 95px → the new candidate
        // is clearly closer, so it must win despite equal priority (sticky
        // only guards jitter within the release radius, not real movement).
        c.Update(new Point2(95, 0), SnapAt(SnapKind.Endpoint, new Point2(100, 0), 5.0), PixelsPerWorld);
        Assert.Equal(new Point2(100, 0), c.Current!.Value.WorldPoint);
    }

    [Fact]
    public void EqualPriorityAtSameSpot_JitterDoesNotFlicker()
    {
        var c = NewController();
        // Two zero-length entries on the same marker: an Endpoint and a
        // Midpoint are different priorities, so simulate two Endpoints a
        // hair apart (classic flicker case: two line endpoints at the same
        // point + cursor subpixel jitter).
        c.Update(new Point2(5.0, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 5.0), PixelsPerWorld);
        Assert.Equal(new Point2(0, 0), c.Current!.Value.WorldPoint);

        // Subpixel jitter: the engine cannot even see a different candidate
        // at the same world spot; same point candidate with a slightly
        // different reported distance must not flip the marker.
        c.Update(new Point2(5.02, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 5.02), PixelsPerWorld);
        c.Update(new Point2(4.98, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 4.98), PixelsPerWorld);
        c.Update(new Point2(5.01, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 5.01), PixelsPerWorld);

        Assert.Equal(new Point2(0, 0), c.Current!.Value.WorldPoint);
    }

    [Fact]
    public void HigherPriorityCandidate_SwitchesImmediately()
    {
        var c = NewController();
        c.Update(new Point2(5, 0), SnapAt(SnapKind.Midpoint, new Point2(0, 0), 5.0), PixelsPerWorld);
        Assert.Equal(SnapKind.Midpoint, c.Current!.Value.Kind);

        // An endpoint appears 6px away while the held midpoint is 5px — the
        // endpoint has strictly higher priority (SnapEngine order), so it
        // must take over even though it is not closer in px.
        c.Update(new Point2(5, 0), SnapAt(SnapKind.Endpoint, new Point2(1, 1), 5.8), PixelsPerWorld);

        Assert.Equal(SnapKind.Endpoint, c.Current!.Value.Kind);
    }

    [Fact]
    public void LowerPriorityCandidate_DoesNotDisplaceHeldMarker()
    {
        var c = NewController();
        c.Update(new Point2(5, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 5.0), PixelsPerWorld);

        c.Update(new Point2(5, 0), SnapAt(SnapKind.Intersection, new Point2(5, 0), 0.0), PixelsPerWorld);

        Assert.Equal(SnapKind.Endpoint, c.Current!.Value.Kind);
    }

    [Fact]
    public void Reset_ForgetsMarker()
    {
        var c = NewController();
        c.Update(new Point2(5, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 5.0), PixelsPerWorld);

        c.Reset();

        Assert.Null(c.Current);
    }

    [Fact]
    public void NoStickyAcrossDocuments_FreshAcquireRequired()
    {
        var c = NewController();
        c.Update(new Point2(5, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 5.0), PixelsPerWorld);
        c.Reset(); // document swap / Esc resets

        // New document: candidate 9px away → inside engine tolerance but
        // beyond acquire radius of the controller → no marker until closer.
        c.Update(new Point2(9, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 9.0), PixelsPerWorld);
        Assert.Null(c.Current);

        c.Update(new Point2(7, 0), SnapAt(SnapKind.Endpoint, new Point2(0, 0), 7.0), PixelsPerWorld);
        Assert.NotNull(c.Current);
    }
}