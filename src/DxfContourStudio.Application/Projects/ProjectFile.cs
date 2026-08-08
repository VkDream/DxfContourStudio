#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Projects;

/// <summary>
/// The serializable project file model (extension .dxfstudio, JSON).
///
/// Schema versioning: <see cref="SchemaVersion"/> is the format version of
/// the file itself; <see cref="ApplicationVersion"/> records which app
/// version wrote the file. Unknown fields are tolerated on load (forward
/// compatibility); unknown schema versions are rejected.
///
/// The file intentionally does NOT persist analysis results, selection or
/// viewport state — those are derived state rebuilt on load. It DOES persist
/// the full geometry (lossless for Line/Arc/Circle/Polyline), layers, units,
/// tolerance settings and the source-file reference.
/// </summary>
public sealed class ProjectFile
{
    /// <summary>Current schema version of the .dxfstudio format.</summary>
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string ApplicationVersion { get; set; } = "0.2.0";

    public ProjectSourceInfo Source { get; set; } = new();

    public LengthUnit Units { get; set; } = LengthUnit.Millimeter;

    public ToleranceSettings Tolerance { get; set; } = new();

    public DiagnosticSettings Diagnostics { get; set; } = new();

    public List<LayerProjection> Layers { get; set; } = [];

    public List<EntityProjection> Entities { get; set; } = [];
}

/// <summary>Source-file reference recorded at import time (informational).</summary>
public sealed class ProjectSourceInfo
{
    public string? FileName { get; set; }

    public string? FilePath { get; set; }

    public string? DxfVersion { get; set; }

    public string? ImportSummary { get; set; }
}

/// <summary>The tolerance policy persisted with the project.</summary>
public sealed class ToleranceSettings
{
    public double PointEqualityTolerance { get; set; }

    public double EndpointSnapTolerance { get; set; }

    public double ZeroLengthTolerance { get; set; }

    public double ClosureTolerance { get; set; }

    public double SmallGeometryThreshold { get; set; }
}

/// <summary>Diagnostic thresholds persisted with the project.</summary>
public sealed class DiagnosticSettings
{
    public double DuplicateTolerance { get; set; }

    public double SelfIntersectionTolerance { get; set; }
}

/// <summary>Layer projection (visibility is a view state — stored, per ADR-009).</summary>
public sealed class LayerProjection
{
    public string Name { get; set; } = "";

    public bool IsOn { get; set; } = true;

    public bool IsFrozen { get; set; }

    public short AciColorIndex { get; set; } = 7;

    public bool IsColorByLayer { get; set; } = true;

    public bool IsVisible { get; set; } = true;
}

/// <summary>
/// One geometry entity in the project file. <see cref="Kind"/> selects the
/// payload; the shapes below are "geometry as the engine knows it" and are
/// serialized with full precision (invariant culture, round-trip safe).
/// </summary>
public sealed class EntityProjection
{
    public long Id { get; set; }

    public string Kind { get; set; } = ""; // "Line" | "Arc" | "Circle" | "Polyline"

    public string Layer { get; set; } = "";

    public bool Visible { get; set; } = true;

    public LineProjection? Line { get; set; }

    public CircleProjection? Circle { get; set; }

    public ArcProjection? Arc { get; set; }

    public PolylineProjection? Polyline { get; set; }
}

public sealed class LineProjection
{
    public double P0X { get; set; }

    public double P0Y { get; set; }

    public double P1X { get; set; }

    public double P1Y { get; set; }
}

public sealed class CircleProjection
{
    public double CenterX { get; set; }

    public double CenterY { get; set; }

    public double Radius { get; set; }
}

public sealed class ArcProjection
{
    public double CenterX { get; set; }

    public double CenterY { get; set; }

    public double Radius { get; set; }

    /// <summary>Start angle in radians (internal convention, CCW from +X).</summary>
    public double StartAngleRadians { get; set; }

    /// <summary>Signed sweep in radians (+CCW / -CW).</summary>
    public double SweepRadians { get; set; }
}

public sealed class PolylineProjection
{
    public bool IsClosed { get; set; }

    public List<SegmentProjection> Segments { get; set; } = [];
}

public sealed class SegmentProjection
{
    public string Kind { get; set; } = ""; // "Line" | "Arc"

    public double StartX { get; set; }

    public double StartY { get; set; }

    public double EndX { get; set; }

    public double EndY { get; set; }

    // Arc payload (when Kind == "Arc"):
    public double CenterX { get; set; }

    public double CenterY { get; set; }

    public double Radius { get; set; }

    public double StartAngleRadians { get; set; }

    public double SweepRadians { get; set; }
}
