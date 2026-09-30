using Avalonia.Controls;
using Avalonia.Interactivity;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class SavePresetAsWindow : Window
{
    public string PresetName { get; private set; } = "";

    public string PresetVersion { get; private set; } = "1.0.0";

    public string? PresetAuthor { get; private set; }

    public string? PresetDescription { get; private set; }

    public bool PresetLocked { get; private set; }

    public bool Confirmed { get; private set; }

    public SavePresetAsWindow() : this(null)
    {
    }

    public SavePresetAsWindow(string? initialName)
    {
        InitializeComponent();
        Title = UiText.Main.PresetSaveAsTitle;
        NameLabel.Text = UiText.Main.PresetSaveAsNameLabel;
        VersionLabel.Text = UiText.Main.PresetSaveAsVersionLabel;
        AuthorLabel.Text = UiText.Main.PresetSaveAsAuthorLabel;
        DescriptionLabel.Text = UiText.Main.PresetSaveAsDescriptionLabel;
        LockedCheckBox.Content = UiText.Main.PresetSaveAsLockedLabel;
        ToolTip.SetTip(LockedCheckBox, UiText.Main.PresetSaveAsLockedHint);
        CancelButton.Content = UiText.Main.PresetSaveAsCancel;
        SaveButton.Content = UiText.Main.PresetSaveAsSave;
        if (!string.IsNullOrWhiteSpace(initialName))
        {
            NameBox.Text = initialName;
        }

        Opened += (_, _) => NameBox.Focus();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    private async void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        var name = NameBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name))
        {
            await AppDialogs.ShowError(this, UiText.Main.PresetSaveAsNameRequired, UiText.Main.PresetSaveAsTitle);
            return;
        }

        PresetName = name;
        PresetVersion = string.IsNullOrWhiteSpace(VersionBox.Text) ? "1.0.0" : VersionBox.Text.Trim();
        PresetAuthor = string.IsNullOrWhiteSpace(AuthorBox.Text) ? null : AuthorBox.Text.Trim();
        PresetDescription = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim();
        PresetLocked = LockedCheckBox.IsChecked == true;
        Confirmed = true;
        Close();
    }
}
