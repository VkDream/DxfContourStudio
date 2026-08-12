# ADR-011: Join semantics (entity merging into one mixed polyline)

- Status: Accepted
- Date: 2026-08-08

## Context

The editing toolbar needs "join/merge selected entities": turning two
endpoint-adjacent path entities (Line, Arc, Polyline with Line/Arc runs) into
one continuous mixed path. Without strict rules a join can silently merge
entities that merely overlap at a large distance, guess the connection on
ambiguous geometry or destroy the layer model.

## Decision

Join is implemented as `JoinEngine` (pure geometry in
`Core/Geometry/Joining/JoinEngine.cs`) + `JoinEntitiesCommand`
(`Application/Commands/JoinEntitiesCommand.cs`).

- **Adjacency**: only endpoint pairs within `GeometryTolerance.JoinTolerance`
  (0.05 mm) join. Endpoints farther apart are refused
  (`NotConnected`).
- **Uniqueness**: exactly **one** matching endpoint pair is required. Two or
  more matches (e.g. two identical/fully overlapping lines) are refused
  (`Ambiguous`) — the tool never guesses.
- **Orientations**: all four combinations resolve deterministically
  (`a.End`↔`b.Start` straight, `a.End`↔`b.End` reverses B, `a.Start`↔`b.Start`
  reverses A, `a.Start`↔`b.End` reverses both). Reversing an arc flips the
  sweep sign and the CCW flag so the geometric span stays canonical.
- **Layers**: both entities must be on the same layer (`DifferentLayers`
  otherwise). Layer merging is never implicit.
- **Result shape**: the merged entity is always a `PolylineGeometry` whose
  run list is exactly the concatenation of the (possibly reversed) source run
  lists. Bulges survive; the two contributions are never woven together.
- **Identity (ADR-013 policy)**: the primary entity's id, layer and visibility
  survive; the secondary entity is removed. Undo restores both original
  entities with ids, document order and geometry exactly as before.
- **Command contract**: the command validates the join in its constructor
  (invalid joins throw `ArgumentException`) and is fully undoable through
  `CommandHistory` (Execute → Undo → Redo round-trip tested).

## Consequences

- Deterministic, hardware-free tests: `JoinTests` (8 tests) cover golden
  cases D9 (Line-Arc-Line chain → one 3-run polyline) and D10
  (undo/redo restores exact geometry), reversed-arc merging, polyline+line,
  and the three rejection reasons.
- A pair that only *touches* but is not endpoint-adjacent within tolerance is
  not joined — the user first applies gap repair or moves the entity.
- Future multi-selection join can loop this command; branching joins remain a
  user decision (no auto-resolution).