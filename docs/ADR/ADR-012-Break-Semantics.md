# ADR-012: Break semantics (splitting a path entity at a curve point)

- Status: Accepted
- Date: 2026-08-08

## Context

The editing toolbar needs "break/trim at point": splitting a Line, Arc or
Polyline into two pieces at a location on the curve (picked in the viewport).
Without strict rules the tool could cut at the path endpoints, silently snap
to points off the curve, or scramble run types when cutting mixed polylines.

## Decision

Break is implemented as `PathBreaker` (pure geometry in
`Core/Geometry/Breaking/PathBreaker.cs`) + `BreakEntityCommand`
(`Application/Commands/BreakEntityCommand.cs`).

- **On-curve requirement**: the cut point must lie on the curve within
  `tolerance`; the projection is parameterized along the path (length-based
  for polylines, angular for arcs) and a point farther away than tolerance is
  refused (`ArgumentException` at command construction).
- **Endpoint refusal**: parameter 0 and 1 (the path endpoints) never split —
  cutting there is a no-op visually and is refused.
- **Kind preservation**: a Line splits into two Lines; an Arc into two Arcs
  (the cut angle = start + t·sweep, both halves keep center/radius and the
  CCW flag); a Polyline splits into two open Polylines, the run containing
  the cut parameter is split into two same-type runs (LineSegment → two
  LineSegments, ArcSegment → two ArcSegments), all other runs are preserved
  verbatim.
- **Closed polyline**: splitting a closed polyline yields two *open*
  polylines whose ends meet at the cut.
- **Identity (ADR-013 policy)**: the left piece keeps the original entity id;
  the right piece gets a fresh id (`max(ids) + 1`). Both pieces keep the
  layer and visibility of the source.
- **Command contract**: fully undoable — undo removes both pieces and
  re-inserts the pristine original at its original document index
  (order preserved), redo rebuilds deterministically.

## Consequences

- Deterministic, hardware-free tests: `BreakTests` (6 tests) cover the line
  midpoint split, the arc half-sweep split, the mixed two-run polyline split,
  undo/redo round-trip and both rejection paths.
- The same projection (TryProjectParameter) is reused by the future trim /
  extend tools so the "on the curve" rule stays consistent across the
  editing suite.