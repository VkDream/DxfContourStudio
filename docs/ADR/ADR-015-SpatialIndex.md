# ADR-015: Spatial index (uniform grid for picking)

- Status: Accepted
- Date: 2026-08-08

## Context

`CadDocument.Pick` scanned the entire entity list for every mouse pick —
O(n) per query, which becomes the dominant cost for large drawings
(campaign D11 targets 100k entities). The renderer already culls by bounds
(Rendering 2.0); picking needed the same treatment.

## Decision

- **Index**: a uniform grid (`SpatialIndex`, pure in
  `Core/Geometry/Spatial/SpatialIndex.cs`). Each entity is registered in
  every cell its world bounds overlap (cell size 8.0 world units by
  default, configurable at construction). Queries look up only the cells
  under the query square, deduplicate per entity, and then apply the exact
  `DistanceToPoint` check — results are identical to the old linear scan.
- **Document integration**: `CadDocument` keeps a lazily built index. A
  revision counter (`_revision`) increments on every entity mutation
  (`MutateAndNotify`); `Pick` rebuilds the index only when the revision
  changed. Layer-visibility filtering is preserved exactly (the old
  `IsVisibleForInteraction` gate applies to query results).
- **Determinism**: the index never owns entities and never reorders results;
  candidates are returned in insertion order per entity (deduplication via
  identity set), the same set as the pre-index scan.

## Consequences

- Picking is independent of total entity count once the index is built
  (~O(cells in the query square)); rebuild happens only on edits.
- Tests: `SpatialIndexTests` (6 tests) — nearby-only query, exact-distance
  filtering, zero radius, document-level `Pick` semantics, mutation
  invalidation (remove then add then pick), and hidden-layer visibility.
- The index is reusable by future nearest-snapping extensions beyond pick.