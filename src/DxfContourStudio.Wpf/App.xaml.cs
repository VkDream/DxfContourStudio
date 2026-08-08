using System.IO;
using System.Windows;
using System.Windows.Threading;
using DxfContourStudio.Application.Localization;
using DxfContourStudio.Wpf.Localization;

#nullable enable

namespace DxfContourStudio.Wpf;

/// <summary>
/// Interaction logic for App.xaml. Applies the persisted UI culture (zh-CN
/// default) before the main window is created so every XAML binding starts in
/// the right language, and registers process-wide unhandled-exception hooks
/// that write a crash log instead of letting the app die silently.
/// </summary>
public partial class App : System.Windows.Application
{
    /// <summary>
    /// Directory for crash/analysis logs. Resolved lazily so a logging attempt
    /// never throws inside an exception handler (worst case: log to the temp
    /// folder).
    /// </summary>
    private static readonly Lazy<string> LogDirectory = new(() =>
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DxfContourStudio", "logs");
            Directory.CreateDirectory(dir);
            return dir;
        }
        catch
        {
            string fallback = Path.GetTempPath();
            try
            {
                Directory.CreateDirectory(fallback);
            }
            catch
            {
            }

            return fallback;
        }
    });

    protected override void OnStartup(StartupEventArgs e)
    {
        // Process-wide unhandled exception hooks: record every crash with the
        // full stack trace to a log file. The Dispatcher hook also prevents
        // the default behavior of terminating the process so a recoverable UI
        // failure never kills the app silently (the log keeps it diagnosable).
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // Apply the persisted UI culture BEFORE base.OnStartup creates the
        // main window: {loc:Loc} values resolve at XAML parse time, so the
        // window must be built with the right culture already in place.
        LocalizationService.Instance.SetCulture(AppSettings.LoadCulture());
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogUnexpected("DispatcherUnhandledException", e.Exception);

        // Policy (see docs/ERROR_HANDLING.md):
        //  - recoverable UI exceptions (binding/layout noise) keep the app
        //    running and are logged;
        //  - programmer errors are NOT silently swallowed: in Debug builds we
        //    let the debugger surface them (Debugger.IsAttached → rethrow);
        //  - in Release, a UI exception that is not recoverable should not
        //    leave the app in a broken half-state silently — we mark it
        //    handled ONLY when it is a known recoverable category; otherwise
        //    we let the app terminate so the failure is visible and the log
        //    keeps the evidence.
        if (System.Diagnostics.Debugger.IsAttached)
        {
            return; // debugger handles it; do not swallow
        }

        if (IsRecoverableUiException(e.Exception))
        {
            e.Handled = true;
            return;
        }

        // Unknown fatal: keep the log, let the app shut down cleanly instead
        // of continuing in an undefined state.
        e.Handled = false;
    }

    /// <summary>
    /// Known recoverable categories: XAML parse failures at first load and
    /// argument/IO errors that come from a user interaction (file dialog etc.).
    /// Everything else is treated as fatal so programmer errors stay visible.
    /// </summary>
    private static bool IsRecoverableUiException(Exception ex)
    {
        return ex is System.Windows.Markup.XamlParseException or
               ArgumentException or
               System.IO.IOException;
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogUnexpected("AppDomainUnhandledException", e.ExceptionObject as Exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogUnexpected("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    /// <summary>
    /// Writes an unexpected exception to the log directory with a timestamped
    /// file name and returns the log path. Never throws (a logging failure
    /// must not mask the original error).
    /// </summary>
    public static string LogUnexpected(string stage, Exception? ex)
    {
        string path = Path.Combine(LogDirectory.Value, $"crash-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] stage={stage}");
            sb.AppendLine($"app=DxfContourStudio.Wpf version={System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
            sb.AppendLine(ex?.ToString() ?? "(null exception)");
            File.WriteAllText(path, sb.ToString());
        }
        catch
        {
            // Logging must never mask the original failure.
        }

        return path;
    }
}
