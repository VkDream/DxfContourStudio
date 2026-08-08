# ADR-009: Project file format (.dxfstudio)

- Status: Accepted
- Date: 2026-08-07

## Context

The workbench edits drawings (repair gaps, delete entities, future edits)
that a plain DXF file cannot represent without losing repair state. A native
project format is needed. `.dcsproj` would collide with C# project files, so
the extension is `.dxfstudio`.

## Decision

- **Format**: JSON, extension `.dxfstudio`, schema version 1.
- **Content**: `schemaVersion`, `applicationVersion`, `source` (file info),
  `units`, `tolerance` (full `GeometryTolerance`), `diagnostics` thresholds,
  `layers` (incl. view visibility), `entities` (Line/Arc/Circle/Polyline with
  arc segments, lossless).
- **Not persisted**: analysis results, selection, viewport camera, history —
  all derived state rebuilt on load.
- **Serialization**: `ProjectSerializer` in Application/Projects; geometry
  projections store full-precision doubles (invariant culture) so arcs and
  bulge segments survive a save→load cycle exactly.
- **Versioning**: loaders reject `schemaVersion` newer than the writer's;
  unknown fields are tolerated.
- **Dirty state**: `CadDocument.IsDirty` + `DataChanged` event. Any geometry
  mutation dirties the document. **Layer visibility is a view preference and
  does NOT dirty the document** — the project stores it, but toggling a layer
  checkbox alone must not trigger the unsaved-changes prompt.
- **Unsaved guard**: `IUnsavedChangesPrompt` abstraction; WPF implements it
  with a Save/Discard/Cancel message box; tests use a stub. Open DXF, Open
  Project and Exit consult the guard.

## Consequences

- Save→load round-trips the geometry (tested: basic-scene, outer_hole,
  repaired small_gap, tolerance settings).
- A repaired document stays repaired after save→load.
- The format is human-readable and diff-friendly for future tooling.
