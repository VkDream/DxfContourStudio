# TOLERANCE.md — DxfContourStudio

All numeric tolerances live in one place: `Core/Geometry/GeometryTolerance.cs`.
Analyzers, the topology builder and the repair engine read from here — magic
numbers in algorithms are forbidden.

| Property | Default | Meaning |
|---|---|---|
| PointEqualityTolerance | 1e-6 mm | two points are the same point |
| EndpointSnapTolerance | 0.05 mm | max gap that is repairable ("small gap") |
| ZeroLengthTolerance | 1e-6 mm | below this a segment is zero-length |
| SmallGeometryThreshold | 0.01 mm | below this a geometry is "very small" (Warning) |
| ClosureTolerance | 0.05 mm | open-chain ends within this still close |
| AngleTolerance | 1e-3 rad | directions equal / 180° joint |
| CollinearTolerance | 1e-3 rad | point collinear with two directions |
| MinimumAreaTolerance | 1e-6 mm² | below this a closed contour is degenerate |
| DuplicateTolerance | 1e-6 mm | entities describe the same geometry |
| SelfIntersectionTolerance | 1e-6 mm | near-touching segments count as crossing |

`GeometryTolerance` is a plain mutable object with a static `Default`
instance; the project format persists a copy (PROJECT_FORMAT.md) so tuning
survives save→load.

## Why one place

- Gap semantics depend on the repair tolerance: 0.03 mm is a SmallGap,
  0.8 mm is an OpenContourEnd. The boundary must be one value, not two
  constants that drift apart.
- The project file stores the exact policy the drawing was analyzed with.
