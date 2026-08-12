# Changelog

All notable changes to DxfContourStudio are documented here.
Versioning follows [ADR-006](docs/ADR/ADR-006-Versioning.md).

## [Unreleased]

### Added (0.3.0-dev)

- Curve intersection engine 2.0 (line/arc/circle, runs, boundary point
  collection) and self-intersection analysis across all runs (D1/D2).
- Snapping engine: endpoint / intersection / center / midpoint / nearest
  priorities; circle-aware (D3).
- Editing commands and engines: Join (endpoint-adjacent, mixed polylines,
  reversible), Break at parameter (lines/arcs/polylines), Trim/Extend
  against line/circle boundaries, node dragging with arc re-splicing and
  shared-vertex consistency (D4–D7).
- Chain join as one undoable transaction (Edit → Join selected, Ctrl+J);
  Break in half (Ctrl+B); Trim start/end to boundary (Ctrl+T /
  Ctrl+Shift+T); menu + keyboard bindings (D12).
- Rendering 2.0: world-space culling with per-entity bounds cache and
  margin (D8).
- Spatial index (uniform grid) behind document picking; revision-based lazy
  rebuild; verified at 100k entities (D9, D11).
- Round-trip fidelity tests for edited geometry — mixed polyline arc runs,
  sweep sign, closed flags, node moves (D10).
- Interactive editing tools (D13A–D17): EditToolSession mode state machine
  behind Select / Node Edit / Join / Break / Trim / Extend tools; toolbar +
  Edit menu activation, hover previews, escape-to-cancel chains, one undo
  step per finished edit (ADR-019).
- Stale-analysis semantics: any edit/undo marks the analysis stale with a
  localized banner and clears the old diagnostic/contour listings; Analyze
  and gap-repair refresh it (ADR-020).

## [0.2.0] - 2026-08-08

First public functional release.

### Added

- DXF import via ACadSharp (MIT): LINE, ARC, CIRCLE, LWPOLYLINE (with
  bulges), POLYLINE/Vertex2D; unsupported kinds counted and reported.
- CAD viewport: pan/zoom, click-pick, selection highlight, layer visibility,
  drag-move with undo.
- Topology-based contour analysis: chains, open/closed classification,
  outer/hole/island nesting with depth, per-contour length/bounds/area/
  orientation.
- Geometry diagnostics: zero-length, very small, duplicates (incl. reversed
  lines), line-line self intersections, branch nodes, severity model
  (Info/Warning/Error).
- Gap diagnostics and safe repair: single-gap (midpoint strategy) and batch
  "repair all safe gaps" with composite undo.
- Project save/load (`.dxfstudio` JSON, schema v1), dirty-state tracking,
  unsaved-changes guard.
- Clean DXF export (LINE/ARC/CIRCLE/LWPOLYLINE + bulge) with configurable
  units and DXF version; refuses to overwrite the source file by default.
- Export/re-import round-trip regression coverage.
- zh-CN / en-US localization, persisted.
- WPF offscreen renderer regression tests (STA RenderTargetBitmap).
- Hand-authored regression corpus (19 files) with golden tests.

### Changed

- Version promoted from 0.1.0 to 0.2.0 (single source of truth:
  `Directory.Build.props`).
- Performance: three O(n²) hot paths made near-linear (topology adjacency,
  gap pairing, duplicate detection) — 50k-entity analysis drops from ~118 s
  to < 2 s.
- Error handling policy (ADR-010): recoverable UI exceptions are handled,
  unknown programmer errors are not silently swallowed.

### Fixed

- Renderer: open figures drawn through `StreamGeometryContext.LineTo(..., isStroked: false, ...)`
  carried no visible stroke; open Line / Polyline / flattened Arc now draw
  via `DrawLine` (Circle keeps `EllipseGeometry`).
- DXF ARC import: ACadSharp reports angles in radians; the mapper now uses
  them directly (a 180° arc was previously imported as a ~3.14° arc).
- Analyze crash: `Status.Analyzed` format string had three placeholders but
  received two arguments (FormatException on the UI thread).
- Open-endpoint diagnostics no longer leak a `double.MaxValue` distance into
  the UI; a "no matching endpoint" state is displayed instead.
- Viewport auto-fit after opening a file; zero-size fits are avoided.

### Known Limitations

- Self-intersection detection is line-line only; line-arc / arc-arc are not
  supported yet.
- R12 export is offered but not yet manually exercised end-to-end.
- 100k+ entity rendering is still a full redraw per frame.
- Not a complete CAD/CAM system: no Trim/Extend/Offset/Fillet, no
  machine/laser/PLC/GCode integration.
