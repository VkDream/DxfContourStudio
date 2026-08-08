# TOPOLOGY.md — DxfContourStudio

How raw geometry becomes contours and diagnostics.

## Pipeline (`ContourAnalyzer.Analyze`)

```
entities
  → TopologyBuilder        (endpoints → nodes/edges via spatial-hash matcher)
  → ContourChainBuilder    (walk edges into open/closed chains + circles)
  → ContourAssembler       (length, bounds, signed area, orientation)
  → GapDiagnosticsBuilder  (small gaps / open ends / branch nodes)
  → NestingAnalyzer        (Outer / Hole / Island + depth + parent)
  → GeometryDiagnosticAnalyzer (zero length / very small / duplicate /
                                self intersection) + contour validity tags
```

## Topology graph (`Core/Topology`)

- `TopologyNode`: position + degree; `IsDangling` (degree 1), `IsBranch`
  (> 2).
- `TopologyEdge`: one LINE / ARC / polyline run mapped onto two nodes.
  Circles never become edges — they are intrinsically closed contours.
- `EndpointMatcher`: collapses coincident endpoints into shared nodes with a
  spatial hash over cells of size = point-equality tolerance (near-linear).
  Small gaps (≤ snap tolerance) are NOT merged — they stay separate nodes so
  the gap diagnostics can repair them.

## Chains

- Closed chain = walk returns to its start node.
- A closed LWPOLYLINE whose last vertex does not coincide with the first gets
  an implicit closing edge (SegmentIndex = -1) instead of a false open end.
- A circle becomes a closed contour directly.

## Contour validity (v0.2-candidate)

Each contour carries `ContourValidity` (Valid / Open / SelfIntersecting /
Branched / Degenerate / GapRepairable) plus a `DiagnosticKinds` list so
multiple problems on one contour are not collapsed into a single enum value.

## Nesting

`NestingAnalyzer` assigns depth: 0 = outer, 1 = hole, 2 = island, 3 = hole, …
with `ParentContourId` pointing at the smallest containing contour. Contour
ids are assigned **before** nesting so containment keys never collide.

## Self intersection (phase 1)

`SelfIntersectionAnalyzer` tests non-adjacent straight runs of closed
contours with `IntersectionEngine`. Adjacent segments (including the closing
segment next to the first) are excluded. Line-arc / arc-arc are
**NOT_SUPPORTED_YET** and silently produce no finding rather than a wrong one.

## Performance

All passes are near-linear by design:
- Endpoint matching: spatial hash (3×3 cell neighbourhood).
- Node→edge adjacency: single-pass lists (no per-node scan).
- Gap pairing: spatial hash over dangling ends.
- Duplicate detection: spatial hash over both endpoints of each entity.

Sanity budgets in `PerformanceSanityTests` cover 1k / 10k / 50k entities
(build, bounds, topology, render prep, hit-test samples).
