# ROADMAP.md — DxfContourStudio

Current version: **0.2.0** — first public functional release (2026-08-08).

## Done (v0.1.0 → v0.2.0)

- Phase 0: skeleton, CI, license, ADR-001..004.
- Phase 1: open DXF, zoom/pan, selection, layers, properties, undo/redo,
  status bar, shortcuts, zh-CN/en-US localization.
- Batch B: topology → contour → nesting → gap analysis/repair, diagnostics
  panel, about dialog.
- GUI runtime recovery: toolbar text leak, viewport auto-fit, Analyze crash
  (FormatException), open-StreamGeometry stroke bug (isStroked:false) with a
  permanent offscreen-render guard.
- Regression corpus: 19 hand-authored DXF fixtures + golden tests.
- Diagnostics hardening: zero length, very small, duplicates (incl. reversed),
  self intersection (line-line), severity model, branch degree.
- Intersection engine (line-line; line-arc/arc-arc NOT_SUPPORTED_YET).
- Repair: midpoint strategy (ADR-007), batch repair with composite undo.
- Project format `.dxfstudio` (ADR-009): save/load, dirty state, unsaved
  guard, round-trip tests.
- Clean DXF export (ADR-008): LINE/ARC/CIRCLE/LWPOLYLINE(+bulge), units,
  version, overwrite protection, export report, round-trip tests.
- Performance: three O(n²) hot paths → near-linear (50k topology 118 s → <2 s).
- Error handling policy (ADR-010) + docs.
- Gap semantics correction: open-chain gap repair vs contour-closure repair
  are distinct scenarios with dedicated fixtures (small_gap_003 /
  rectangle_gap_003).
- Interactive editing tools (D13A–D17): Select / Node Edit / Join / Break /
  Trim / Extend as tools (EditToolSession, ADR-019) with toolbar + menu
  activation, hover previews, escape chains, single-undo-per-edit; stale
  analysis semantics with banner (ADR-020).

## Next candidates (not started — await user)

- Self intersection for line-arc / arc-arc.
- Repair preview (Core can compute original/new endpoints + delta).
- 9 additional unit/unitless/degenerate fixtures.
- Offset / Fillet / Kerf — out of scope per user (Trim/Extend are done).
- CAM / GCode / machine integration — out of scope.

## Explicitly excluded

Trim, Extend, Offset, Fillet, Kerf, Lead-in/out, Cut order, GCode, CAM,
motion cards, PLC, laser cards, cameras, MES, AI, network accounts,
databases, DWG.
