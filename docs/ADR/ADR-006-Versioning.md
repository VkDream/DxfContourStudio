# ADR-006: Versioning & test-suite policy

- Status: Accepted (updated for v0.2.0)
- Date: 2026-08-07 (original), 2026-08-08 (v0.2.0 promotion)

## Context

v0.1.0 was a delivery snapshot for the A+B batches (WPF productisation and the
topology → contour → gap-repair → nesting analysis). v0.2.0 is the **first
public functional release**: diagnostics hardening, project save/load, clean
DXF export, round-trip regression, performance work and a regression corpus
landed after 0.1.0. A reliable version identity is needed for the About dialog
and for tests that pin the delivery.

## Decision

- **Assemblies are at `0.2.0`** for the public release.
  - `AssemblyVersion` / `FileVersion` = `0.2.0.0`
  - `AssemblyInformationalVersion` = `0.2.0`
- The single source of truth is the root `Directory.Build.props`
  (`Version`, `AssemblyVersion`, `FileVersion`, `InformationalVersion`).
  The WPF About dialog shows the semantic part of
  `AssemblyInformationalVersion`; no XAML/VM/About/csproj hard-codes a
  different version.
- **Test gate**: every code change must keep the whole solution at
  `0 warnings, 0 errors` in both Debug and Release, and `dotnet test` green.
  Version identity is asserted by `VersioningTests` for the Application and
  Core assemblies.
- v0.2.0 is **frozen** after release: no unrelated feature commits; new
  functionality enters 0.3.0 development.

## Consequences

- Tests pin the delivery version, so the gate cannot silently drift.
- A release tag `v0.2.0` and a GitHub Release document the public snapshot.
- Version policy for future releases follows semantic versioning from here.
