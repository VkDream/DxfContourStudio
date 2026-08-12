# ADR-013: Trim/extend semantics (moving one path end to a boundary crossing)

- Status: Accepted
- Date: 2026-08-08

## Context

The editing toolbar needs "trim to boundary" and "extend to boundary": one
end of a Line / Arc / Polyline moves to the nearest crossing with another
(usually perpendicular) entity. Without a fixed rule, "nearest" is ambiguous
(two crossings), extension direction is ambiguous, and polylines could be
silently rewritten at interior runs.

## Decision

Trim/extend is implemented as `TrimExtendEngine` (pure geometry in
`Core/Geometry/TrimExtend/TrimExtendEngine.cs`) + `TrimExtendCommand`
(`Application/Commands/TrimExtendCommand.cs`).

- **Side semantics**: `TrimSide.KeepStart` keeps the start point and moves
  the end; `TrimSide.KeepEnd` keeps the end and moves the start. The kept
  end never moves (hard contract).
- **Line**: crossings are computed against the infinite line; the crossing
  nearest to the moving end inside the travel reach is used. Inside the
  segment → trimmed, beyond it → extended.
- **Arc**: crossings are computed against the arc's full circle and
  parameterized along the sweep direction. Inside the sweep → trimmed;
  beyond the sweep end → extended (extending that would grow the sweep to
  ≥ 2π, i.e. a full turn, is refused).
- **Polyline**: only the run at the adjusted end may change (LAST run for
  KeepStart, FIRST run for KeepEnd). Crossings on interior runs are not
  handled — such a trim returns no result (refused). All other runs are
  preserved verbatim, the result stays a Polyline with the same id/layer.
- **Boundary treatment**: a Line boundary acts as its infinite line; an Arc
  boundary as its full circle (the boundary's own span is not a gate); any
  crossing is valid regardless of which "side" of the boundary it lies on.
- **Touch case**: when the adjusting end is already on the boundary (within
  tolerance), the result is `Unchanged` — no document mutation.
- **Refusals**: no crossing at all (e.g. parallel lines), boundary of
  unsupported kinds (Circle boundary is supported), arc wrap ≥ 2π, polyline
  interior-run crossings → the command throws `ArgumentException`.
- **Identity**: the primary keeps its id; undo restores the pristine entity
  at its original document index, redo reproduces deterministically.

## Consequences

- Deterministic, hardware-free tests: `TrimExtendCommandTests` (9 tests)
  cover both trim sides, line/arc/polyline extension, arc trim to mid-sweep,
  the unchanged no-op, undo/redo and the parallel-boundary refusal.
- The projection rule ("on the curve within tolerance") shared with the
  break tool keeps the editing suite consistent.
- Interior-run crossings and boundary-circles are explicit future work, not
  silent fallbacks.