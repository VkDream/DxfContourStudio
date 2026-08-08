# ADR-002: Layering and dependency rules

- Status: Accepted
- Date: 2026-08-07

## Context

For a tool that must stay auditable (a "workbench" for laser/nesting-ish
geometry, future topology, MES-integrations optional), the dependency layout
must survive a long life and unknown future additions. Everything has a place,
and nothing cross-wires.

The initial scaffold creates:

- `DxfContourStudio.Core` — pure math, geometry, tolerances, entities
- `DxfContourStudio.Dxf` — DXF import/export adapter (ACadSharp)
- `DxfContourStudio.Application` — document model, import service,
  viewport math, selection
- `DxfContourStudio.Wpf` — UI only

## Decision

Dependency is one-way and strict:

```
Wpf → Application → Core
Wpf ──────→ Dxf ───────→ Core
```

- `Core` never references WPF/DXf/Application.
- `Application` references Core and Dxf.Abstractions (via the Dxf project);
  it loads no third-party DXF tables directly.
- `Wpf` references everything but owns rendering/interaction state only.
- `Dxf` depends only on Core and ACadSharp; its public-facing records
  (IDxfReader etc.) must not leak ACadSharp types outward.

## Consequences

- Swapping DXF backends touches only `Dxf`.
- Rendering concerns (Drawing, StreamGeometry, Brushes) never reach
  `Application`/Core geometry code.
- Storing entity data in Core keeps the door open to headless CLT/scripting
  later.