# TESTING.md — DxfContourStudio

## Test projects

| Project | Focus | Runs on |
|---|---|---|
| Core.Tests | geometry, topology, contours, intersection, diagnostics math | net10.0 |
| Dxf.Tests | import mapping, unit conversion, bulge | net10.0 |
| Application.Tests | documents, commands, projects, export round-trip, golden corpus, WPF view-model integration, **STA offscreen render** | net10.0-windows (UseWPF) |

No GUI automation: no window clicks, no screenshots, no UIA-driven visual
checks. The WPF renderer is covered by **offscreen RenderTargetBitmap**
rendering on an STA thread (`ViewportOffscreenRenderTests`), which proves
real non-background pixels without touching the desktop.

## Golden corpus

`docs/TEST_CORPUS.md` documents every file in `testdata/dxf/`;
`RegressionCorpusGoldenTests` asserts the real imported numbers (entity
counts, bounds, contour census, diagnostics) for each.

## Key regression guards

- **Renderer**: `ViewportOffscreenRenderTests` — basic-scene / outer_hole /
  small_gap must produce ink pixels. This is the permanent guard for the
  open-StreamGeometry stroke bug.
- **Round-trip**: `ProjectRoundTripTests` (save→load geometry equality,
  repaired-state persistence, schema rejection, tolerance round-trip) and
  `DxfExportRoundTripTests` (repair→export→re-import gap=0, nesting
  preservation, precision, overwrite refusal, inch output).
- **Repair**: `RepairGapCommandTests`, `BatchRepairAndDirtyTests` (batch =
  one composite undo), `MainViewModelIntegrationTests` (Analyze→Repair→Undo→
  Redo through the real VM).
- **Intersection**: `IntersectionEngineTests` (cross, touch, parallel,
  collinear overlap, near-tolerance, degenerate).
- **Diagnostics**: `GeometryDiagnosticsTests` (zero length, very small,
  duplicates incl. reversed, bow-tie self intersection, NaN guard).
- **Localization**: key-set parity zh/en + format-placeholder safety.
- **Performance**: `PerformanceSanityTests` — 1k/10k/50k build, bounds,
  topology, render prep, hit tests inside generous budgets.

## Conventions

- Tests never write into the project tree: generated DXF/project files go to
  `Path.GetTempPath()` subdirectories, cleaned up in `finally`.
- No test may depend on execution order.
- Assertions compare geometry (numeric tolerance), never serialized strings.
- xUnit analyzers are enforced (0 warnings policy).

## Gate

```
dotnet restore
dotnet build -c Debug   (0W/0E)
dotnet test  -c Debug
dotnet build -c Release (0W/0E)
dotnet test  -c Release
```
