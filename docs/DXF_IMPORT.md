# DXF_IMPORT.md 鈥?DxfContourStudio

How a DXF file becomes the internal geometry model.

## Path

```
*.dxf
 鈫?ACadSharp DxfReader (parser notifications 鈫?report messages)
 鈫?AcadSharpEntityMapper (units 鈫?mm, entity kinds 鈫?IGeometryEntity)
 鈫?DxfImportResult (entities, layers, report)
 鈫?DxfImportService 鈫?CadDocument (stamps summary, units, source path)
```

## Supported entities

| DXF entity | Internal | Notes |
|---|---|---|
| LINE | LineGeometry | zero-length dropped with warning |
| ARC | ArcGeometry | angles read in **radians** (ACadSharp exposes radians; the mapper converts once 鈥?`ArcSweepRadians`) |
| CIRCLE | CircleGeometry | radius 鈮?0 dropped |
| LWPOLYLINE | PolylineGeometry | per-vertex bulge 鈫?ArcSegment |
| POLYLINE/Vertex2D | PolylineGeometry | same segment model |
| ELLIPSE / SPLINE / INSERT / TEXT | 鈥?| counted in statistics, reported, skipped |

## Units (ADR-003)

`$INSUNITS` 鈫?`LengthUnit` 鈫?`UnitConverter.ToMillimetersFactor`. Unknown /
unitless files degrade to millimeters with a reported warning (never silent).

## Report

`DxfImportReport` carries: file info, DXF version string, declared vs
interpreted units, layer count, total/imported counts, warnings/errors, per-kind
statistics, and the full message list. The UI renders it in the Import Report
tab (`ImportReportBuilder`).

## Tests

- Dxf.Tests: unit conversion, bulge conversion, entity mapping.
- Application.Tests: import service over the regression corpus
  (19 files, see TEST_CORPUS.md).

