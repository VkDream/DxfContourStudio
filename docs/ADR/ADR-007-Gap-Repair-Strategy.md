# ADR-007: Gap repair strategy (auto-repairable small gaps)

- Status: Accepted
- Date: 2026-08-07

## Context

The analysis pipeline reports three gap diagnostics: `SmallGap` (two open
ends closer than the repair tolerance, typically an unintentional 0.01–0.05
mm joint), `OpenContourEnd` (chain end without a partner) and `BranchNode`
(three or more ends at one point). The UI needs a one-click repair for the
common drafting error while refusing to guess on ambiguous geometry.

## Decision

- **Repair semantics**: both ends of a `SmallGap` are moved to the midpoint
  of their endpoints (ADR contract: for a 0.020 mm gap the two moved ends
  land at 100.01, i.e. `(100.0 + 100.02) / 2`).
- **Auto-repairability**: only `SmallGap` with `CanAutoRepair == true` is
  accepted by `RepairGapCommand`. `OpenContourEnd` and `BranchNode` are never
  auto-repaired; constructing a `RepairGapCommand` with them throws
  `ArgumentException`.
- **Same-entity case**: an open polyline whose two chain ends are the gap
  closes onto itself (both ends move, gap becomes 0).
- **Undo/redo**: the repair executes as exactly one undoable command through
  `CommandHistory`.
- **Closed polyline semantics**: a polyline flagged closed is not repairable;
  if the closing vertex is missing, the topology builder synthesizes an
  implicit closing edge (`SegmentIndex = -1`) instead of reporting a false
  open end.
- **Contour identity**: contour ids are assigned before nesting analysis so
  nesting keyed lookups never collide (all-zero ids caused dangling
  `ParentContourId` and wrong nesting depth).

## Consequences

- The repair is deterministic and testable without hardware:
  `RepairGapCommandTests` (4 tests) and the acceptance tests over the
  checked-in `small_gap_003.dxf` (0.030 mm gap) cover repair, undo/redo,
  same-entity closing and the non-repairable throw.
- The UI repair button stays disabled unless a repairable diagnostics row is
  selected (`CanRepairSelectedGap`).
- Future gap strategies (trim/extend, fillet) are explicitly out of scope for
  v0.1.0.