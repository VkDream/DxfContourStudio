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
  collinear overlap, near-tolerance, degenerate) and
  `SelfIntersectionTests` (all runs, tangent not flagged).
- **Editing (D4–D7)**: `JoinTests` / `JoinManyCommandTests` (chain join +
  single undo), `BreakTests` (midpoint, arc, polyline run, undo/redo),
  `TrimExtendTests` (trim both sides, extend line/arc/polyline, no-op,
  undo), `NodeEditTests` (re-splice arcs, shared vertices, closed polys).
- **Interactive tools (D13A–D17)**: `EditToolSessionTests` (activation,
  step picking, hover, escape chains, overlay data, single undo per edit,
  document-changed invalidation), `TrimSectionCommandTests` (id assignment
  for kept/discarded pieces), `MainViewModelToolTests` (tool activation via
  string command parameters, full join pipeline, refusal statuses, undo
  invalidates analysis), `MainViewModelIntegrationTests` stale-banner
  snapshots across undo/redo.
- **Scale (D9/D11)**: `SpatialIndexTests` + `SpatialIndexStressTests` and
  `DocumentPickStressTests` — 100k entities, correctness pinned against a
  linear-scan oracle; a regression to O(n) picking would surface as a CI
  hang.
- **Edited-geometry round-trip (D10)**: `EditedGeometryRoundTripTests` —
  mixed polyline arc runs, sweep sign, closed flags, node moves survive
  save → load byte-exact.
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
