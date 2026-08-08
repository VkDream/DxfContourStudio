#nullable enable

using DxfContourStudio.Application.Selection;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Pixel-based hit testing. The key property: a fixed pixel tolerance must
/// translate to a zoom-dependent world tolerance so picking feels constant
/// when zooming in/out.
/// </summary>
public class HitTestTests
{
    private const double TolPx = HitTester.DefaultPickTolerancePx;

    [Fact]
    public void PixelsToWorld_InverseOfPixelsPerWorld()
    {
        var vp = new Viewport(10.0);
        Assert.Equal(0.6, HitTester.PixelsToWorld(TolPx, vp), 12);

        var vpZoomedOut = new Viewport(0.5);
        Assert.Equal(12.0, HitTester.PixelsToWorld(TolPx, vpZoomedOut), 12);
    }

    [Fact]
    public void PickClosest_Line_HitsWithinTolerance()
    {
        var doc = TestDocs.LineDocument();
        var vp = new Viewport(1.0); // 6px == 6 world units

        IGeometryEntity? hit = HitTester.PickClosest(doc, new Point2(50, 5), TolPx, vp);

        Assert.NotNull(hit);
        Assert.Equal(1L, hit!.Id);
    }

    [Fact]
    public void PickClosest_Line_MissesOutsideTolerance()
    {
        var doc = TestDocs.LineDocument();
        var vp = new Viewport(1.0);

        IGeometryEntity? hit = HitTester.PickClosest(doc, new Point2(50, 7), TolPx, vp);

        Assert.Null(hit);
    }

    [Fact]
    public void PickClosest_Circle_HitsRimOnly()
    {
        var doc = TestDocs.SceneDocument();
        var vp = new Viewport(1.0);
        var center = new Point2(50, 25);

        IGeometryEntity? hit = HitTester.PickClosest(doc, center, TolPx, vp);
        Assert.Null(hit); // dead center of a circle is not on the curve

        hit = HitTester.PickClosest(doc, new Point2(50, 15), TolPx, vp); // 10 below center = on rim
        Assert.NotNull(hit);
        Assert.Equal(2L, hit!.Id);
    }

    [Fact]
    public void PickClosest_Arc_HitsSpanNotCenter()
    {
        var doc = TestDocs.SceneDocument();
        var vp = new Viewport(1.0);
        // Arc: center (10,10) r5, span 0°..90°. A point on the arc at 45°.
        double mid = 5 * Math.Cos(Math.PI / 4); // = 5*cos45 ≈ 3.54
        IGeometryEntity? hit = HitTester.PickClosest(doc, new Point2(10 + mid, 10 + mid), TolPx, vp);
        Assert.NotNull(hit);
        Assert.Equal(3L, hit!.Id);
    }

    [Fact]
    public void PickClosest_Polyline_HitsAnySegment()
    {
        var doc = TestDocs.SceneDocument();
        var vp = new Viewport(1.0);
        // Polyline edge (0,0)-(0,50): point (3, 25) is 3 world units away.
        IGeometryEntity? hit = HitTester.PickClosest(doc, new Point2(3, 25), TolPx, vp);
        Assert.NotNull(hit);
        Assert.Equal(4L, hit!.Id);
    }

    [Fact]
    public void PickClosest_ClosestEntityWins()
    {
        var doc = TestDocs.SceneDocument();
        var vp = new Viewport(1.0);
        // Circle rim: center (50,25) r10. Point (53,10): 5.3mm from rim (inside
        // 6px tol), 10mm from the line y=0 (outside tol) → only the circle hits.
        IGeometryEntity? hit = HitTester.PickClosest(doc, new Point2(53, 10), TolPx, vp);
        Assert.NotNull(hit);
        Assert.Equal(2L, hit!.Id);
    }

    [Fact]
    public void ZoomIn_WorldToleranceShrinks()
    {
        var doc = TestDocs.LineDocument();
        var vpIn = new Viewport(10.0);   // very zoomed in: 6px == 0.6 world
        var vpOut = new Viewport(0.5);   // zoomed out: 6px == 12 world

        // Same physical pixel click offset (5px below the line) picks at the
        // zoomed-out scale but not at the zoomed-in one — the world distance
        // implied by 5px depends on the zoom.
        IGeometryEntity? hitIn = HitTester.PickClosest(doc, new Point2(50, 5), TolPx, vpIn);
        IGeometryEntity? hitOut = HitTester.PickClosest(doc, new Point2(50, 5), TolPx, vpOut);

        Assert.Null(hitIn);
        Assert.NotNull(hitOut);
    }

    [Fact]
    public void HiddenLayer_IsNotPicked()
    {
        var doc = TestDocs.SceneDocument();
        doc.SetLayerVisible("cut", false);
        var vp = new Viewport(1.0);

        IGeometryEntity? hit = HitTester.PickClosest(doc, new Point2(50, 15), TolPx, vp);

        Assert.Null(hit); // circle is on the hidden "cut" layer
    }
}
