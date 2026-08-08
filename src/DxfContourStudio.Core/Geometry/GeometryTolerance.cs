namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// Central policy object for all numeric tolerances used across geometry,
/// topology and contour algorithms.
///
/// The whole codebase must read tolerances from here instead of scattering
/// magic constants like 0.001 / 0.01 / 1e-6 in individual algorithms, so
/// that the behavior of gap closing, endpoint matching, snapping and zero
/// detection can be tuned in one place (and later from the UI).
/// </summary>
public sealed class GeometryTolerance
{
    /// <summary>
    /// The production default policy. All values are in millimeters because
    /// the engine canonicalizes coordinates to millimeters.
    /// </summary>
    public static GeometryTolerance Default { get; } = new();

    /// <summary>
    /// Maximum distance below which two points are considered geometrically
    /// identical. Default 1e-6 mm — pure floating-point noise.
    /// </summary>
    public double PointEqualityTolerance { get; set; } = 1e-6;

    /// <summary>
    /// Maximum distance for which two endpoints are considered connectable
    /// during topology building (a "small gap" that can be repaired).
    /// Default 0.05 mm, matching the UI preset "0.05 mm".
    /// </summary>
    public double EndpointSnapTolerance { get; set; } = 0.05;

    /// <summary>
    /// Maximum length below which a segment is considered zero-length and
    /// dropped with a diagnostic. Default 1e-6 mm.
    /// </summary>
    public double ZeroLengthTolerance { get; set; } = 1e-6;

    /// <summary>
    /// Maximum length below which geometry is flagged as "very small"
    /// (a Warning, distinct from zero length). Default 0.01 mm.
    /// </summary>
    public double SmallGeometryThreshold { get; set; } = 0.01;

    /// <summary>
    /// Tolerance used to decide that two entities describe the same geometry
    /// (duplicate detection). Default 1e-6 mm.
    /// </summary>
    public double DuplicateTolerance { get; set; } = 1e-6;

    /// <summary>
    /// Tolerance used by the self-intersection pass when deciding whether two
    /// nearly-touching segments count as a crossing. Default 1e-6 mm.
    /// </summary>
    public double SelfIntersectionTolerance { get; set; } = 1e-6;

    /// <summary>
    /// Maximum distance between the two endpoints of an open chain to still
    /// call it "closable" during contour building. Default 0.05 mm.
    /// </summary>
    public double ClosureTolerance { get; set; } = 0.05;

    /// <summary>
    /// Minimum angle (radians) used to decide whether two directions are
    /// equal or whether a corner is a 180-degree "straight" joint.
    /// Default 1e-3 rad (~0.057°).
    /// </summary>
    public double AngleTolerance { get; set; } = 1e-3;

    /// <summary>
    /// Maximum angle (radians) between two directions to consider a point
    /// collinear (used by join/merge decisions). Default 1e-3 rad.
    /// </summary>
    public double CollinearTolerance { get; set; } = 1e-3;

    /// <summary>
    /// Minimum area (mm²) below which a closed contour is flagged as a
    /// degenerate / very small contour. Default 1e-6 mm².
    /// </summary>
    public double MinimumAreaTolerance { get; set; } = 1e-6;
}
