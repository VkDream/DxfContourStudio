# ADR-001: DXF parsing library

- Status: Accepted
- Date: 2026-08-07
- Decision maker: @aus or owner (@auscooper-contractors)

## Context

DxfContourStudio must read DXF files reliably and, later, write `.dxf`. No
industrial project should hand-roll a DXF parser when a maintained OSS library
already covers the format (group codes, DXF class table, unit declarations,
version differences).

Candidates researched on NuGet / GitHub (as of 2026-08-07):

| Library        | License | Last release          | .NET 10 target | Notes |
|:---------------|:--------|:----------------------|:---------------|:------|
| **ACadSharp**  | MIT     | 3.6.51 (active 2026)  | yes (`net10.0`) | read + write, DWG+DXF, actively maintained |
| IxMilia.Dxf   | MIT     | 0.8.4 (2024-06)       | runtime repacks | no recent release cadence |
| netDxf (haplokuon) | MIT | archived Oct 2023    | –               | archived, risky as long-term dependency |

Facts verified during this project (temp probe, ACadSharp 3.6.51):

- `DxfReader.Read(path)` → `CadDocument`; `doc.Entities` enumerates model
  space; `doc.Header.InsUnits` is `UnitsType` (0 Unitless … 4 Millimeters …);
  `doc.Layers` gives layer table entries.
- `LwPolyline.Vertex.Location` is `CSMath.XY` and carries `Bulge`;
  `Polyline2D.Vertices` are `Vertex2D` with inherited `Vertex.Location`
  (`CSMath.XYZ`) and `Bulge`.
- Arc angles (`StartAngle`, `EndAngle`, ...) are reported in **degrees** and
  must be converted to the internal radian convention.

## Decision

Use **ACadSharp 3.6.51** as the DXF I/O backend in the `Dxf` infrastructure
layer.
No DXF-library types leave the `Dxf.Infrastructure` namespace: every other
layer consumes only `Core.Geometry` shapes and the `Dxf.Abstractions`
records (`IDxfReader`, `DxfImportResult`, `ImportedLayerInfo`,
`DxfImportReport`).

## Consequences

- DXF import in Phase 1 is provided by `AcadSharpDxfReader` (implements
  `IDxfReader`) plus `AcadSharpEntityMapper`.
- Bench DXF format peculiarities (degrees vs radians, bulge) live in one
  place.
- The license (MIT) is compatible with the Apache-2.0 project itself; all
  binaries carry 3rd-party notices in the published README.