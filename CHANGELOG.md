# Changelog

All notable changes to DxfContourStudio are documented here.
Versioning follows [ADR-006](docs/ADR/ADR-006-Versioning.md).

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
