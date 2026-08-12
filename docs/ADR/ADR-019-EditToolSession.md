# ADR-019: Interactive editing tool session (EditToolSession)

- Status: Accepted
- Date: 2026-08-11

## Context

D4–D7 delivered editing *engines and commands* (Join, Break, Trim, Extend,
NodeEdit) and D12 bound them to selection-based menu commands. That surface
requires the user to first select the exact entities, then invoke a command;
it cannot express two-step interactions such as "pick the first entity, then
pick the boundary", and the old node editing had no dedicated mode at all.

## Decision

- New `EditToolSession` (Application layer, `Interaction/`): a mode state
  machine shared by the five tools — Select / NodeEdit / Join / Break /
  Trim / Extend. It is *not* a command: it stages interactions and forwards
  each finished gesture to the existing commands (`JoinEntitiesCommand`,
  `BreakEntityCommand`, `TrimSectionCommand`, `ExtendEntityCommand`,
  `NodeEditCommand`) via `CommandRequested` events.
- Tools are activated through `MainViewModel.ActivateToolCommand(string)`
  (string parameter, parsed with `Enum.TryParse`, because XAML
  `CommandParameter` cannot bind an enum); `Select` deactivates the tool.
- While a tool other than Select is active: selection is cleared, the
  viewport consumes all left clicks (no pick/drag), hover geometry is
  reported through `HoverRequested` and rendered as a dashed overlay, and
  `Escape` cancels the current step first, then the tool itself
  (`CancelToolCommand`).
- A single undo step per finished edit: `CommandRequested` → `History.Execute`
  → `NotifyCommandCompleted` — identical to the old menu commands.
- Status/guidance text is localized via the `EditTools.*` key family; tool
  names and tooltips via `Tool.Name.*` / `Tooltip.*`.

## Consequences

- The full interaction surface is testable at Application level
  (`EditToolSessionTests` ~28 tests, `MainViewModelToolTests` 9 tests,
  `TrimSectionCommandTests` 6 tests); WPF stays a thin binding.
- The old selection-based commands (`JoinSelected`, `BreakSelected`,
  `TrimSelectedStart/End`, `ExtendSelected`) remain for the snap-flow tests
  but are no longer exposed in the UI.
- Editing invalidates analysis output (see ADR-020).
