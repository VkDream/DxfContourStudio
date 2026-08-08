#nullable enable

using System.IO;
using System.Text.RegularExpressions;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Guards the localization rule at the source level: user-visible strings in
/// the WPF layer must go through the localizer (XAML: {loc:Loc ...} or
/// bindings; ViewModel: localizer keys). This test scans the WPF source files
/// on disk and fails when a hard-coded UI string is found.
///
/// It deliberately does not flag:
/// - AutomationId / x:Name / binding paths / markup names (identifiers),
/// - DXF entity codes or numeric literals,
/// - enum names, command parameters like "zh-CN",
/// - XML comments.
/// </summary>
public class HardcodedStringScanTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DxfContourStudio.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir.FullName;
    }

    private static string WpfSourcePath() => Path.Combine(RepoRoot(), "src", "DxfContourStudio.Wpf");

    [Fact]
    public void Xaml_NoHardcodedUiTextAttributes()
    {
        // The suspicious attributes are exactly the ones that carry UI text.
        // Value must be empty, a binding/markup expression (starts with '{'),
        // or a pure identifier-ish token (e.g. nothing in our XAML).
        // InputGestureText is deliberately excluded: it shows a keyboard hint,
        // not translatable text (the lookbehind prevents matching the "Text"
        // inside "InputGestureText").
        var re = new Regex(
            @"(?<!\w)(?<attr>(?:Content|Header|Text|ToolTip|Title))\s*=\s*""(?<value>[^""]*?)""",
            RegexOptions.Compiled);

        foreach (string file in Directory.EnumerateFiles(WpfSourcePath(), "*.xaml", SearchOption.AllDirectories))
        {
            if (file.Contains(@"\obj\"))
            {
                continue;
            }

            string content = StripXmlComments(File.ReadAllText(file));
            foreach (Match m in re.Matches(content))
            {
                string value = m.Groups["value"].Value.Trim();
                if (value.Length == 0)
                {
                    continue;
                }

                bool isExpression = value.StartsWith("{") && value.EndsWith("}");
                bool isBinding = value.Contains("{Binding") || value.Contains("{loc:Loc");
                Assert.True(
                    isExpression || isBinding,
                    $"{file}: hard-coded UI text on {m.Groups["attr"].Value}='{value}'. " +
                    "Use {loc:Loc Key=...} or a binding instead.");
            }
        }
    }

    [Fact]
    public void ViewModels_NoChineseStringLiterals()
    {
        // Chinese in string literals is the most obvious localization leak.
        // Comments are excluded (they legitimately use Chinese).
        var re = new Regex(@"""[^""]*[\u4e00-\u9fff][^""]*""", RegexOptions.Compiled);

        foreach (string file in Directory.EnumerateFiles(
                     Path.Combine(WpfSourcePath(), "ViewModels"), "*.cs", SearchOption.TopDirectoryOnly))
        {
            string content = StripCsComments(File.ReadAllText(file));
            foreach (Match m in re.Matches(content))
            {
                Assert.Fail($"{file}: Chinese string literal '{m.Value}'. " +
                            "Move the text into LocalizedStrings and resolve it via the localizer.");
            }
        }
    }

    private static string StripXmlComments(string xaml)
    {
        // Remove <!-- ... --> blocks (non-greedy, multiline-safe).
        return Regex.Replace(xaml, @"<!--.*?-->", "", RegexOptions.Singleline);
    }

    private static string StripCsComments(string code)
    {
        code = Regex.Replace(code, @"/\*.*?\*/", "", RegexOptions.Singleline);
        code = Regex.Replace(code, @"//.*$", "", RegexOptions.Multiline);
        return code;
    }
}
