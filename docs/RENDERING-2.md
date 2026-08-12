# Rendering 2.0 — viewport culling & bounds cache

- Status: Accepted
- Date: 2026-08-08

## Context

The viewport drew every entity on every frame: each `OnRender` looped over
all document entities, projected them and issued `DrawLine`/`DrawEllipse`
calls even when the geometry sat far outside the visible world rectangle.
For 100k-entity scenes (campaign D11) that is pure waste: most of the frame
budget goes into geometry that the user cannot see.

## Decision

Add two cooperating mechanisms to `CadViewport`:

1. **World-space culling** — `RenderCulling` (pure, framework-free math in
   `Application/Selection/RenderCulling.cs`):
   - `WorldView(viewport, wPx, hPx)` computes the world rectangle currently
     visible (center ± half of the world-space viewport size).
   - `IsVisible(entityBounds, viewWorld, marginWorld)` tests AABB overlap
     against the padded view rectangle. The margin (default 8 screen px
     converted to world units) keeps entities whose strokes/pens poke across
     the edge from being wrongly dropped.
   - Kept framework-free so it is unit-testable headlessly.
2. **Per-entity bounds cache** — `CadViewport` caches `entity.Bounds` in a
   dictionary keyed by id; the cache is invalidated on `CadDocument.DataChanged`
   (the only event that can change geometry). Drag-preview entities are never
   culled (they can move anywhere mid-gesture).

## Consequence

- Off-screen geometry is skipped before any drawing call; pan/zoom scenes
  with far-apart clusters render only the visible region.
- Tests: `RenderCullingTests` (6 tests) — headless culling math (visible /
  far / margin / zoom-out / empty bounds) plus an STA off-screen proof that
  two far-apart clusters each render ink when focused and nothing crashes.
- The 100k entity stress (D11) builds directly on this: culling bounds the
  per-frame work by what is actually on screen.

## Out of scope (later)

- Spatial-indexed picking (D9) uses the same Bounds math.
- Adaptive level-of-detail for dense clusters.