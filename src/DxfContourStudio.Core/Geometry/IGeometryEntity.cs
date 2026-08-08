namespace DxfContourStudio.Core.Geometry;

/// <summary>
    /// Common contract of every geometry entity in the document.
    ///
    /// Core geometry/topology algorithms operate exclusively on
    /// <see cref="IGeometryEntity"/>; the WPF layer is never needed to read
    /// or write these, and rendering concerns (pens, brushes) stay out of the
    /// domain model.
    /// </summary>
public interface IGeometryEntity
{
    /// <summary>Stable, unique id within the containing document.</summary>
    long Id { get; set; }

    /// <summary>The category of this entity.</summary>
    GeometryType GeometryType { get; }

    /// <summary>The name of the DXF layer this entity belongs to ("" when none).</summary>
    string LayerName { get; }

    /// <summary>Whether the entity participates in rendering / selection.
    /// Visibility is a presentation concept and must not change the geometry
    /// itself; but the flag lives here because DXF carries it per entity.</summary>
    bool IsVisible { get; set; }

    /// <summary>Axis-aligned bounds in world coordinates (millimeters).</summary>
    Bounds Bounds { get; }

    /// <summary>Total path length in millimeters.</summary>
    double Length { get; }

    /// <summary>Start point of the entity in its construction order (for closed primitives see note).</summary>
    Point2 StartPoint { get; }

    /// <summary>End point of the entity in its construction order (for closed primitives see note).</summary>
    Point2 EndPoint { get; }

    /// <summary>
    /// Returns a deep copy. Implementations must guarantee the copied entity
    /// is safe to use and to be replaced in a document without side effects,
    /// because undo/redo and selection rely on the copy semantics.
    /// </summary>
    IGeometryEntity Clone();

    /// <summary>
    /// Returns this geometry transformed by an affine <paramref name="transform"/>.
    /// The caller owns the new instance (the original is immutable).
    /// </summary>
    IGeometryEntity Transformed(Transform2 transform);

    /// <summary>
    /// Distance from <paramref name="p"/> to the geometry curve, in millimeters.
    /// Implementations must handle the degenerate cases documented in
    /// <see cref="GeometryTolerance"/>.
    /// </summary>
    double DistanceToPoint(Point2 p);

    /// <summary>
    /// Point on the curve at normalized parameter <paramref name="t"/> in [0,1].
    /// </summary>
    Point2 PointAtParameter(double t);

    /// <summary>Unit tangent direction at parameter <paramref name="t"/> in [0,1].</summary>
    Vector2 TangentAt(double t);
}