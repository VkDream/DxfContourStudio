# DIAGNOSTICS.md — DxfContourStudio

This document describes every diagnostic the engine can produce, its
severity, and how the UI surfaces it.

## Model

Two sources feed the diagnostics panel:

1. **Topology diagnostics** (`Core/Contours/GapDiagnosticsBuilder.cs`) —
   derived from open chains: small gaps, open endpoints, branch nodes.
2. **Geometry diagnostics** (`Core/Diagnostics/*`) — derived from the raw
   entities: zero-length, very small, duplicates, self intersections, invalid
   (NaN/Infinity) geometry.

The panel merges both into one ordered list; each row carries a severity
(Info / Warning / Error) shown as a colored label.

## Severity policy

| Finding | Severity | CanAutoRepair |
|---|---|---|
| Small repairable gap (≤ repair tolerance) | Warning | yes |
| Open endpoint without repairable match | Error | no |
| Branch node (degree > 2) | Error | no |
| Zero-length geometry | Error | no |
| Very small geometry (< SmallGeometryThreshold, > zero tolerance) | Warning | no |
| Duplicate entity pair | Warning | no |
| Self intersection | Error | no |
| Invalid geometry (NaN/Infinity) | Error | no |

Colors resolve through the theme resources `DiagnosticWarning` /
`DiagnosticError` (see `Styles/Colors.xaml`).

## Findings detail

### SmallGap
Two dangling ends closer than `EndpointSnapTolerance` (0.05 mm default).
Repair moves both ends to their midpoint (ADR-007). UI: orange connector.

### OpenEndpoint
A dangling end with no match within the repair tolerance. UI: orange-red dot.
Not auto-repairable.

**Distance semantics:** `GapDiagnostic.HasDistance` distinguishes two cases:
- `HasDistance = true` → a real measured distance to the nearest candidate
  exists (beyond tolerance): the UI shows "距离 X — 超出修复范围".
- `HasDistance = false` → no candidate endpoint exists anywhere near: the UI
  shows "无可匹配端点" (no number at all).

A `double.MaxValue` sentinel is **never** used as "no distance" — sentinels
must not leak into the UI. `DisplayFormat` additionally refuses to print
NaN / Infinity / double.MaxValue / double.MinValue (renders the localized
"无" placeholder).

### BranchNode
A node where three or more edges meet. UI: red dot. `GapDiagnostic.BranchNodeId`
maps back to the topology node; degree is available from the graph.

### ZeroLength
An entity whose measured length ≤ `ZeroLengthTolerance` (1e-6 mm). At import
such entities are dropped with a warning; the analyzer would flag them as
errors if they ever reach the document. UI: crimson dot.

### VerySmall
Length above the zero tolerance but below `SmallGeometryThreshold` (0.01 mm).
Warning only. UI: amber dot.

### Duplicate
Two entities describing the same geometry within `DuplicateTolerance`
(1e-6 mm): identical lines (either direction), identical circles, identical
arcs. Diagnostics only — nothing is deleted. UI: magenta ring; the detail
shows `#A ↔ #B`.

### SelfIntersection
Two non-adjacent segments of a closed contour genuinely cross. Adjacent
segments sharing an endpoint are excluded; the closing segment is adjacent to
the first. Line-line only in this phase; line-arc and arc-arc are
**NOT_SUPPORTED_YET** and produce no finding. UI: red X at the crossing.

## Thresholds

All thresholds live in `Core/Geometry/GeometryTolerance.cs` — never hard-code
magic numbers in analyzers:

- `PointEqualityTolerance` = 1e-6 mm
- `EndpointSnapTolerance` = 0.05 mm (repair tolerance)
- `ZeroLengthTolerance` = 1e-6 mm
- `SmallGeometryThreshold` = 0.01 mm
- `DuplicateTolerance` = 1e-6 mm
- `SelfIntersectionTolerance` = 1e-6 mm
- `ClosureTolerance` = 0.05 mm
- `MinimumAreaTolerance` = 1e-6 mm²

## Locating a finding

Selecting a diagnostic row does not move the viewport yet; the row shows the
world position. The Next/Prev anomaly buttons step through the rows.
