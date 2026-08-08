# DxfContourStudio architecture

Status: matches the code as of 2026-08-08 (v0.2.0)
hardening). ADR files in `docs/ADR/` record the decisions.

## Solution layout

```
DxfContourStudio.sln
鈹溾攢 src
鈹? 鈹溾攢 DxfContourStudio.Core        # pure geometry/topology 鈥?no WPF, no DXF
鈹? 鈹? 鈹溾攢 Geometry/                 # Point2..GeometryTolerance, entities, PathSegments
鈹? 鈹? 鈹溾攢 Topology/                 # TopologyBuilder/Graph/Node/Edge, EndpointMatcher
鈹? 鈹? 鈹溾攢 Contours/                 # ContourAnalyzer, chains, assembly, nesting, gaps
鈹? 鈹? 鈹斺攢 Diagnostics/              # GeometryDiagnostic, analyzers, GeometrySanity
鈹? 鈹溾攢 DxfContourStudio.Dxf         # ACadSharp adapter (read + write)
鈹? 鈹? 鈹溾攢 Abstractions/             # IDxfReader, IDxfWriter, import/export models
鈹? 鈹? 鈹斺攢 Infrastructure/           # AcadSharpDxfReader/Mapper/Writer, BulgeConverter
鈹? 鈹溾攢 DxfContourStudio.Application # documents, imports, exports, projects, commands
鈹? 鈹? 鈹溾攢 Documents/CadDocument (+LayerState, dirty, unsaved guard)
鈹? 鈹? 鈹溾攢 Imports/DxfImportService
鈹? 鈹? 鈹溾攢 Exports/DxfExportService
鈹? 鈹? 鈹溾攢 Projects/ProjectSerializer (+ .dxfstudio model)
鈹? 鈹? 鈹溾攢 Commands/ (history, move/delete, gap repair, batch/composite)
鈹? 鈹? 鈹斺攢 Selection/SelectionModel, Viewport, HitTester
鈹? 鈹斺攢 DxfContourStudio.Wpf         # WPF UI only
鈹?    鈹斺攢 Views/CadViewport (OnRender + overlay), ViewModels/MainViewModel, MainWindow
鈹斺攢 tests
   鈹溾攢 DxfContourStudio.Core.Tests
   鈹溾攢 DxfContourStudio.Dxf.Tests
   鈹斺攢 DxfContourStudio.Application.Tests   # incl. STA offscreen render + golden corpus
```

## Data flow (import / analyze / repair / export / project)

```
*.dxf 鈫?AcadSharpDxfReader 鈫?DxfImportResult 鈫?DxfImportService 鈫?CadDocument
CadDocument 鈫?ContourAnalyzer 鈫?ContourAnalysisResult (contours + diagnostics)
CadDocument + analysis 鈫?RepairGapCommand / BatchRepairCommand (undoable)
CadDocument 鈫?DxfExportService 鈫?IDxfWriter 鈫?*_cleaned.dxf
CadDocument 鈫?ProjectSerializer 鈫?*.dxfstudio (JSON, lossless)
```

## Key invariants

1. **No ACadSharp type outside the Dxf project**: `DxfImportResult` /
   `DxfExportReport` / `IDxfWriter` are the only contracts crossing the
   boundary; Core never sees a parser/writer type.
2. **All world coordinates are millimeters**; `UnitConverter` documents
   factors; export converts back per `DxfExportOptions.OutputUnit`.
3. **Arcs are never full circles**; a 2蟺 sweep is a `CircleGeometry`.
4. **The UI never mutates geometry**: commands live in Application; the Wpf
   layer only renders and forwards gestures.
5. **Tolerances are centralized** in `GeometryTolerance` and persisted with
   the project.
6. **Rendering never uses open StreamGeometry with isStroked:false runs** 鈥?   open figures are drawn with `DrawLine` (regression-guarded by offscreen
   render tests).

## Testing

`docs/TESTING.md` describes the projects and the key regression guards.
Run: `dotnet build DxfContourStudio.sln` then `dotnet test DxfContourStudio.sln`
(Debug and Release, both 0W/0E).

## Open items / later phases

- Self intersection for line-arc / arc-arc; repair preview; 9 extra
  unit/unitless fixtures; DrawingVisual batching for very large drawings.
- CAM / GCode / machine integration: out of scope.

