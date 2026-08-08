# GEOMETRY_MODEL.md — DxfContourStudio

The Core geometry model — everything the algorithms see. No WPF, no DXF.

## Entities (`Core/Geometry`)

| Type | Fields | Notes |
|---|---|---|
| LineGeometry | P0, P1 | zero-length guarded at import |
| CircleGeometry | Center, Radius | Radius ≥ 0 enforced by ctor |
| ArcGeometry | Center, Radius, StartAngleRadians, SweepRadians, IsCounterClockwise | angles in radians, CCW from +X; sweep in (0, 2π) magnitude, signed by direction |
| PolylineGeometry | Segments (IPathSegment[]), IsClosed | segment-based so bulge arcs survive |

`IGeometryEntity` contract: Id, LayerName, IsVisible, Bounds, Length,
StartPoint, EndPoint, Clone, Transformed, DistanceToPoint, PointAtParameter,
TangentAt.

`IPathSegment`: LineSegment | ArcSegment (polyline runs).

## Canonical conventions

- All coordinates/lengths in **millimeters** (ADR-003).
- Angles in radians; math convention (CCW from +X, Y up).
- Arc sweep is signed: positive = CCW. A full turn is a `CircleGeometry`,
  never a 2π arc.
- Bounds is always finite; `Bounds.Empty` is the accumulation start.

## Degenerate handling

| Case | Behavior |
|---|---|
| Zero-length line | Import drops with warning; analyzer flags Error if it reaches the document |
| Radius < 0 | Constructor throws (programmer error) |
| NaN / Infinity | `GeometrySanity.HasInvalidValues` flags; analyzers report invalid-geometry |
| Very short segment | `SmallGeometryThreshold` Warning (0.01 mm) |
| Duplicate polyline vertex | Segment length ≈ 0 → skipped at import (zero-length tolerance) |

## Serialization

`Application/Projects/ProjectSerializer` round-trips all four entity kinds
losslessly (see PROJECT_FORMAT.md). Arc segments inside polylines survive
with full precision.

## Intersection engine

`Core/Geometry/IntersectionEngine.cs` — parametric line-line intersection
with four result kinds: None / Point / Parallel / CollinearOverlap. This is
the single math source for the self-intersection analyzer.
