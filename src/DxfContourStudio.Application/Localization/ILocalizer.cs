#nullable enable

namespace DxfContourStudio.Application.Localization;

/// <summary>
/// Contract used everywhere a display string must be produced. The design
/// intent is that business code never hard-codes user-visible strings:
/// every message, label and formatted value flows through an <see cref="ILocalizer"/>.
///
/// Implementations should treat <see cref="CurrentCulture"/> as mutable state
/// that raises an event on change, so that bindings and view models can
/// refresh when the user switches language at runtime.
/// </summary>
public interface ILocalizer
{
    /// <summary>Current culture name (e.g. "zh-CN"). Changing it re-raises <see cref="CultureChanged"/>.</summary>
    string CurrentCulture { get; }

    /// <summary>Raised after <see cref="CurrentCulture"/> changes.</summary>
    event Action? CultureChanged;

    /// <summary>
    /// Returns the text for <paramref name="key"/>. Keys are dot-separated
    /// identifiers ("Menu.File.Open", "Status.Moved", ...). Fallback order for
    /// the built-in implementation: current culture → zh-CN → the key itself,
    /// so missing keys are never empty (they surface visibly as the key).
    /// </summary>
    string Get(string key);

    /// <summary>Returns the formatted text for <paramref name="key"/> using <see cref="string.Format"/> with <paramref name="args"/>.</summary>
    string Get(string key, params object?[] args);

    /// <summary>Whether the current culture (or its zh-CN fallback) defines the given key.</summary>
    bool HasKey(string key);
}