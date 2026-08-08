#nullable enable

using System.IO;
using System.Text.Json;
using DxfContourStudio.Application.Localization;

namespace DxfContourStudio.Wpf.Localization;

/// <summary>
/// Persists user settings (currently: the UI language) to
/// %AppData%\DxfContourStudio\settings.json. Loaded once at startup; saved
/// whenever the user switches the language.
/// </summary>
public static class AppSettings
{
    private const string FileName = "settings.json";

    private static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DxfContourStudio");

    private static string SettingsPath => Path.Combine(SettingsDirectory, FileName);

    /// <summary>
    /// Loads the saved culture name, or the default (zh-CN) when no settings
    /// exist or the value is unknown.
    /// </summary>
    public static string LoadCulture()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return LocalizationService.ZhCnName;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath));
            string? culture = doc.RootElement.GetProperty("culture").GetString();
            return culture is not null && LocalizationService.IsKnownCulture(culture)
                ? culture
                : LocalizationService.ZhCnName;
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or UnauthorizedAccessException)
        {
            // Corrupt/absent settings must never block startup; fall back to the default.
            return LocalizationService.ZhCnName;
        }
    }

    /// <summary>Persists the current culture for the next launch.</summary>
    public static void SaveCulture(string culture)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new { culture }, options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings persistence is best-effort; the app keeps running with
            // the in-memory culture.
        }
    }
}
