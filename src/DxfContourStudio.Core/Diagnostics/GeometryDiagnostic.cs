#nullable enable

using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Core.Diagnostics;

/// <summary>
/// Severity of a diagnostic finding. Drives the UI icon/color and the
/// "fixability" hints: <see cref="Warning"/> findings are suspicious but the
/// drawing may still be usable; <see cref="Error"/> findings are broken
/// geometry that analysis cannot fully trust.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>Informational (e.g. unit fallback).</summary>
    Info,

    /// <summary>Suspicious but not fatal (duplicate, very small geometry).</summary>
    Warning,

    /// <summary>Broken / dangerous geometry (zero length, self intersection, branch).</summary>
    Error,
}

/// <summary>
/// The category of a diagnostic finding. Kept as an enum so the UI can group,
/// filter and localize without string matching.
/// </summary>
public enum DiagnosticKind
{
    /// <summary>Small, repairable endpoint gap (<= repair tolerance).</summary>
    SmallGap,

    /// <summary>Open endpoint without a repairable match nearby.</summary>
    OpenEndpoint,

    /// <summary>A junction where three or more edges meet.</summary>
    BranchNode,

    /// <summary>A line / segment with measured length below the zero-length tolerance.</summary>
    ZeroLength,

    /// <summary>A very small (but not zero) geometry, below the small-geometry threshold.</summary>
    VerySmall,

    /// <summary>Two entities describe the same geometry (possibly reversed).</summary>
    Duplicate,

    /// <summary>Two non-adjacent segments of a contour cross each other.</summary>
    SelfIntersection,
}

/// <summary>
/// One diagnostic finding produced by the geometry analyzers. Carries the
/// severity, the category, the localization key and the position(s) so the UI
/// can render markers and locate entities. Immutable.
/// </summary>
public sealed class GeometryDiagnostic
{
    /// <summary>Category of the finding.</summary>
    public DiagnosticKind Kind { get; }

    /// <summary>Severity (Info / Warning / Error).</summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>Localization key for the type name (resolved by the Application layer).</summary>
    public string TypeKey { get; }

    /// <summary>Primary entity involved (0 when not entity-specific).</summary>
    public long EntityIdA { get; }

    /// <summary>Secondary entity (duplicates / self-intersection pairs).</summary>
    public long EntityIdB { get; }

    /// <summary>Primary world position.</summary>
    public Point2 PositionA { get; }

    /// <summary>Secondary world position (gap B end / overlap end).</summary>
    public Point2 PositionB { get; }

    /// <summary>Measured distance in millimeters (gaps, distances).</summary>
    public double Distance { get; }

    /// <summary>Measured length of the offending geometry (zero-length / very small).</summary>
    public double MeasuredLength { get; }

    /// <summary>True when an automatic repair exists for this finding.</summary>
    public bool CanAutoRepair { get; }

    /// <summary>Free-form note (localization key with placeholders already resolved by callers).</summary>
    public string? DetailKey { get; }

    public GeometryDiagnostic(
        DiagnosticKind kind,
        DiagnosticSeverity severity,
        string typeKey,
        long entityIdA = 0,
        long entityIdB = 0,
        Point2 positionA = default,
        Point2 positionB = default,
        double distance = 0,
        double measuredLength = 0,
        bool canAutoRepair = false,
        string? detailKey = null)
    {
        Kind = kind;
        Severity = severity;
        TypeKey = typeKey;
        EntityIdA = entityIdA;
        EntityIdB = entityIdB;
        PositionA = positionA;
        PositionB = positionB;
        Distance = distance;
        MeasuredLength = measuredLength;
        CanAutoRepair = canAutoRepair;
        DetailKey = detailKey;
    }
}
