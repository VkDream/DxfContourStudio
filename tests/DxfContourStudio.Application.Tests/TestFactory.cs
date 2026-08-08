#nullable enable

using DxfContourStudio.Application.Documents;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Shared helpers to build small test documents without DXF files.
/// Entities are created with explicit ids so tests can assert on them.
/// </summary>
internal static class TestDocs
{
    /// <summary>A document with one horizontal line (0,0)-(100,0) on layer "0".</summary>
    public static CadDocument LineDocument()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [new LineGeometry(1, "0", new Point2(0, 0), new Point2(100, 0))],
            [new LayerState("0", IsOn: true, IsFrozen: false, AciColorIndex: 7, IsColorByLayer: true)],
            null, null, null);
        return doc;
    }

    /// <summary>Rich multi-entity scene: line, circle, arc, polyline.</summary>
    public static CadDocument SceneDocument()
    {
        var doc = new CadDocument();
        doc.ReplaceContent(
            [
                new LineGeometry(1, "0", new Point2(0, 0), new Point2(100, 0)),
                new CircleGeometry(2, "cut", new Point2(50, 25), 10),
                new ArcGeometry(3, "cut", new Point2(10, 10), 5, startAngleRadians: 0, sweepRadians: Math.PI / 2),
                new PolylineGeometry(4, "mark", [new LineSegment(new Point2(0, 0), new Point2(0, 50)), new LineSegment(new Point2(0, 50), new Point2(50, 50)), new LineSegment(new Point2(50, 50), new Point2(0, 0))], isClosed: true),
            ],
            [
                new LayerState("0", IsOn: true, IsFrozen: false, AciColorIndex: 7, IsColorByLayer: true),
                new LayerState("cut", IsOn: true, IsFrozen: false, AciColorIndex: 1, IsColorByLayer: true),
                new LayerState("mark", IsOn: true, IsFrozen: false, AciColorIndex: 2, IsColorByLayer: true),
            ],
            null, null, null);
        return doc;
    }
}