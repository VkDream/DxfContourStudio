# ADR-008: DXF export units

- Status: Accepted
- Date: 2026-08-07

## Context

The engine canonicalizes all coordinates to millimeters at import
(ADR-003). A cleaned-DXF export must decide in which unit to write. Options:

A. Always export millimeters.
B. Preserve the source unit (convert mm → source unit on write).

Option B makes the exported file behave like the original for downstream
tools (same numbers as the source drawing) and keeps the round-trip
`export → re-import` numerically identical to the source.

## Decision

- **Default**: export in the document's unit (the interpreted source unit,
  usually Millimeter). `DxfExportOptions.OutputUnit` overrides (e.g. force mm
  for a file that came in as inches).
- The exporter converts internal mm → output unit on write and sets the
  matching `$INSUNITS` so a re-import converts back and yields the same
  millimeters.
- `UnitConverter` is the single source of unit factors; the writer never
  hard-codes conversion constants.

## Consequences

- Round-trip tests cover mm (default) and inch output: a 100 mm line exported
  as inches re-imports as 100 mm within 1e-6.
- The DXF writer must know the document's interpreted unit; the document
  stores it (`CadDocument.Units`) and the export service reads it.
