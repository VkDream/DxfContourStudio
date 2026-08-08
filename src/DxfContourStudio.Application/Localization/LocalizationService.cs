#nullable enable

using System.ComponentModel;

namespace DxfContourStudio.Application.Localization;

/// <summary>
/// Built-in <see cref="ILocalizer"/> implementation.
///
/// - zh-CN is the default and the complete, product-grade resource.
/// - en-US ships as an aligned skeleton (every key present, basic wording).
/// - Runtime switching is supported by <see cref="SetCulture"/> (no restart);
///   <see cref="CultureChanged"/> lets bindings / view models refresh.
/// - Missing keys fall back zh-CN → key itself so a mistake is never silent.
///
/// Thread-safety: reads are lock-free on immutable dictionaries; switching the
/// culture is intended for the UI thread.
/// </summary>
public sealed class LocalizationService : ILocalizer, INotifyPropertyChanged
{
    public const string ZhCnName = "zh-CN";
    public const string EnUsName = "en-US";

    private static readonly IReadOnlyDictionary<string, string> Zh = LocalizedStringsZhCn.All;
    private static readonly IReadOnlyDictionary<string, string> En = LocalizedStringsEn.All;

    private string _culture = ZhCnName;

    /// <inheritdoc />
    public string CurrentCulture => _culture;

    /// <inheritdoc />
    public event Action? CultureChanged;

    /// <summary>
    /// Raised whenever the active culture changes so that WPF bindings against
    /// the indexer refresh themselves. Raised with an empty property name
    /// (= "all properties changed").
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the single application-wide service instance.</summary>
    public static LocalizationService Instance { get; } = new();

    /// <summary>Whether the given culture name is provided by this service.</summary>
    public static bool IsKnownCulture(string name) =>
        string.Equals(name, ZhCnName, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, EnUsName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Every key the service knows (its primary record).</summary>
    public static IReadOnlyCollection<string> KnownKeys => Zh.Keys.ToList();

    /// <summary>Every culture the service can switch to, in UI order.</summary>
    public static IReadOnlyList<string> KnownCultures => [ZhCnName, EnUsName];

    private LocalizationService()
    {
    }

    /// <summary>Switches the active culture at runtime and notifies listeners.</summary>
    public void SetCulture(string cultureName)
    {
        string next = string.Equals(cultureName, ZhCnName, StringComparison.OrdinalIgnoreCase) ? ZhCnName : EnUsName;
        if (string.Equals(_culture, next, StringComparison.Ordinal))
        {
            return;
        }

        _culture = next;
        CultureChanged?.Invoke();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    /// <summary>Indexer used by WPF bindings: <c>{Binding Item[key]}</c> refreshes on culture change.</summary>
    public string this[string key] => Get(key);

    /// <inheritdoc />
    public string Get(string key) => Lookup(key);

    /// <inheritdoc />
    public string Get(string key, params object?[] args)
    {
        string template = Lookup(key);
        return args is { Length: > 0 } ? string.Format(template, args) : template;
    }

    /// <inheritdoc />
    public bool HasKey(string key) => Zh.ContainsKey(key) || En.ContainsKey(key);

    /// <summary>Resolves a key using fallback order: current → zh-CN → key itself.</summary>
    private string Lookup(string key)
    {
        var table = string.Equals(_culture, ZhCnName, StringComparison.Ordinal) ? Zh : En;
        return table.TryGetValue(key, out string? value)
            ? value
            : Zh.TryGetValue(key, out string? zh)
                ? zh
                : key;
    }
}