using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core.Localization;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class LocaleManagerWindow : Window
{
    private readonly ObservableCollection<LocalePackRow> _packs = [];
    private bool _busy;

    public LocaleManagerWindow()
    {
        InitializeComponent();
        Title = UiText.LocalePack.Title;
        TitleText.Text = UiText.LocalePack.Title;
        HintText.Text = UiText.LocalePack.Hint;
        RefreshButton.Content = UiText.LocalePack.Refresh;
        ApplyButton.Content = UiText.LocalePack.Apply;
        CloseButton.Content = UiText.LocalePack.Close;
        ColumnAddUpdate.Text = UiText.LocalePack.ColumnAddUpdate;
        ColumnRemove.Text = UiText.LocalePack.ColumnRemove;
        ColumnLanguage.Text = UiText.LocalePack.ColumnLanguage;
        ColumnStatus.Text = UiText.LocalePack.ColumnStatus;
        ColumnRevision.Text = UiText.LocalePack.ColumnRevision;
        PackList.ItemsSource = _packs;
        Opened += async (_, _) => await RefreshAsync();
    }

    public bool LanguagesChanged { get; private set; }

    private async void RefreshButton_Click(object? sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private async void ApplyButton_Click(object? sender, RoutedEventArgs e) =>
        await ApplyAsync();

    private void CloseButton_Click(object? sender, RoutedEventArgs e) =>
        Close();

    private async Task RefreshAsync()
    {
        if (_busy)
        {
            return;
        }

        SetBusy(true);
        StatusText.Text = UiText.LocalePack.Checking;
        try
        {
            var result = await LocalePackUpdateService.CheckAsync();
            ReplaceRows(result.Packs);
            StatusText.Text = result.Ok
                ? UiText.LocalePack.Ready(result.Packs)
                : UiText.LocalePack.CheckFailed(result.ErrorMessage);
        }
        catch (Exception ex)
        {
            ReplaceRows(LocalePackStore.BuildSnapshots(null));
            StatusText.Text = UiText.LocalePack.CheckFailed(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ApplyAsync()
    {
        if (_busy)
        {
            return;
        }

        var toRemove = _packs.Where(row => row.RemoveSelected && row.CanRemove).ToArray();
        var toInstall = _packs.Where(row => row.Wanted && row.CanAddOrUpdate).ToArray();
        if (toRemove.Length == 0 && toInstall.Length == 0)
        {
            await AppDialogs.ShowInfo(this, UiText.LocalePack.NothingSelected, UiText.LocalePack.Title);
            return;
        }

        var current = LanguageCatalog.NormalizeUiCulture(LanguageService.CurrentUiCulture);
        var removedCurrent = false;
        var updatedCurrent = false;

        SetBusy(true);
        try
        {
            foreach (var row in toRemove)
            {
                LocalePackUpdateService.Remove(row.UiCulture);
                LanguagesChanged = true;
                if (string.Equals(row.UiCulture, current, StringComparison.OrdinalIgnoreCase))
                {
                    removedCurrent = true;
                }
            }

            foreach (var row in toInstall)
            {
                await LocalePackUpdateService.InstallAsync(row.UiCulture);
                LanguagesChanged = true;
                if (string.Equals(row.UiCulture, current, StringComparison.OrdinalIgnoreCase))
                {
                    updatedCurrent = true;
                }
            }

            LanguageCatalog.Reload();
            StringResources.Reload();
            LanguageService.RefreshRuntimeStrings();
            if (removedCurrent)
            {
                LanguageService.ApplyAndSave(LanguageCatalog.DefaultUiCulture);
            }

            var result = await LocalePackUpdateService.CheckAsync();
            ReplaceRows(result.Packs);
            StatusText.Text = result.Ok
                ? UiText.LocalePack.Ready(result.Packs)
                : UiText.LocalePack.CheckFailed(result.ErrorMessage);

            if (removedCurrent || updatedCurrent)
            {
                await AppDialogs.ShowInfo(
                    this,
                    UiText.Settings.LanguageRestartMessage,
                    UiText.Settings.LanguageRestartTitle);
            }
            else
            {
                await AppDialogs.ShowInfo(this, UiText.LocalePack.ApplySuccess, UiText.LocalePack.Title);
            }
        }
        catch (Exception ex)
        {
            LanguageCatalog.Reload();
            StringResources.Reload();
            LanguageService.RefreshRuntimeStrings();
            await AppDialogs.ShowError(this, ex.Message, UiText.LocalePack.Title);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ReplaceRows(IReadOnlyList<LocalePackSnapshot> packs)
    {
        _packs.Clear();
        foreach (var pack in packs)
        {
            _packs.Add(new LocalePackRow(pack));
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        RefreshButton.IsEnabled = !busy;
        ApplyButton.IsEnabled = !busy;
        PackListHost.IsEnabled = !busy;
    }
}

internal sealed class LocalePackRow : INotifyPropertyChanged
{
    private bool _wanted;
    private bool _removeSelected;

    public LocalePackRow(LocalePackSnapshot snapshot)
    {
        Snapshot = snapshot;
        DisplayName = snapshot.Option.DisplayName;
        UiCulture = snapshot.Option.UiCulture;
        CanAddOrUpdate = snapshot.CanAdd || snapshot.CanUpdate;
        CanRemove = snapshot.CanRemove;
        Status = UiText.LocalePack.Status(snapshot);
        RevisionText = UiText.LocalePack.Revision(snapshot);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public LocalePackSnapshot Snapshot { get; }

    public string DisplayName { get; }

    public string UiCulture { get; }

    public string Status { get; }

    public string RevisionText { get; }

    public bool CanAddOrUpdate { get; }

    public bool CanRemove { get; }

    public bool Wanted
    {
        get => _wanted;
        set
        {
            if (_wanted == value)
            {
                return;
            }

            _wanted = value;
            if (_wanted)
            {
                RemoveSelected = false;
            }

            OnPropertyChanged();
        }
    }

    public bool RemoveSelected
    {
        get => _removeSelected;
        set
        {
            if (_removeSelected == value)
            {
                return;
            }

            _removeSelected = value;
            if (_removeSelected)
            {
                Wanted = false;
            }

            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
