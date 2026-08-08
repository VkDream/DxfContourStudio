# ERROR_HANDLING.md — DxfContourStudio

Policy for how failures are contained, logged and surfaced. See also
ADR-010-Error-Handling.

## Layered handling

1. **Business errors** (import fail, analyze fail, save/load fail, export
   fail, refused overwrite) never crash the process. The ViewModel catches
   them, shows a localized status line, and logs the exception to
   `%LocalAppData%\DxfContourStudio\logs\crash-<timestamp>.log` via
   `App.LogUnexpected(stage, ex)`.
2. **Known recoverable UI exceptions** (XAML parse at first load, argument
   errors, IO errors from user dialogs) are logged; the Dispatcher hook marks
   them handled and the app keeps running.
3. **Unknown programmer errors** are NOT swallowed. Policy:
   - Debug with a debugger attached: rethrow / let the debugger surface it.
   - Release: the Dispatcher hook does NOT set `e.Handled` for unknown
     categories, so the process terminates with the log as evidence — the
     app never continues in an undefined half-broken state.
4. `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`
   always log; unobserved tasks are marked observed.

## What is logged

Startup, Open DXF, Open Project, Analyze (with diagnostic counts), Repair,
Batch Repair, Undo/Redo, Save Project, Export DXF, and every unexpected
exception. High-frequency events (mouse move, render frames) are never logged.

## Why not "handle everything"

Permanently setting `e.Handled = true` for every Dispatcher exception hides
programmer errors: the app keeps running with corrupted state and the real
bug is deferred. The current policy keeps the app alive only for categories
that are provably recoverable and lets everything else fail loudly with a log.

## Guard rails

- `App.LogUnexpected` never throws (a logging failure must not mask the
  original error).
- File dialogs / save / export paths are validated before use.
- The DXF exporter refuses to overwrite the source file by default
  (a "safe failure" instead of data loss).
