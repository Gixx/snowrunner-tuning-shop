using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Tires;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Core.Xml;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class TireTuningView : UserControl
{
    private readonly ObservableCollection<TireRowViewModel> _tires = [];
    private readonly ICollectionView _tiresView;
    private bool _pakWritesAllowed = true;

    public TireTuningView()
    {
        InitializeComponent();
        _tiresView = CollectionViewSource.GetDefaultView(_tires);
        _tiresView.Filter = MatchesFilter;
        TiresGrid.ItemsSource = _tiresView;
        ResetMultiplierSlidersToBaseline();
    }

    public string? PakPath { get; private set; }

    public void SetPakWritesAllowed(bool allowed)
    {
        _pakWritesAllowed = allowed;
        RefreshRestoreButton();
    }

    public void LoadFromPak(string pakPath)
    {
        PakPath = pakPath;
        RefreshRestoreButton();
        ReloadTires();
    }

    public async Task LoadFromPakAsync(string pakPath)
    {
        PakPath = pakPath;
        RefreshRestoreButton();
        await ReloadTiresAsync();
    }

    public void Clear()
    {
        PakPath = null;
        _tires.Clear();
        ApplyMultipliersButton.IsEnabled = false;
        SaveIndividualButton.IsEnabled = false;
        RestoreTiresButton.IsEnabled = false;
    }

    public void RefreshRestoreButton()
    {
        var hasPak = !string.IsNullOrWhiteSpace(PakPath);
        ApplyMultipliersButton.IsEnabled = hasPak && _pakWritesAllowed;
        SaveIndividualButton.IsEnabled = hasPak && _pakWritesAllowed;
        RestoreTiresButton.IsEnabled = hasPak
            && _pakWritesAllowed
            && PakBaselineService.HasBaseline(PakPath!);
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRestoreButton();
        ReloadTires();
    }

    private void RestoreTiresButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryProceed(null) || !_pakWritesAllowed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(PakPath))
        {
            MessageBox.Show(UiText.Tires.LoadPakFirst, UiText.Tires.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!PakBaselineService.HasBaseline(PakPath))
        {
            MessageBox.Show(
                UiText.Main.BaselineMissingShort,
                UiText.Main.BaselineTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            var result = TireService.RestoreTiresFromBaseline(PakPath);
            ResetMultiplierSlidersToBaseline();
            GlobalIgnoreIceCheckBox.IsChecked = false;
            ReloadTires();
            MessageBox.Show(
                UiText.Tires.RestoreTiresMessage(
                    result.ChangedTires,
                    result.UpdatedFiles),
                UiText.Tires.RestoreTiresSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Tires.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyMultipliersButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryProceed(null) || !_pakWritesAllowed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(PakPath))
        {
            MessageBox.Show(UiText.Tires.LoadPakFirst, UiText.Tires.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!PakBaselineService.HasBaseline(PakPath))
        {
            MessageBox.Show(
                UiText.Main.BaselineMissingShort,
                UiText.Main.BaselineTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            var result = TireService.ApplyGlobalMultipliers(
                PakPath,
                GetMultiplier(OnRoadFrictionMultiplierSlider),
                GetMultiplier(OffRoadFrictionMultiplierSlider),
                GetMultiplier(MudFrictionMultiplierSlider),
                GlobalIgnoreIceCheckBox.IsChecked == true ? true : null);

            ReloadTires();
            GlobalIgnoreIceCheckBox.IsChecked = false;
            MessageBox.Show(
                UiText.Tires.MultipliersSavedMessage(
                    result.ChangedTires,
                    result.UpdatedFiles),
                UiText.Tires.SaveSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Tires.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveIndividualButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryProceed(null) || !_pakWritesAllowed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(PakPath))
        {
            MessageBox.Show(UiText.Tires.LoadPakFirst, UiText.Tires.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            TiresGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
            TiresGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

            var tires = _tires
                .SelectMany(row => row.ToDefinitions())
                .ToArray();

            var result = TireService.SaveTireChanges(PakPath, tires);
            ReloadTires();
            MessageBox.Show(
                UiText.Tires.IndividualSavedMessage(result.ChangedTires),
                UiText.Tires.SaveSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Tires.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ReloadTires()
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            Clear();
            return;
        }

        try
        {
            ApplyTires(TireService.LoadTires(PakPath, AppLanguage.Current));
        }
        catch (Exception ex)
        {
            Clear();
            MessageBox.Show(ex.Message, UiText.Tires.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ReloadTiresAsync()
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            Clear();
            return;
        }

        try
        {
            var path = PakPath;
            var language = AppLanguage.Current;
            var tires = await Task.Run(() => TireService.LoadTires(path, language));
            ApplyTires(tires);
        }
        catch (Exception ex)
        {
            Clear();
            MessageBox.Show(ex.Message, UiText.Tires.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyTires(IReadOnlyList<TireDefinition> tires)
    {
        _tires.Clear();
        foreach (var row in GroupTireRows(tires))
        {
            _tires.Add(row);
        }

        _tiresView.Refresh();
    }

    private static IEnumerable<TireRowViewModel> GroupTireRows(IReadOnlyList<TireDefinition> tires)
    {
        return tires
            .GroupBy(TireRowViewModel.CreateGroupKey)
            .Select(group =>
            {
                var instances = group
                    .Select(TireInstanceRef.FromDefinition)
                    .ToArray();
                var vehicleNames = group
                    .SelectMany(item => item.UsedByVehicles)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var first = group.First();
                var row = TireRowViewModel.FromDefinition(first);
                row.Instances = instances;
                row.UsedBy = PartXmlHelpers.FormatUsedBy(vehicleNames);
                row.UsedByTooltip = PartXmlHelpers.FormatUsedByTooltip(
                    vehicleNames,
                    UiText.Parts.NoTrucksWheelSet);
                return row;
            })
            .OrderBy(row => row.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase);
    }

    private void MultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        UpdateMultiplierLabels();

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        TuningListFilter.UpdatePlaceholderVisibility(FilterTextBox, FilterPlaceholder);
        _tiresView.Refresh();
    }

    private bool MatchesFilter(object item)
    {
        if (item is not TireRowViewModel row)
        {
            return false;
        }

        var extraFields = row.Instances
            .SelectMany(instance => new[] { instance.SetName, instance.SetId })
            .ToArray();

        var fields = new List<string?>
        {
            row.Category,
            row.DisplayName,
            row.Name,
            row.SetName,
            row.SetId,
            row.UsedBy,
            row.UsedByTooltip,
        };
        fields.AddRange(extraFields);

        return TuningListFilter.Matches(FilterTextBox.Text, fields.ToArray());
    }

    private void ResetMultiplierSlidersToBaseline()
    {
        OnRoadFrictionMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        OffRoadFrictionMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        MudFrictionMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        UpdateMultiplierLabels();
    }

    private void UpdateMultiplierLabels()
    {
        if (OnRoadFrictionMultiplierLabel is null
            || OffRoadFrictionMultiplierLabel is null
            || MudFrictionMultiplierLabel is null)
        {
            return;
        }

        OnRoadFrictionMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.OnRoad,
            GetMultiplierIndex(OnRoadFrictionMultiplierSlider));
        OffRoadFrictionMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.OffRoad,
            GetMultiplierIndex(OffRoadFrictionMultiplierSlider));
        MudFrictionMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.Mud,
            GetMultiplierIndex(MudFrictionMultiplierSlider));
    }

    private static int GetMultiplierIndex(Slider slider) =>
        TuningMultiplierPresets.ClampIndex((int)Math.Round(slider.Value));

    private static double GetMultiplier(Slider slider) =>
        TuningMultiplierPresets.GetValue(GetMultiplierIndex(slider));

    public sealed class TireRowViewModel : INotifyPropertyChanged
    {
        private double _onRoadFriction;
        private double _offRoadFriction;
        private double _mudFriction;
        private bool _ignoreIce;

        public required string EntryPath { get; init; }
        public required string Name { get; init; }
        public required string DisplayName { get; init; }
        public required string SourceFile { get; init; }
        public required string SetId { get; init; }
        public required string SetName { get; init; }
        public required string UsedBy { get; set; }
        public required string UsedByTooltip { get; set; }
        public required string Category { get; init; }
        public int Price { get; init; }
        public required string FrictionTemplate { get; init; }
        public IReadOnlyList<TireInstanceRef> Instances { get; set; } = [];

        public double OnRoadFriction
        {
            get => _onRoadFriction;
            set
            {
                if (Math.Abs(_onRoadFriction - value) < 0.0001)
                {
                    return;
                }

                _onRoadFriction = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OnRoadFriction)));
            }
        }

        public double OffRoadFriction
        {
            get => _offRoadFriction;
            set
            {
                if (Math.Abs(_offRoadFriction - value) < 0.0001)
                {
                    return;
                }

                _offRoadFriction = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OffRoadFriction)));
            }
        }

        public double MudFriction
        {
            get => _mudFriction;
            set
            {
                if (Math.Abs(_mudFriction - value) < 0.0001)
                {
                    return;
                }

                _mudFriction = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MudFriction)));
            }
        }

        public bool IgnoreIce
        {
            get => _ignoreIce;
            set
            {
                if (_ignoreIce == value)
                {
                    return;
                }

                _ignoreIce = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IgnoreIce)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public static TireRowViewModel FromDefinition(TireDefinition definition) =>
            new()
            {
                EntryPath = definition.EntryPath,
                Name = definition.Name,
                DisplayName = definition.DisplayName,
                SourceFile = definition.SourceFile,
                SetId = definition.SetId,
                SetName = definition.SetName,
                UsedBy = definition.UsedBy,
                UsedByTooltip = definition.UsedByTooltip,
                Category = definition.Category,
                Price = definition.Price,
                FrictionTemplate = definition.FrictionTemplate,
                OnRoadFriction = definition.OnRoadFriction,
                OffRoadFriction = definition.OffRoadFriction,
                MudFriction = definition.MudFriction,
                IgnoreIce = definition.IgnoreIce,
                Instances = [TireInstanceRef.FromDefinition(definition)],
            };

        internal static TireGroupKey CreateGroupKey(TireDefinition definition) =>
            new(
                definition.Category,
                definition.DisplayName,
                definition.Price,
                definition.OnRoadFriction,
                definition.OffRoadFriction,
                definition.MudFriction,
                definition.IgnoreIce);

        public IEnumerable<TireDefinition> ToDefinitions()
        {
            var targets = Instances.Count > 0
                ? Instances
                : [new TireInstanceRef(EntryPath, Name, SetId, SetName, SourceFile, FrictionTemplate)];

            foreach (var instance in targets)
            {
                yield return new TireDefinition
                {
                    EntryPath = instance.EntryPath,
                    Name = instance.Name,
                    DisplayName = DisplayName,
                    SourceFile = instance.SourceFile,
                    SetId = instance.SetId,
                    SetName = instance.SetName,
                    UsedBy = UsedBy,
                    UsedByTooltip = UsedByTooltip,
                    Category = Category,
                    Price = Price,
                    FrictionTemplate = instance.FrictionTemplate,
                    OnRoadFriction = OnRoadFriction,
                    OffRoadFriction = OffRoadFriction,
                    MudFriction = MudFriction,
                    IgnoreIce = IgnoreIce,
                };
            }
        }
    }

    public readonly record struct TireGroupKey(
        string Category,
        string DisplayName,
        int Price,
        double OnRoadFriction,
        double OffRoadFriction,
        double MudFriction,
        bool IgnoreIce);

    public readonly record struct TireInstanceRef(
        string EntryPath,
        string Name,
        string SetId,
        string SetName,
        string SourceFile,
        string FrictionTemplate)
    {
        public static TireInstanceRef FromDefinition(TireDefinition definition) =>
            new(
                definition.EntryPath,
                definition.Name,
                definition.SetId,
                definition.SetName,
                definition.SourceFile,
                definition.FrictionTemplate);
    }
}
