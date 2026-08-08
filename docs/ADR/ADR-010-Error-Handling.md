# ADR-010: Error handling policy

- Status: Accepted
- Date: 2026-08-07

## Context

v0.1.0 shipped a global `DispatcherUnhandledException` hook that set
`e.Handled = true` for **every** exception to keep the app alive. That hid
programmer errors: the app kept running with corrupted state and the real bug
was deferred to an unknown later failure. A permanent "handle everything"
policy is not acceptable for a desktop tool.

## Decision

Three layers, in order:

1. **Business errors** are caught at the command/view-model boundary,
   surfaced as localized status text, and logged
   (`App.LogUnexpected(stage, ex)` → `%LocalAppData%\DxfContourStudio\logs\`).
   Import / analyze / save / load / export failures never reach the global
   hooks.
2. **Known recoverable UI exceptions** — `XamlParseException` at first load,
   `ArgumentException`, `IOException` from user dialogs — are logged and
   marked `e.Handled = true`; the app keeps running.
3. **Unknown programmer errors** are **not swallowed**:
   - Debug with a debugger attached: the hook does not handle the exception
     (the debugger surfaces it).
   - Release: `e.Handled` stays false so the process terminates; the log file
     keeps the full stack trace as evidence. Continuing in an undefined state
     is worse than failing loudly.

`AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`
always log; unobserved tasks are marked observed.

## Consequences

- Recoverable UI noise no longer kills the app.
- Real bugs become visible instead of being masked; every failure is
  diagnosable from the log.
- The policy is documented in docs/ERROR_HANDLING.md.
