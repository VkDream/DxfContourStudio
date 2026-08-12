# ADR-017: Scale targets — picking stays interactive at 100k entities

- Status: Accepted
- Date: 2026-08-08

## Context

Campaign target D11: the document + pick pipeline must stay correct and
interactive on drawings of ~100k entities, the scale at which the previous
O(n)-scan pick would introduce multi-second stalls per mouse click.

## Decision

- Verified with dedicated stress suites (no timing asserts; correctness is
  pinned against a direct distance-scan oracle, and regressions to O(n)
  would surface as CI hangs by orders of magnitude):
  - `SpatialIndexStressTests` (Core, 4 tests): 100k-entity grid; single
    exact candidate, boundary/miss windows, index results ≡ linear-scan
    oracle on scattered sample windows, verdict stability.
  - `DocumentPickStressTests` (Application, 4 tests): `CadDocument.Pick`
    at 100k — nearest hit, empty zones, layer-visibility honored, index
    invalidation after remove + re-add.
- Measured wall time for the full suite (build + index + queries at 100k)
  is well under a second per test on the build machine; no timing assertions
  are baked in.

## Consequences

- `Pick` stays O(cells + candidates) instead of O(n) per click; rebuild-on-
  mutation only on edit commands, not on mouse moves.
- The uniform grid (ADR-015) is now backed by stress coverage at the
  campaign D11 target count — the drawing canvas remains reactive at scale
  once rendering culling (ADR/D8) and spatial picking (this ADR) are both
  in effect.