# ADR-020: Analysis staleness after editing (stale banner)

- Status: Accepted
- Date: 2026-08-11

## Context

Interactive tools (Join/Break/Trim/Extend/NodeEdit, ADR-019) change geometry
after an analysis was run. Keeping the old diagnostics/overlay rendered
against edited geometry is misleading; silently discarding them hides the
fact that the numbers no longer apply.

## Decision

- `MainViewModel` tracks `IsAnalysisStale` + `AnalysisRevision`.
- Any undo/redo, tool commit, or document mutation makes the analysis
  stale: `MarkAnalysisStale()` keeps `AnalysisResult` (so the "Analyze"
  button can still be re-run) but clears `SelectedDiagnostic`,
  `DiagnosticItems`, `ContourItems`, `CurrentDiagnostics`,
  `CurrentGeometryDiagnostics` and raises the banner.
- `Analyze` (and the gap-repair commands, which are deliberate fixes)
  call `MarkAnalysisFresh()` and increment `AnalysisRevision`.
- The UI shows a localized banner on the Diagnostics panel while stale;
  overlay markers tied to the old analysis are not drawn.
- `RefreshAnalysis()` no longer resets staleness on its own;
  `RefreshAllLocalized` keeps the banner visible for stale content.

## Consequences

- The user can never misread a stale contour/diagnostic listing as current.
- Undo of an edit restores the *previous* analysis state snapshot, so
  undo/redo pairs are consistent (covered by
  `MainViewModelIntegrationTests.SmallGap` and `MainViewModelToolTests`).
