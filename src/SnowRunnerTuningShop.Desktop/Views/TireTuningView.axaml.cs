using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Tires;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Core.Xml;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class TireTuningView : UserControl
{
    private readonly ObservableCollection<TireRowViewModel> _tires = [];
    private bool _pakWritesAllowed = true;
    private AppSession? _session;

    public TireTuningView()
    {
        InitializeComponent();
        ApplyStaticText();
        RefreshFilter();
        ResetMultiplierSlidersToBaseline();
    }

    public event EventHandler<string>? StatusChanged;

    public string? PakPath { get; private set; }

    private Window? OwnerWindow => TopLevel.GetTopLevel(this) as Window;

    public void AttachSession(AppSession session) => _session = session;

    public void SetPakWritesAllowed(bool allowed)
    {
        _pakWritesAllowed = allowed;
        RefreshRestoreButton();
    }

    public async Task LoadFromPakAsync(string pakPath, CancellationToken cancellationToken = default)
    {
        PakPath = pakPath;
        RefreshRestoreButton();
        await ReloadTiresAsync(cancellationToken);
    }

    public void Clear()
    {
        PakPath = null;
        _tires.Clear();
        RefreshFilter();
        PartsTuningUiHelpers.ClearWriteButtons(ApplyMultipliersButton, SaveIndividualButton, RestoreTiresButton);
    }

    public void RefreshRestoreButton() =>
        PartsTuningUiHelpers.SetPartWriteButtonStates(
            _session,
            PakPath,
            _pakWritesAllowed,
            ApplyMultipliersButton,
            SaveIndividualButton,
            RestoreTiresButton);

    private void ApplyStaticText()
    {
        MultipliersExpander.Header = UiText.Tires.GlobalMultipliersTitle;
        GlobalIgnoreIceCheckBox.Content = UiText.Tires.IgnoreIceAll;
        ApplyMultipliersButton.Content = UiText.Tires.Apply;
        SaveIndividualButton.Content = UiText.Tires.SaveIndividualChanges;
        RestoreTiresButton.Content = UiText.Tires.RestoreTiresToBaseline;
        ReloadButton.Content = UiText.Tires.RefreshList;
        FilterTextBox.PlaceholderText = UiText.Tires.FilterPlaceholder;
        PartsTuningUiHelpers.SetColumnHeaders(
            TiresGrid,
            UiText.Tires.CategoryColumn,
            UiText.Tires.NameColumn,
            UiText.Tires.UsedByColumn,
            UiText.Tires.PriceColumn,
            UiText.Tires.OnRoadFrictionColumn,
            UiText.Tires.OffRoadFrictionColumn,
            UiText.Tires.MudFrictionColumn,
            UiText.Tires.IgnoreIceColumn);
    }

    private void ReloadButton_Click(object? sender, RoutedEventArgs e)
    {
        RefreshRestoreButton();
        ReloadTires();
    }

    private async void RestoreTiresButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryBeginWrite(owner, _session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => ReportStatus(UiText.Tires.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreTiresButton))
        {
            try
            {
                var path = PakPath!;
                var result = await Task.Run(() => TireService.RestoreTiresFromBaseline(path));
                ResetMultiplierSlidersToBaseline();
                GlobalIgnoreIceCheckBox.IsChecked = false;
                ReloadTires();
                ReportStatus(UiText.Tires.MultipliersAppliedStatus(
                    result.ChangedTires,
                    result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Tires.SaveErrorTitle);
            }
        }
    }

    private async void ApplyMultipliersButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryBeginWrite(owner, _session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => ReportStatus(UiText.Tires.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreTiresButton))
        {
            try
            {
                var path = PakPath!;
                var onRoad = GetMultiplier(OnRoadFrictionMultiplierSlider);
                var offRoad = GetMultiplier(OffRoadFrictionMultiplierSlider);
                var mud = GetMultiplier(MudFrictionMultiplierSlider);
                var ignoreIce = GlobalIgnoreIceCheckBox.IsChecked == true ? true : (bool?)null;
                var result = await Task.Run(() => TireService.ApplyGlobalMultipliers(
                    path, onRoad, offRoad, mud, ignoreIce));

                ReloadTires();
                GlobalIgnoreIceCheckBox.IsChecked = false;
                ReportStatus(UiText.Tires.MultipliersAppliedStatus(
                    result.ChangedTires,
                    result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Tires.SaveErrorTitle);
            }
        }
    }

    private async void SaveIndividualButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryBeginWrite(owner, _session, PakPath, _pakWritesAllowed, requireBaseline: false,
                () => ReportStatus(UiText.Tires.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreTiresButton))
        {
            try
            {
                PartsTuningUiHelpers.CommitGridEdits(TiresGrid);

                var tires = _tires.SelectMany(row => row.ToDefinitions()).ToArray();
                var path = PakPath!;
                var result = await Task.Run(() => TireService.SaveTireChanges(path, tires));
                ReloadTires();
                ReportStatus(UiText.Tires.IndividualSavedStatus(result.ChangedTires, result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Tires.SaveErrorTitle);
            }
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
            if (OwnerWindow is { } owner)
            {
                _ = AppDialogs.ShowError(owner, ex.Message, UiText.Tires.LoadErrorTitle);
            }
        }
    }

    private async Task ReloadTiresAsync(CancellationToken cancellationToken = default)
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
            var tires = await Task.Run(() => TireService.LoadTires(path, language), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ApplyTires(tires);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Clear();
            if (OwnerWindow is { } owner)
            {
                await AppDialogs.ShowError(owner, ex.Message, UiText.Tires.LoadErrorTitle);
            }
        }
    }

    private void ApplyTires(IReadOnlyList<TireDefinition> tires)
    {
        _tires.Clear();
        foreach (var row in GroupTireRows(tires))
        {
            _tires.Add(row);
        }

        RefreshFilter();
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

    private void MultiplierSlider_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != RangeBase.ValueProperty)
        {
            return;
        }

        UpdateMultiplierLabels();
    }

    private void FilterTextBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        RefreshFilter();

    private void RefreshFilter()
    {
        TuningListFilter.ApplyFilter(
            TiresGrid,
            _tires,
            row =>
            {
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
            });
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
            UiText.Slider.OnRoad, GetMultiplierIndex(OnRoadFrictionMultiplierSlider));
        OffRoadFrictionMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.OffRoad, GetMultiplierIndex(OffRoadFrictionMultiplierSlider));
        MudFrictionMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.Mud, GetMultiplierIndex(MudFrictionMultiplierSlider));
    }

    private static int GetMultiplierIndex(Slider slider) =>
        TuningMultiplierPresets.ClampIndex((int)Math.Round(slider.Value));

    private static double GetMultiplier(Slider slider) =>
        TuningMultiplierPresets.GetValue(GetMultiplierIndex(slider));

    private void ReportStatus(string message) =>
        StatusChanged?.Invoke(this, message);

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
                if (Math.Abs(_onRoadFriction - value) < 0.0001) return;
                _onRoadFriction = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OnRoadFriction)));
            }
        }

        public double OffRoadFriction
        {
            get => _offRoadFriction;
            set
            {
                if (Math.Abs(_offRoadFriction - value) < 0.0001) return;
                _offRoadFriction = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OffRoadFriction)));
            }
        }

        public double MudFriction
        {
            get => _mudFriction;
            set
            {
                if (Math.Abs(_mudFriction - value) < 0.0001) return;
                _mudFriction = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MudFriction)));
            }
        }

        public bool IgnoreIce
        {
            get => _ignoreIce;
            set
            {
                if (_ignoreIce == value) return;
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
