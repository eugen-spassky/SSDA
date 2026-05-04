using System.Windows.Controls;
using SSDA.App.Converters;

namespace SSDA.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        // Hand the inherited "InverseBool" converter through resources lookup.
        if (Resources["InverseBool"] is null)
            Resources["InverseBool"] = new InverseBooleanConverter();
    }
}
