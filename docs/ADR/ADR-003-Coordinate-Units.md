# ADR-003: Internal coordinate system & units

- Status: Accepted
- Date: 2026-08-07

## Context

DXF files carry coordinates in the drawing's own unit system ($INSUNITS) —
often millimeters, sometimes inches, sometimes *unitless*. Mixing up units is
a classic source of "my part is 25.4x too big" bugs in laser/cutting software.
The whole engine must agree on one internal unit.

## Decision

- **All internal geometry is stored in millimeters (double precision).**
- The `Dxf` layer multiplies raw DXF coordinates by the factor derived from
  the interpreted unit (see `UnitConverter.ToMillimetersFactor`).
- `$INSUNITS` interpretation rules:
  - `Unitless (0)`: treated as millimeters with an informational message
    (units unknown, cannot be auto-detected reliably);
  - unknown / missing `$INSUNITS`: warning + assume millimeters;
  - otherwise map per the DXF table (1 inch → 25.4 mm, 2 foot → 304.8 mm,
    4 mm, 5 cm, 6 m, ...).
- The report (`DxfImportReport.DeclaredUnits` / `InterpretedUnits`) always
  tells the user what was assumed.

## Consequences

- All `IGeometryEntity` coordinates, `Bounds`, and distances are mm.
- UI status bar shows mm and the interpreted unit.
- Tests of the import path assert the 25.4x factor for inch files.