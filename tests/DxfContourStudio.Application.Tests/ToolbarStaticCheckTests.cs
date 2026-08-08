#nullable enable

using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Static regression guard for the GUI smell "the toolbar rendered raw XAML
/// source as visible text": a stray raw text node inside the ToolBar. The file
/// is copied next to the test assembly by the test csproj.
/// </summary>
public class ToolbarStaticCheckTests
{
    private static string MainWindowXaml =>
        Path.Combine(AppContext.BaseDirectory, "src", "MainWindow.xaml");

    private static readonly XNamespace Wpf =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Fact]
    public void Toolbar_HasNoRawTextNodes()
    {
        Assert.True(File.Exists(MainWindowXaml), "MainWindow.xaml not copied to the test output");
        XDocument doc = XDocument.Load(MainWindowXaml);

        XElement? toolbar = doc
            .Descendants()
            .FirstOrDefault(e => e.Name == Wpf + "ToolBar");
        Assert.NotNull(toolbar);

        // A valid toolbar only ever contains element children (Button /
        // Separator). Any direct text content — which previously leaked raw
        // XAML attribute lines onto the rendered bar — is a regression.
        foreach (XNode node in toolbar.Nodes())
        {
            if (node is XText text && !string.IsNullOrWhiteSpace(text.Value))
            {
                Assert.Fail($"ToolBar contains raw text node: '{text.Value.Trim()}'");
            }
        }
    }

    [Fact]
    public void Toolbar_ExposesTheExpectedButtons()
    {
        XDocument doc = XDocument.Load(MainWindowXaml);
        XElement? toolbar = doc
            .Descendants()
            .FirstOrDefault(e => e.Name == Wpf + "ToolBar");
        Assert.NotNull(toolbar);

        int buttons = toolbar.Elements(Wpf + "Button").Count();
        int separators = toolbar.Elements(Wpf + "Separator").Count();
        Assert.True(buttons >= 9, $"expected >=9 toolbar buttons, found {buttons}");
        Assert.True(separators >= 4, $"expected >=4 separators, found {separators}");
    }

    [Fact]
    public void Toolbar_AutomationIdsForKeyCommandsPresent()
    {
        string content = File.ReadAllText(MainWindowXaml);
        foreach (string id in new[] { "Toolbar.Open", "Toolbar.Undo", "Toolbar.Redo", "Toolbar.Analyze", "Toolbar.RepairGap" })
        {
            Assert.Contains($"AutomationId=\"{id}\"", content, StringComparison.Ordinal);
        }
    }
}