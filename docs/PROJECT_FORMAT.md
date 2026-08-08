# PROJECT_FORMAT.md 鈥?.dxfstudio

The native project format is **JSON** with the extension **`.dxfstudio`**
(ADR-009). It stores the full editable state of a drawing so a save鈫抣oad
cycle reproduces the document exactly.

## Schema

```json
{
  "schemaVersion": 1,
  "applicationVersion": "0.2.0",
  "source": {
    "fileName": "part.dxf",
    "filePath": "C:\\...\\part.dxf",
    "dxfVersion": "AC1027",
    "importSummary": "..."
  },
  "units": "Millimeter",
  "tolerance": {
    "pointEqualityTolerance": 1e-6,
    "endpointSnapTolerance": 0.05,
    "zeroLengthTolerance": 1e-6,
    "closureTolerance": 0.05,
    "smallGeometryThreshold": 0.01
  },
  "diagnostics": {
    "duplicateTolerance": 1e-6,
    "selfIntersectionTolerance": 1e-6
  },
  "layers": [
    { "name": "0", "isOn": true, "isFrozen": false,
      "aciColorIndex": 7, "isColorByLayer": true, "isVisible": true }
  ],
  "entities": [
    { "id": 1, "kind": "Line", "layer": "0", "visible": true,
      "line": { "p0X": 0, "p0Y": 0, "p1X": 100, "p1Y": 0 } }
  ]
}
```

Entity kinds: `Line`, `Arc`, `Circle`, `Polyline` (with `line` / `arc`
segments). Arc angles are stored in radians in the internal convention
(CCW from +X, signed sweep) so no precision is lost.

## Round-trip guarantees

- Entity ids, order, layers, visibility and units are preserved.
- Polyline arc segments (bulge-converted) serialize losslessly 鈥?an arc never
  becomes a line.
- Tolerance settings round-trip.
- **Analysis results are NOT persisted** 鈥?they are derived state, rebuilt by
  running Analyze after load.

## Versioning

- `schemaVersion` 1 = current format. Loaders reject files with a newer
  schema version (`NotSupportedException`), tolerate unknown fields.
- `applicationVersion` records the writer app version (informational).

## Dirty state

- Any geometry mutation (import, move, delete, gap repair, batch repair,
  project load) marks the document dirty via `CadDocument.MarkDataChanged`.
- **Layer visibility is a view preference and does NOT dirty the document**
  (ADR-009) 鈥?the project stores layer visibility anyway, but toggling a
  checkbox alone does not prompt "unsaved changes".
- Saving a project clears the dirty flag; the window title shows ` * ` while
  dirty.

## Unsaved-changes guard

Open DXF / Open Project / Exit consult `IUnsavedChangesPrompt` (WPF:
`UnsavedPromptBox` with Save / Discard / Cancel). The decision flow lives in
`UnsavedChangesGuard` and is unit tested with a stub prompt.

