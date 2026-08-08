#nullable enable

using System.Windows.Markup;
using DxfContourStudio.Application.Localization;

namespace DxfContourStudio.Wpf.Localization;

/// <summary>
/// XAML markup extension that resolves a localization key against the
/// application-wide <see cref="LocalizationService"/>.
///
/// Usage:  <c>{loc:Loc Key=Menu.File.Open}</c>  or  <c>{loc:Loc Menu.File.Open}</c>
///
/// IMPORTANT: the value is resolved at XAML load time, not via a live
/// binding. A custom markup extension cannot return a raw <see cref="Binding"/>
/// (or <c>BindingExpression</c>): the XAML runtime hands the returned object
/// to <c>DependencyObject.SetValue</c>, which rejects binding objects for
/// string-typed dependency properties such as <c>Text</c> / <c>Title</c> /
/// <c>Header</c> (compiled <c>{Binding}</c> syntax is special-cased, custom
/// extensions are not). Per the localization round spec, switching the
/// language persists the choice and applies on the next launch
/// ("restart-applies"). View-model driven strings (status bar, property rows,
/// import report) do refresh live through <see cref="LocalizationService.CultureChanged"/>.
/// </summary>
public sealed class LocExtension : MarkupExtension
{
    /// <summary>The localization key (dot-separated, e.g. "Menu.File.Open").</summary>
    public string Key { get; set; } = "";

    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
        {
            return "";
        }

        return LocalizationService.Instance.Get(Key);
    }
}
