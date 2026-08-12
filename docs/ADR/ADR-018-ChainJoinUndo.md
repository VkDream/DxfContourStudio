# ADR-018: Chain join as a single undoable transaction (UI binding)

- Status: Accepted
- Date: 2026-08-08

## Context

D4's `JoinEntitiesCommand` joins exactly two entities. The Edit menu
targets a whole selection (Ctrl+J → join all selected, in document order);
running N−1 single commands would require N−1 undos and could leave the
document half-merged if a later step failed.

## Decision

- New `JoinManyCommand` (Application layer, D12):
  - Constructor **simulates the whole chain eagerly** with `JoinEngine`
    and fails fast (ArgumentException) when any step is not joinable —
    same fail-fast contract as `JoinEntitiesCommand`, so the UI can bind
    CanExecute and never execute a partial merge.
  - `Execute` runs every step in order (previous step's polyline becomes
    the next step's primary); `Undo` reverses the chain — one Ctrl+Z
    restores all originals with ids, order and layers intact.
  - Indices are refreshed at Execute time (chains shrink the entity list,
    construction-time indices would be stale).
- UI (D12): Edit menu gains 合并/打断/修剪 items wired to VM commands
  `JoinSelected` (Ctrl+J), `BreakSelected` (Ctrl+B, midpoint of the single
  selected entity), `TrimSelectedStart`/`TrimSelectedEnd` (Ctrl+T /
  Ctrl+Shift+T, first selected trims against second). All status strings
  are localized (zh/en).

## Consequences

- The command surface is testable at Application level
  (`JoinManyCommandTests`, 4 tests: chain collapse, single-undo restore +
  redo, non-adjacent rejection, single-id rejection); the WPF layer is a
  thin binding with no extra logic.