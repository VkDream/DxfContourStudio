# ADR-016: Round-trip fidelity for edited geometry

- Status: Accepted
- Date: 2026-08-08

## Context

The D4–D9 editing commands produce polyline entities with mixed Line/Arc
runs, re-spliced (possibly reversed) arcs, split polylines and moved shared
vertices. The `.dxfstudio` project format must persist these results
byte-exact: save → load must reproduce the same in-memory geometry.

## Decision

- The existing `ProjectSerializer` coverage is complete for all edited
  outputs: Line, Circle, Arc (center/radius/start/sweep, orientation via
  sweep sign) and Polyline (per-run kind, start/end, arc center/radius/
  start/sweep, closed flag). Entity ids, order and visibility are preserved.
- New tests in `EditedGeometryRoundTripTests` (4 tests, D10) exercise
  round-trip fidelity for the *command products* specifically:
  - joined mixed polyline (line + reversed arc + line),
  - broken polyline (both halves, arc-run kinds and sweep preserved),
  - CW arc trimmed against a boundary (sign/start preserved exactly),
  - closed polyline with a moved vertex (closed flag + shared vertex).

## Consequences

- Editing output is durable in the native format; no serialization gap
  exists between in-memory geometry and the project file.
- Sweep-sign carries orientation across the wire; deserialization
  reconstructs Arc/ArcSegment with `SweepRadians >= 0` ⇒
  `IsCounterClockwise`, matching the C# angle convention (angle increases
  CCW, negative sweep = clockwise).