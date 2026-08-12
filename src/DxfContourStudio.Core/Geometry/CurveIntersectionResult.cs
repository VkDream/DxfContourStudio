namespace DxfContourStudio.Core.Geometry;

/// <summary>
/// The geometric relationship found between two curves. This is the semantic
/// result model of <see cref="IntersectionEngine"/> 2.0: a bare
/// <c>List&lt;Point2&gt;</c> would lose the "two curves merely touch" vs
/// "they cross" vs "they overlap" distinction that Trim / Join / Break need.
///
/// Semantics per kind:
/// - <see cref="CurveIntersectionKind.None"/> — no contact at all.
/// - <see cref="CurveIntersectionKind.Point"/> — a single well-defined
///   transversal crossing or endpoint touch (usable as a trim boundary).
/// - <see cref="CurveIntersectionKind.TwoPoints"/> — exactly two distinct
///   crossings of a line / arc with a circle or a second arc (usable as trim
///   boundaries; ordering along the first curve is caller-side).
/// - <see cref="CurveIntersectionKind.Tangent"/> — a single point of tangency.
///   It is a real contact but NOT a crossing; trim/break engines must treat it
///   as a boundary with caution (splitting at a tangent yields a zero-length
///   piece and should be refused).
/// - <see cref="CurveIntersectionKind.Parallel"/> — parallel lines, no
///   intersection and no overlap.
/// - <see cref="CurveIntersectionKind.Collinear"/> — collinear lines sharing
///   the same supporting line but with disjoint spans.
/// - <see cref="CurveIntersectionKind.Overlap"/> — collinear lines (or two
///   arcs on the same circle) overlapping over an interval of positive length;
///   <see cref="CurveIntersectionResult.PointA"/> / <see cref="CurveIntersectionResult.PointB"/>
///   span the shared interval.
/// - <see cref="CurveIntersectionKind.Coincident"/> — the two curves cover the
///   same geometric arc region (same circle with fully overlapping sweep, or
///   identical supporting circle).
/// - <see cref="CurveIntersectionKind.Degenerate"/> — one input is degenerate
///   (zero radius / zero length / NaN inputs); no intersection was computed.
///
/// The geometric intersection and the "usable for Trim" intersection are NOT
/// the same concept: tangent contacts, overlaps and collinear runs must be
/// filtered by the editing layer, not silently turned into cuts.
/// </summary>
public enum CurveIntersectionKind
{
    None,
    Point,
    TwoPoints,
    Tangent,
    Parallel,
    Collinear,
    Overlap,
    Coincident,
    Degenerate,
}

/// <summary>
/// Result of intersecting two curves.
/// </summary>
/// <param name="Kind">The semantic relationship (see <see cref="CurveIntersectionKind"/>).</param>
/// <param name="Point1">First point: Point/Tangent contact, or start of the overlap interval.</param>
/// <param name="Point2">Second point for TwoPoints, or end of the overlap interval.</param>
public readonly record struct CurveIntersectionResult(
    CurveIntersectionKind Kind,
    Point2 Point1,
    Point2 Point2)
{
    /// <summary>The (up to two) contact points for kinds that carry points, in stable order.</summary>
    public IReadOnlyList<Point2> Points =>
        Kind is CurveIntersectionKind.Point or CurveIntersectionKind.Tangent
            ? [Point1]
            : Kind is CurveIntersectionKind.TwoPoints or CurveIntersectionKind.Overlap
                ? [Point1, Point2]
                : [];

    public static CurveIntersectionResult None =>
        new(CurveIntersectionKind.None, Point2.Origin, Point2.Origin);

    public static CurveIntersectionResult At(Point2 p) =>
        new(CurveIntersectionKind.Point, p, p);

    public static CurveIntersectionResult TangentAt(Point2 p) =>
        new(CurveIntersectionKind.Tangent, p, p);

    public static CurveIntersectionResult Two(Point2 a, Point2 b) =>
        new(CurveIntersectionKind.TwoPoints, a, b);

    public static CurveIntersectionResult Parallel() =>
        new(CurveIntersectionKind.Parallel, Point2.Origin, Point2.Origin);

    public static CurveIntersectionResult Collinear() =>
        new(CurveIntersectionKind.Collinear, Point2.Origin, Point2.Origin);

    public static CurveIntersectionResult Overlap(Point2 a, Point2 b) =>
        new(CurveIntersectionKind.Overlap, a, b);

    public static CurveIntersectionResult Coincident() =>
        new(CurveIntersectionKind.Coincident, Point2.Origin, Point2.Origin);

    public static CurveIntersectionResult Degenerate() =>
        new(CurveIntersectionKind.Degenerate, Point2.Origin, Point2.Origin);
}