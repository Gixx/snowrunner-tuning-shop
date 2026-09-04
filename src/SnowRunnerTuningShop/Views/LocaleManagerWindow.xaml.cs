using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using SnowRunnerTuningShop.Core.Localization;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class LocaleManagerWindow : Window
{
    private readonly ObservableCollection<LocalePackRow> _packs = [];
    private bool _busy;

    public LocaleManagerWindow()
    {
        InitializeComponent();
        PackList.ItemsSource = _packs;
        Loaded += LocaleManagerWindow_Loaded;
    }

    public bool LanguagesChanged { get; private set; }

    private async void LocaleManagerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= LocaleManagerWindow_Loaded;
        await RefreshAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private async void ApplyButton_Click(object sender, RoutedEventArgs e) =>
        await ApplyAsync();

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
            MessageBox.Show(
                UiText.LocalePack.NothingSelected,
                UiText.LocalePack.Title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
                MessageBox.Show(
                    UiText.Settings.LanguageRestartMessage,
                    UiText.Settings.LanguageRestartTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    UiText.LocalePack.ApplySuccess,
                    UiText.LocalePack.Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            LanguageCatalog.Reload();
            StringResources.Reload();
            LanguageService.RefreshRuntimeStrings();
            MessageBox.Show(ex.Message, UiText.LocalePack.Title, MessageBoxButton.OK, MessageBoxImage.Error);
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
