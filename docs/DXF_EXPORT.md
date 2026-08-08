# DXF_EXPORT.md — Clean DXF Export

Exports the current internal geometry back to a DXF file so a cleaned /
repaired drawing can be reused in any CAD tool.

## Architecture

```
Application/Documents (CadDocument)
        │  entities + layers (library-free)
        ▼
Application/Exports/DxfExportService   ← overwrite guard, options
        │  IDxfWriter (Dxf/Abstractions)
        ▼
Dxf/Infrastructure/AcadSharpDxfWriter  ← the ONLY place that knows ACadSharp writer types
```

The Application layer never touches a concrete writer library
(ADR-002 layering). Swapping the backend later touches only the Dxf project.

## Supported output

| Internal kind | DXF entity |
|---|---|
| LineGeometry | LINE |
| CircleGeometry | CIRCLE |
| ArcGeometry | ARC |
| PolylineGeometry (line segments) | LWPOLYLINE |
| PolylineGeometry (arc/bulge segments) | LWPOLYLINE with bulge |

Anything else is **counted and reported as a warning** — never silently
dropped (`DxfExportReport.Skipped`).

## Units (ADR-008)

The internal geometry is always millimeters. `DxfExportOptions.OutputUnit`
(default: the document's unit, usually Millimeter) converts coordinates back
and writes the matching `$INSUNITS`, so a re-import yields the same
millimeters. Round-trip is tested for mm and inch.

## Versions

`DxfExportVersion`: R12, R2000, R2010, R2018 (default R2018). These map to
ACadSharp `ACadVersion` values the writer can produce. R12 has no LWPOLYLINE;
this version is offered but not currently exercised by the tests.

## Overwrite protection

The exporter **refuses** to overwrite the DXF the document was imported from
unless `DxfExportOptions.OverwriteSource` is explicitly true. The UI always
opens a Save As dialog; the default file name appends `_cleaned`.

## Export report

`DxfExportReport` carries: output file, written / skipped counts, warning /
error counts, version, output unit, duration, per-kind written/skipped
statistics and a message list. The UI shows the result in the status bar
(`Status.ExportDone`).

## Tests

`DxfExportRoundTripTests`:
- small_gap_003 → repair → export → re-import → gap = 0, open chain stays open (Closed 0)
- rectangle_gap_003 → repair → export → re-import → gap = 0, contour stays closed (Closed 1)
- outer_hole → export → re-import → Outer 1 / Hole 1, bounds equal
- basic-scene → export → re-import → geometry precision preserved
- export to source path refused by default
- inch output re-imports to the same millimeters

All exports in tests go to temp directories; nothing is ever written into the
project tree.
