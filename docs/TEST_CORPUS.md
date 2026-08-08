# TEST_CORPUS.md — DxfContourStudio Regression DXF Corpus

Every file in `testdata/dxf/` is **hand-authored for this project** (ASCII DXF
generated from `gen-corpus`-style scripts or written directly). No customer
drawings, no company drawings, no downloaded files. All coordinates are in
millimeters unless noted.

Golden expectations are asserted by `RegressionCorpusGoldenTests`
(Application tests) and `GeometryDiagnosticsTests` / `IntersectionEngineTests`
(Core tests).

## Files

| File | Purpose | Entities | Contours | Diagnostics | Units |
|---|---|---|---|---|---|
| basic-scene.dxf | mixed scene: polyline+2 lines+circle+arc | 5 | 2 closed / 2 open | 1 branch, 3 open ends | mm |
| outer_hole.dxf | outer 200×150 rect + inner 50×50 rect (as closed LWPOLYLINEs) | 2 | 2 closed: 1 outer, 1 hole | none | mm |
| small_gap_003.dxf | two lines with a 0.030 mm **internal** gap (open chain, NOT a contour) | 2 | 2 open | 1 SmallGap (0.030 mm, repairable) — repair joins the chain (open 2→1), never closes it | mm |
| rectangle_gap_003.dxf | 100×60 rectangle with a 0.030 mm gap at one corner | 4 | 1 open | 1 SmallGap (0.030 mm, repairable) — repair CLOSES the contour (closed 0→1) | mm |
| rectangle_lines.dxf | 100×60 rectangle as 4 LINEs | 4 | 1 closed outer | none | mm |
| rectangle_scrambled.dxf | same rectangle, segment order scrambled | 4 | 1 closed outer | none | mm |
| rectangle_reversed.dxf | same rectangle, mixed orientations | 4 | 1 closed outer | none | mm |
| line_arc_closed.dxf | LINE + semicircular ARC forming a closed loop | 2 | 1 closed outer (area π·50²/2) | none | mm |
| nested_island.dxf | outer rect, hole rect, island rect (depth 0/1/2) | 12 | 3 closed: 1 outer, 1 hole, 1 island | none | mm |
| large_gap.dxf | two lines 0.8 mm apart (> repair tolerance) | 2 | 2 open | 4 open ends, no SmallGap | mm |
| branch.dxf | three lines meeting at (50,50) + one isolated line | 4 | 4 open | 1 BranchNode at (50,50) | mm |
| zero_length.dxf | one zero-length LINE + one normal LINE | 2 (1 imported) | 1 open | zero-length dropped at import with warning | mm |
| duplicate_entity.dxf | duplicate LINE, reversed LINE, normal LINE + extra | 4 | 4 open | 3 Duplicate pairs | mm |
| self_intersection.dxf | bow-tie: four lines crossing at (50,50) | 4 | 1 closed (self-intersecting) | 1 SelfIntersection | mm |
| open_polyline.dxf | open LWPOLYLINE (3 vertices) | 1 | 1 open | none | mm |
| closed_polyline.dxf | closed LWPOLYLINE rectangle 80×50 | 1 | 1 closed outer | none | mm |
| bulge_polyline.dxf | closed LWPOLYLINE with bulge arcs | 1 | 1 closed outer | none | mm |
| mixed_layers.dxf | entities on layers OUTER / INNER / 0 | 6 | 2 closed (outer+hole), 1 open | none | mm |
| unknown_units.dxf | $INSUNITS=0 (unitless) square | 4 | 1 closed outer | unit assumed mm with warning | unitless→mm |

## Golden cases (user acceptance mapping)

| Case | File | Expected |
|---|---|---|
| CASE 1 Rectangle | rectangle_scrambled | Closed = 1 |
| CASE 2 Mixed | line_arc_closed | Closed = 1, area 3926.99 |
| CASE 3 Nesting | nested_island | Depth 0 / 1 / 2 (Outer/Hole/Island) |
| CASE 4 Small gap (chain) | small_gap_003 | RepairableGap 0.030 mm; Repair → **open chain 2→1, Closed stays 0**; Undo → gap 1; Redo → gap 0 |
| CASE 5 Gap closes contour | rectangle_gap_003 | Before: Closed 0, Gap 1 (0.030 mm); Repair → **Closed 1**; Undo → Closed 0; Redo → Closed 1; export→re-import stays Closed 1 |
| CASE 6 Branch | branch.dxf | Branch diagnostic at (50,50) |
| CASE 7 Zero length | zero_length.dxf | Zero-length handled at import (warning + drop) |
| CASE 8 Duplicate | duplicate_entity.dxf | Duplicate diagnostic (3 pairs) |
| CASE 9 Self intersection | self_intersection.dxf | SelfIntersection diagnostic at (50,50) |
| CASE 13 Outer/Hole | outer_hole | Export → re-import → Outer 1 / Hole 1 |

> **Topology contract (important):** small_gap_003 is an OPEN CHAIN with an
> internal gap — repairing it joins the chain but does NOT close a contour
> (Open stays 1, Closed stays 0). rectangle_gap_003 is the fixture where gap
> repair genuinely closes a contour. The two scenarios are tested separately
> (chain-connectivity repair vs contour-closure repair).

## Notes

- All files declare `$ACADVER=AC1027` (R2010) so ACadSharp reads them with
  the modern table handling.
- ARC angles in the corpus are written in degrees in the DXF (group 50/51);
  ACadSharp exposes them in radians — the mapper converts once (see
  `AcadSharpEntityMapper.ArcSweepRadians`).
- LWPOLYLINE bulge values follow the standard `tan(sweep/4)` convention.
- The corpus is copied into the test output by the Application.Tests csproj
  (`None Include=... testdata/dxf/*.dxf`).
