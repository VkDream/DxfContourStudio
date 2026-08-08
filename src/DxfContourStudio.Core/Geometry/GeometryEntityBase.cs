namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// Common base for all concrete geometry entities.
///
/// Stores the identity, layer and visibility shared by every entity and
/// provides a standard, tolerance-aware comparison utility. Entities are
/// immutable by convention: mutating operations return new copies.
/// </summary>
public abstract class GeometryEntityBase : IGeometryEntity
{
    /// <inheritdoc />
    public long Id { get; set; }

    /// <inheritdoc />
    public abstract GeometryType GeometryType { get; }

    /// <inheritdoc />
    public string LayerName { get; }

    /// <inheritdoc />
    public bool IsVisible { get; set; }

    protected GeometryEntityBase(long id, string layerName, bool isVisible = true)
    {
        Id = id;
        LayerName = layerName;
        IsVisible = isVisible;
    }

    /// <inheritdoc />
    public abstract Bounds Bounds { get; }

    /// <inheritdoc />
    public abstract double Length { get; }

    /// <inheritdoc />
    public abstract Point2 StartPoint { get; }

    /// <inheritdoc />
    public abstract Point2 EndPoint { get; }

    /// <inheritdoc />
    public abstract IGeometryEntity Clone();

    /// <inheritdoc />
    public abstract IGeometryEntity Transformed(Transform2 transform);

    /// <inheritdoc />
    public abstract double DistanceToPoint(Point2 p);

    /// <inheritdoc />
    public abstract Point2 PointAtParameter(double t);

    /// <inheritdoc />
    public abstract Vector2 TangentAt(double t);
}