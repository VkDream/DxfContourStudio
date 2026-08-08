namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// The concrete kind of a piece of geometry. This is the internal vocabulary
/// used by the Core layer; DXF entity types are mapped onto these values by
/// the import layer and are intentionally distinct from it.
/// </summary>
public enum GeometryType
{
    /// <summary>No geometry / placeholder. Never produced by normal imports.</summary>
    Unknown = 0,

    /// <summary>Straight segment.</summary>
    Line = 1,

    /// <summary>Circular arc (partial circle).</summary>
    Arc = 2,

    /// <summary>Full circle (a closed primitive).</summary>
    Circle = 3,

    /// <summary>Polyline composed of line/arc segments, possibly closed.</summary>
    Polyline = 4,

    /// <summary>Ellipse — reserved (P1 geometry model, not part of first import).</summary>
    Ellipse = 5,

    /// <summary>Spline — reserved; typically tessellated before entering contour logic.</summary>
    Spline = 6,
}