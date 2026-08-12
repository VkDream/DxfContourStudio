#nullable enable

using DxfContourStudio.Application.Interaction;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// D13B — grip contract tests: the descriptors produced for each entity type
/// pin the exact handles the node-edit interaction (D14) will drag.
/// </summary>
public class GripBuilderTests
{
    [Fact]
    public void Line_ProducesStartAndEndGrips()
    {
        var line = new LineGeometry(7, "0", new Point2(1, 2), new Point2(9, 8));
        var grips = GripBuilder.Build(line);

        Assert.Equal(2, grips.Count);
        Assert.Equal(GripKind.LineStart, grips[0].Kind);
        Assert.Equal(new Point2(1, 2), grips[0].WorldPosition);
        Assert.Equal(GripKind.LineEnd, grips[1].Kind);
        Assert.Equal(new Point2(9, 8), grips[1].WorldPosition);
        Assert.Equal(7, grips[0].EntityId);
    }

    [Fact]
    public void Circle_ProducesCenterAndRadiusGrips()
    {
        var circle = new CircleGeometry(3, "0", new Point2(10, 20), 5);
        var grips = GripBuilder.Build(circle);

        Assert.Equal(2, grips.Count);
        Assert.Equal(GripKind.CircleCenter, grips[0].Kind);
        Assert.Equal(new Point2(10, 20), grips[0].WorldPosition);
        Assert.Equal(GripKind.CircleRadius, grips[1].Kind);
        Assert.Equal(new Point2(15, 20), grips[1].WorldPosition);
    }

    [Fact]
    public void Arc_ProducesCenterStartAndEndGrips()
    {
        var arc = new ArcGeometry(1, "0", new Point2(0, 0), 10.0, 0, Math.PI / 2);
        var grips = GripBuilder.Build(arc);

        Assert.Equal(3, grips.Count);
        Assert.Equal(GripKind.ArcCenter, grips[0].Kind);
        Assert.Equal(GripKind.ArcStart, grips[1].Kind);
Assert.Equal(GripKind.ArcEnd, grips[2].Kind);
        Assert.True(new Point2(10, 0).DistanceTo(grips[1].WorldPosition) < 1e-9);
        Assert.True(new Point2(0, 10).DistanceTo(grips[2].WorldPosition) < 1e-9);
    }

    [Fact]
    public void Arc_AcrossZero_KeepsStartAndEndSemantics()
    {
        // Sweep 350°→10° (start 350°, counter-clockwise sweep 20°): the grips
        // must land on the true endpoints, not the trivial min/max angles.
        var arc = new ArcGeometry(2, "0", new Point2(0, 0), 10.0, Math.PI * 35.0 / 18.0, Math.PI / 9.0);
        var grips = GripBuilder.Build(arc);
        Assert.Equal(3, grips.Count);
        double expectedStartX = 10.0 * Math.Cos(arc.StartAngleRadians);
        double expectedStartY = 10.0 * Math.Sin(arc.StartAngleRadians);
        Assert.Equal(expectedStartX, grips[1].WorldPosition.X, 1e-6);
        Assert.Equal(expectedStartY, grips[1].WorldPosition.Y, 1e-6);
    }

    [Fact]
    public void OpenPolyline_OneGripPerVertexIncludingTerminal()
    {
        var segments = new IPathSegment[]
        {
            new LineSegment(new Point2(0, 0), new Point2(10, 0)),
            new ArcSegment(new Point2(10, 0), 5.0, 0, Math.PI / 2, true),
            new LineSegment(new Point2(10, 5), new Point2(20, 5)),
        };
        var poly = new PolylineGeometry(3, "0", segments, isClosed: false);
        var grips = GripBuilder.Build(poly);

Assert.Equal(4, grips.Count); // 3 starts + terminal end
        Assert.Equal(GripKind.PolylineVertex, grips[0].Kind);
        Assert.Equal(0, grips[0].Parameter);
        Assert.True(new Point2(0, 0).DistanceTo(grips[0].WorldPosition) < 1e-9);
        Assert.True(new Point2(15, 0).DistanceTo(grips[1].WorldPosition) < 1e-9);
        Assert.Equal(new Point2(10, 5), grips[2].WorldPosition);
        Assert.True(new Point2(20, 5).DistanceTo(grips[3].WorldPosition) < 1e-9);
        Assert.Equal(3, grips[3].Parameter); // terminal vertex index
    }

    [Fact]
    public void ClosedPolyline_OneGripPerVertex_NoDuplicateTerminal()
    {
        var segments = new IPathSegment[]
        {
            new LineSegment(new Point2(0, 0), new Point2(10, 0)),
            new LineSegment(new Point2(10, 0), new Point2(10, 10)),
            new LineSegment(new Point2(10, 10), new Point2(0, 10)),
        };
        var poly = new PolylineGeometry(4, "0", segments, isClosed: true);
        var grips = GripBuilder.Build(poly);

        // Closed: 3 vertices (last segment returns to the first, no 4th grip).
        Assert.Equal(3, grips.Count);
        Assert.Equal(new Point2(0, 0), grips[0].WorldPosition);
        Assert.Equal(new Point2(10, 0), grips[1].WorldPosition);
        Assert.Equal(new Point2(10, 10), grips[2].WorldPosition);
    }

    [Fact]
    public void PolylineWithoutSegments_NoGrips()
    {
var poly = new PolylineGeometry(4, "0", Array.Empty<IPathSegment>(), isClosed: false);
        Assert.Empty(GripBuilder.Build(poly));
    }
}
