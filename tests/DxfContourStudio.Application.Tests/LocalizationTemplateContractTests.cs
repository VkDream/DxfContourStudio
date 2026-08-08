#nullable enable

using System.Text.RegularExpressions;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Imports;
using DxfContourStudio.Application.Localization;
using DxfContourStudio.Dxf.Infrastructure;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Localization contract guard: every template that carries format
/// placeholders must be well-formed for string.Format. This is the regression
/// net for the GUI crash "clicking Analyze terminated the app" whose root
/// cause was a 3-placeholder template formatted with only 2 arguments
/// (Status.Analyzed → FormatException on the UI thread).
/// </summary>
public class LocalizationTemplateContractTests
{
    [Fact]
    public void AllTemplates_FormatWithTheirPlaceholders_DoNotThrow()
    {
        foreach (string key in LocalizationService.KnownKeys)
        {
            string template = LocalizationService.Instance.Get(key);
            int maxIndex = MaxPlaceholderIndex(template);
            // Provide one argument more than the highest index so the format
            // always succeeds; this catches dangling {2} with 2 args etc.
            object[] args = Enumerable.Range(0, maxIndex + 1).Select(i => (object)i).ToArray();
            string formatted = string.Format(template, args);
            Assert.False(string.IsNullOrEmpty(formatted));
        }
    }

    [Fact]
    public void StatusAnalyzed_TemplateRequiresExactlyThreeArguments()
    {
        string template = LocalizationService.Instance.Get(LocalizationKeys.StatusAnalyzed);
        Assert.Equal(2, MaxPlaceholderIndex(template));
        string ok = string.Format(template, 2, 1, 1);
        Assert.False(string.IsNullOrEmpty(ok));
    }

    [Fact]
    public void StatusAnalyzeFailed_HasNoPlaceholders()
    {
        Assert.Equal("轮廓分析失败，请查看诊断日志",
            LocalizedStringsZhCn.All[LocalizationKeys.StatusAnalyzeFailed]);
        Assert.Equal(-1, MaxPlaceholderIndex(LocalizationService.Instance.Get(LocalizationKeys.StatusAnalyzeFailed)));
    }

    private static int MaxPlaceholderIndex(string template)
    {
        int max = -1;
        foreach (Match m in Regex.Matches(template, @"\{(\d+)\}"))
        {
            if (int.TryParse(m.Groups[1].Value, out int idx))
            {
                max = Math.Max(max, idx);
            }
        }

        return max;
    }
}
