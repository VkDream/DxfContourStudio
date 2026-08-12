# ADR-014: Node editing semantics (vertex / endpoint manipulation)

- Status: Accepted
- Date: 2026-08-08

## Context

The editing toolbar needs a grip/node edit tool: the user drags an endpoint
or a polyline vertex to a new position. The hard part is defining what the
entities do when a shared vertex moves — especially arcs, whose endpoints
normally sit on a fixed circle.

## Decision

Node editing is implemented as `NodeEditEngine` (pure geometry in
`Core/Geometry/NodeEdit/NodeEditEngine.cs`) + `MoveNodeCommand`
(`Application/Commands/MoveNodeCommand.cs`).

- **Node inventory**: a Line exposes 2 nodes (start, end); an Arc 2 nodes
  (its two path endpoints); an open Polyline one node per vertex (runs + 1);
  a closed Polyline one node per vertex (runs; the shared end vertex exists
  once); Circles expose no nodes.
- **Line**: a node move re-pivots the corresponding endpoint; both other
  geometry is unchanged.
- **Arc**: nodes keep the arc's circle. The moved endpoint re-splices its
  angle along the circle (same center, same radius, CCW/CW sign preserved)
  and the sweep is recomputed as the angular distance travel direction.
  A sweep collapsing to 0 (target opposite the fixed end) or reaching ≥ 2π
  is refused.
- **Polyline**: an interior vertex moves by re-pivoting the two adjacent
  runs onto the target (line runs re-point, arc runs re-splice like single
  arcs); a first/last node moves the first/last run's free end; a closed
  polyline keeps its closed flag and re-pivots the two runs meeting at the
  moved vertex.
- **Refusals** (command throws): node index out of range, degenerate arc
  sweep after re-splicing, fixed nodes on circles, unsupported kinds.
- **Identity**: the entity keeps its id; the command is a single undoable
  unit (undo restores the pristine entity at the original document index).

## Consequences

- Deterministic, hardware-free tests: `MoveNodeCommand` covered by 8 tests in
  `NodeEditTests` (line endpoint, arc re-splice, polyline interior vertex,
  open-poly end node, closed-poly shared vertex, undo/redo, out-of-range
  refusal, node inventory counts).
- The same node list feeds the future viewport handle rendering
  (`NodePositions`), so the UI and the command share one source of truth.