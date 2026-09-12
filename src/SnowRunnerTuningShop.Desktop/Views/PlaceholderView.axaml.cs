using Avalonia.Controls;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class PlaceholderView : UserControl
{
    public PlaceholderView()
    {
        InitializeComponent();
        MessageText.Text = UiText.Parts.ComingSoon;
    }

    public void Set(string title)
    {
        TitleText.Text = title;
    }
}
