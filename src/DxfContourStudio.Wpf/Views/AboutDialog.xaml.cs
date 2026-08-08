#nullable enable

using System.Windows;

namespace DxfContourStudio.Wpf.Views;

/// <summary>
/// About dialog: product name, version (read from assembly metadata), tech
/// stack, license and repository status. Code-behind only handles the
/// default Close behavior; all content comes from the data context.
/// </summary>
public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
    }
}
