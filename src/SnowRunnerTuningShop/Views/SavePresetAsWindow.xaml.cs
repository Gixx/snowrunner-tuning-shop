using System.Windows;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class SavePresetAsWindow : Window
{
    public string PresetName { get; private set; } = "";

    public string PresetVersion { get; private set; } = "1.0.0";

    public string? PresetAuthor { get; private set; }

    public string? PresetDescription { get; private set; }

    public bool PresetLocked { get; private set; }

    public SavePresetAsWindow(string? initialName = null)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(initialName))
        {
            NameBox.Text = initialName;
        }

        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(
                UiText.Main.PresetSaveAsNameRequired,
                UiText.Main.PresetSaveAsTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        PresetName = name;
        PresetVersion = string.IsNullOrWhiteSpace(VersionBox.Text) ? "1.0.0" : VersionBox.Text.Trim();
        PresetAuthor = string.IsNullOrWhiteSpace(AuthorBox.Text) ? null : AuthorBox.Text.Trim();
        PresetDescription = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim();
        PresetLocked = LockedCheckBox.IsChecked == true;
        DialogResult = true;
        Close();
    }
}
