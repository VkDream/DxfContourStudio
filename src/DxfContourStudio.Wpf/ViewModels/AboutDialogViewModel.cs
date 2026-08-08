#nullable enable

using System.Reflection;
using DxfContourStudio.Application.Localization;

namespace DxfContourStudio.Wpf.ViewModels;

/// <summary>
/// Read-only data shown by the About dialog. Version is taken from the
/// assembly's informational version (single source ADR-006), everything else
/// flows through the localizer — no hard-coded UI text.
/// </summary>
public sealed class AboutDialogViewModel
{
    /// <summary>Displayed product name (localized).</summary>
    public string AppName { get; } = LocalizationService.Instance.Get(LocalizationKeys.AppName);

    /// <summary>Version line, e.g. "版本 0.2.0".</summary>
    public string Version { get; } = BuildVersionText();

    /// <summary>Technology stack value, e.g. ".NET 10 / WPF / ACadSharp".</summary>
    public string TechnologyValue { get; } = LocalizationService.Instance.Get(LocalizationKeys.AboutTechStack);

    /// <summary>License name value, e.g. "MIT".</summary>
    public string LicenseValue { get; } = LocalizationService.Instance.Get(LocalizationKeys.AboutLicenseName);

    /// <summary>GitHub repository status (not published yet).</summary>
    public string GitHubStatus { get; } = LocalizationService.Instance.Get(LocalizationKeys.AboutGitHubNotPublished);

    private static string BuildVersionText()
    {
        string version = typeof(AboutDialogViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        // InformationalVersion may carry build metadata; show the semantic part.
        string semantic = version.Split('+')[0];
        return $"{LocalizationService.Instance.Get(LocalizationKeys.AboutVersion)} {semantic}";
    }
}