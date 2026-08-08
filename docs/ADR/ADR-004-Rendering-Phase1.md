# ADR-004: Rendering & selection strategy (Phase 1)

- Status: Accepted
- Date: 2026-08-07
- Status note: subject to revision in Phase 2 when performance work starts.

## Context

Phase 1 needs an interactive 2D viewport: open a DXF, see the drawing, pan
and zoom, click-select an entity. We want the math testable without a WPF
process and rendering simple enough to keep full enumeration correct.

## Decision

- **Pure-math viewport** (`Application/Selection/Viewport`) does world↔screen
  mapping. No WPF types; `ZoomToFit`, `Pan`, `ZoomAt` are unit-tested.
- **`CadViewport`** (WPF) is a `FrameworkElement` that redraws the whole
  document in `OnRender` with `StreamGeometry`. Arcs/circles are flattened to
  short polyline runs for rendering (sagitta < 0.5 px).
- Mouse: left-click picks (id → `SelectionModel`), middle/right-drag pans,
  wheel zooms around the cursor.
- Selection is entity-id based, stored in `Application.Selection`.

## Consequences

- OnRender redraw is simple but not scalable to hundreds of thousands of
  entities; benchmark in Phase 2 and move to cached `DrawingVisual`s /
  display lists with visible-region culling.
- String「zoom to fit」uses `Viewport.ZoomToFit` with the document's
  `OverallBounds`.