#nullable enable

using DxfContourStudio.Application.Selection;
using DxfContourStudio.Core.Geometry;
using Xunit;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Pure-math tests for the pan gesture (Viewport.PanByScreen): content follows
/// the pointer 1:1 (drag right = content moves right), the Y axis flip
/// (screen down = world up) is honoured, and world points track the expected
/// centre shift at any zoom level. No window or GUI automation.
/// </summary>
public class PanTests
{
    [Fact]
    public void PanByScreen_DragRight_MovesCentreLeft_SameAmountWorld()
    {
        var viewport = new Viewport();
        viewport.Pan(new Point2(100, 100));

        viewport.PanByScreen(120.0, 0.0);

        // At scale 1.0, 120 screen px right = 120 world units left.
        Assert.Equal(100 - 120.0, viewport.Center.X, 9);
        Assert.Equal(100.0, viewport.Center.Y, 9);
    }

    [Fact]
    public void PanByScreen_DragDown_MovesCentreUp()
    {
        var viewport = new Viewport();
        

        viewport.PanByScreen(0.0, 80.0);

        // Screen y grows downwards; the content follows the pointer so the
        // centre moves UP in world space.
        Assert.Equal(0.0, viewport.Center.X, 9);
        Assert.Equal(80.0, viewport.Center.Y, 9);
    }

    [Fact]
    public void PanByScreen_ScaleAware_DragMovesLessWorldWhenZoomed()
    {
        var viewport = new Viewport();
        
        viewport.ZoomAt(4.0);

        viewport.PanByScreen(120.0, 0.0);

        // 120 px at 4 px/world = 30 world units.
        Assert.Equal(-30.0, viewport.Center.X, 9);
    }

    [Fact]
    public void PanByScreen_OneToOneTrack_WorldPointFollowsPointer()
    {
        var viewport = new Viewport();
        viewport.Pan(new Point2(5, 5));

        Point2 before = viewport.ScreenToWorld(new Point2(300, 200), 1200, 700);
        viewport.PanByScreen(-150.0, 60.0); // drag left + down
        Point2 after = viewport.ScreenToWorld(new Point2(300 - 150, 200 + 60), 1200, 700);

        Assert.Equal(before.X, after.X, 9);
        Assert.Equal(before.Y, after.Y, 9);
    }
}