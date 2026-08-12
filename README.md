# DxfContourStudio

Industrial DXF contour analysis, diagnostics and repair workbench built with
C# / .NET 10 / WPF.

Open a DXF, inspect and analyze contours, repair small gaps, save projects,
and export cleaned DXF files — all in a bilingual (zh-CN / en-US) desktop UI.

[中文 README](README.zh-CN.md)

> **Status:** v0.2.0 — first public functional release.

## Overview

DxfContourStudio turns raw DXF geometry into a topology graph, recognizes
open and closed contours, classifies nesting (outer / hole / island), finds
geometry defects (gaps, open ends, branches, duplicates, zero-length runs,
self intersections) and lets you repair the safe ones with full undo support.
It is a pre-production geometry workbench — **not** a full CAD/CAM system.

## Features

- **DXF import** (ACadSharp 3.6.51, MIT): LINE, ARC, CIRCLE, LWPOLYLINE
  (with bulges), POLYLINE/Vertex2D; ellipses/splines/INSERT/text are counted
  and reported, never crash. Coordinates canonicalized to millimeters.
- **CAD viewport**: pan / zoom, click-pick, selection highlight, layer
  visibility, drag-move with undo.
- **Topology & contours**: topology graph → chains → contours (length, bounds,
  signed area, orientation) → nesting (Outer / Hole / Island + depth).
- **Diagnostics**: small repairable gaps, open endpoints, branch nodes,
  zero-length geometry, very small geometry, duplicates (incl. reversed),
  line-line self intersections; severity model (Info / Warning / Error).
- **Repair**: single-gap repair (midpoint strategy) and batch "repair all
  safe gaps" — one composite undo restores the whole batch.
- **Project format** `.dxfstudio` (JSON): lossless save/load of geometry,
  layers, units and tolerance settings; dirty-state tracking and an
  unsaved-changes guard.
- **Clean DXF export**: writes LINE/ARC/CIRCLE/LWPOLYLINE (+ bulge) with
  configurable units and version; refuses to overwrite the source file by
  default; export report in the status bar.
- **Export / re-import round-trip** regression coverage: a repaired file
  stays repaired after export and re-import.
- **Localization**: zh-CN and en-US, persisted.
- **Performance**: near-linear topology and diagnostics (50k entities analyze
  in seconds).

## Screenshots

Screenshots will be added after the first public release.

## Supported DXF Entities

| DXF entity | Support |
|---|---|
| LINE | full |
| ARC | full |
| CIRCLE | full |
| LWPOLYLINE (with bulges) | full |
| POLYLINE / Vertex2D | full |
| ELLIPSE, SPLINE, INSERT, TEXT/MTEXT | counted + reported, geometry skipped |

## Contour Analysis

- Topology-based chain building (shared endpoints collapse into nodes).
- Closed contour detection incl. implicit closing edges for closed polylines.
- Open / closed classification; outer / hole / island nesting with depth.
- Per-contour length, bounds, signed area and orientation.

## Diagnostics & Repair

- Small gap (≤ repair tolerance) → repairable; both ends move to the midpoint.
- Open endpoints without a matching end → "no matching endpoint" (never a
  numeric sentinel).
- Branch nodes, zero-length entities, very small entities, duplicates
  (incl. reversed lines), line-line self intersections.
- Single repair and "repair all safe gaps" (one undo restores the batch).

## Project Save / Load

- `.dxfstudio` JSON format, schema version 1.
- Lossless geometry round-trip (Line / Arc / Circle / Polyline incl. arc
  segments), layers, units and tolerance settings.
- Dirty-state indicator and unsaved-changes guard on open / open-project /
  exit.

## Clean DXF Export

- LINE / ARC / CIRCLE / LWPOLYLINE (+ bulge) output.
- Configurable output unit (default: source unit, mm supported) and DXF
  version (R12 / R2000 / R2010 / R2018, default R2018).
- Never overwrites the source file unless explicitly allowed.

## Architecture

```
src/DxfContourStudio.Core         pure geometry / topology — no WPF, no DXF
src/DxfContourStudio.Dxf          ACadSharp adapter (read + write), library-free contracts
src/DxfContourStudio.Application  documents, import/export, projects, commands, selection
src/DxfContourStudio.Wpf          WPF UI only (MVVM, CommunityToolkit.Mvvm)
```

See `docs/ARCHITECTURE.md` and `docs/ADR/` for details.

## Quick Start

Requirements:

- Windows 10/11
- .NET 10 SDK (see `global.json`)
- Visual Studio 2022 (optional)

Clone and build:

```sh
git clone <repo-url>
cd DxfContourStudio
dotnet restore DxfContourStudio.sln
dotnet build DxfContourStudio.sln -c Release
dotnet test  DxfContourStudio.sln -c Release
dotnet run --project src/DxfContourStudio.Wpf
```

## Build

```sh
dotnet restore DxfContourStudio.sln
dotnet build DxfContourStudio.sln -c Debug      # 0 warnings / 0 errors
dotnet build DxfContourStudio.sln -c Release
```

## Tests

- `tests/DxfContourStudio.Core.Tests` — geometry, topology, contours,
  intersection, diagnostics math.
- `tests/DxfContourStudio.Dxf.Tests` — import mapping, unit conversion, bulge.
- `tests/DxfContourStudio.Application.Tests` — documents, commands, projects,
  export round-trip, golden corpus, view-model integration, STA offscreen
  render regression.

Current baseline: **369 tests passing** (Debug + Release, 0 warnings /
0 errors). See `docs/TESTING.md`.

## Test Corpus

`testdata/dxf/` holds a hand-authored regression corpus (19 files) covering
rectangles, nesting, gaps, branches, duplicates, self intersections, polylines
with bulges, mixed layers and unitless files. See `docs/TEST_CORPUS.md`.

## Known Limitations

- Self-intersection analysis covers line/arc/polyline runs; tangent touches
  are intentionally not reported as intersections.
- R12 export is offered but not yet manually exercised end-to-end.
- This is **not** a complete CAD/CAM system: no Offset / Fillet / Chamfer,
  no machine / laser / PLC / GCode integration.

## Roadmap

See `docs/ROADMAP.md`.

## License

Apache-2.0 — see [LICENSE](LICENSE).
