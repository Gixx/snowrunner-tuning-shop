using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Suspension;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class SuspensionTuningView : UserControl
{
    private readonly ObservableCollection<SuspensionRowViewModel> _suspensions = [];
    private bool _pakWritesAllowed = true;
    private AppSession? _session;

    public SuspensionTuningView()
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
        await ReloadSuspensionsAsync(cancellationToken);
    }

    public void Clear()
    {
        PakPath = null;
        _suspensions.Clear();
        RefreshFilter();
        PartsTuningUiHelpers.ClearWriteButtons(ApplyMultipliersButton, SaveIndividualButton, RestoreSuspensionsButton);
    }

    public void RefreshRestoreButton() =>
        PartsTuningUiHelpers.SetPartWriteButtonStates(
            _session,
            PakPath,
            _pakWritesAllowed,
            ApplyMultipliersButton,
            SaveIndividualButton,
            RestoreSuspensionsButton);

    private void ApplyStaticText()
    {
        MultipliersExpander.Header = UiText.Suspension.GlobalMultipliersTitle;
        ApplyMultipliersButton.Content = UiText.Suspension.Apply;
        SaveIndividualButton.Content = UiText.Suspension.SaveIndividualChanges;
        RestoreSuspensionsButton.Content = UiText.Suspension.RestoreSuspensionsToBaseline;
        ReloadButton.Content = UiText.Suspension.RefreshList;
        FilterTextBox.PlaceholderText = UiText.Suspension.FilterPlaceholder;
        PartsTuningUiHelpers.SetColumnHeaders(
            SuspensionsGrid,
            UiText.Suspension.CategoryColumn,
            UiText.Suspension.NameColumn,
            UiText.Suspension.UsedByColumn,
            UiText.Suspension.PriceColumn,
            UiText.Suspension.DamageColumn,
            UiText.Suspension.FrontHeightColumn,
            UiText.Suspension.FrontStrengthColumn,
            UiText.Suspension.FrontDampingColumn,
            UiText.Suspension.RearHeightColumn,
            UiText.Suspension.RearStrengthColumn,
            UiText.Suspension.RearDampingColumn);
    }

    private void ReloadButton_Click(object? sender, RoutedEventArgs e)
    {
        RefreshRestoreButton();
        ReloadSuspensions();
    }

    private async void RestoreSuspensionsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryBeginWrite(owner, _session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => ReportStatus(UiText.Suspension.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreSuspensionsButton))
        {
            try
            {
                var path = PakPath!;
                var result = await Task.Run(() => SuspensionService.RestoreSuspensionsFromBaseline(path));
                ResetMultiplierSlidersToBaseline();
                ReloadSuspensions();
                ReportStatus(UiText.Suspension.MultipliersAppliedStatus(
                    result.ChangedSuspensions,
                    result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Suspension.SaveErrorTitle);
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
                () => ReportStatus(UiText.Suspension.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreSuspensionsButton))
        {
            try
            {
                var path = PakPath!;
                var height = GetMultiplier(HeightMultiplierSlider);
                var strength = GetMultiplier(StrengthMultiplierSlider);
                var damping = GetMultiplier(DampingMultiplierSlider);
                var damage = GetMultiplier(DamageMultiplierSlider);
                var result = await Task.Run(() => SuspensionService.ApplyGlobalMultipliers(
                    path, height, strength, damping, damage));

                ReloadSuspensions();
                ReportStatus(UiText.Suspension.MultipliersAppliedStatus(
                    result.ChangedSuspensions,
                    result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Suspension.SaveErrorTitle);
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
                () => ReportStatus(UiText.Suspension.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreSuspensionsButton))
        {
            try
            {
                PartsTuningUiHelpers.CommitGridEdits(SuspensionsGrid);

                var suspensions = _suspensions.Select(row => row.ToDefinition()).ToArray();
                var path = PakPath!;
                var result = await Task.Run(() => SuspensionService.SaveSuspensionChanges(path, suspensions));
                ReloadSuspensions();
                ReportStatus(UiText.Suspension.IndividualSavedStatus(result.ChangedSuspensions, result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Suspension.SaveErrorTitle);
            }
        }
    }

    private void ReloadSuspensions()
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            Clear();
            return;
        }

        try
        {
            ApplySuspensions(SuspensionService.LoadSuspensions(PakPath, AppLanguage.Current));
        }
        catch (Exception ex)
        {
            Clear();
            if (OwnerWindow is { } owner)
            {
                _ = AppDialogs.ShowError(owner, ex.Message, UiText.Suspension.LoadErrorTitle);
            }
        }
    }

    private async Task ReloadSuspensionsAsync(CancellationToken cancellationToken = default)
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
            var suspensions = await Task.Run(() => SuspensionService.LoadSuspensions(path, language), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ApplySuspensions(suspensions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Clear();
            if (OwnerWindow is { } owner)
            {
                await AppDialogs.ShowError(owner, ex.Message, UiText.Suspension.LoadErrorTitle);
            }
        }
    }

    private void ApplySuspensions(IReadOnlyList<SuspensionDefinition> suspensions)
    {
        _suspensions.Clear();
        foreach (var suspension in suspensions)
        {
            _suspensions.Add(SuspensionRowViewModel.FromDefinition(suspension));
        }

        RefreshFilter();
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

    private void RefreshFilter() =>
        TuningListFilter.ApplyFilter(
            SuspensionsGrid,
            _suspensions,
            row => TuningListFilter.Matches(
                FilterTextBox.Text,
                row.Category,
                row.DisplayName,
                row.Name,
                row.SetName,
                row.SetId,
                row.UsedBy,
                row.UsedByTooltip));

    private void ResetMultiplierSlidersToBaseline()
    {
        HeightMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        StrengthMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        DampingMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        DamageMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        UpdateMultiplierLabels();
    }

    private void UpdateMultiplierLabels()
    {
        if (HeightMultiplierLabel is null
            || StrengthMultiplierLabel is null
            || DampingMultiplierLabel is null
            || DamageMultiplierLabel is null)
        {
            return;
        }

        HeightMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.Height, GetMultiplierIndex(HeightMultiplierSlider));
        StrengthMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.Strength, GetMultiplierIndex(StrengthMultiplierSlider));
        DampingMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.Damping, GetMultiplierIndex(DampingMultiplierSlider));
        DamageMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.DamageCapacity, GetMultiplierIndex(DamageMultiplierSlider));
    }

    private static int GetMultiplierIndex(Slider slider) =>
        TuningMultiplierPresets.ClampIndex((int)Math.Round(slider.Value));

    private static double GetMultiplier(Slider slider) =>
        TuningMultiplierPresets.GetValue(GetMultiplierIndex(slider));

    private void ReportStatus(string message) =>
        StatusChanged?.Invoke(this, message);

    public sealed class SuspensionRowViewModel : INotifyPropertyChanged
    {
        private int _price;
        private double _damageCapacity;
        private double? _frontHeight;
        private double? _frontStrength;
        private double? _frontDamping;
        private double? _rearHeight;
        private double? _rearStrength;
        private double? _rearDamping;

        public required string EntryPath { get; init; }
        public required string Name { get; init; }
        public required string DisplayName { get; init; }
        public required string SourceFile { get; init; }
        public required string SetId { get; init; }
        public required string SetName { get; init; }
        public required string UsedBy { get; init; }
        public required string UsedByTooltip { get; init; }
        public required string Category { get; init; }

        public int Price
        {
            get => _price;
            set
            {
                if (_price == value) return;
                _price = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Price)));
            }
        }

        public bool HasFront { get; init; }
        public bool HasRear { get; init; }

        public double DamageCapacity
        {
            get => _damageCapacity;
            set
            {
                if (Math.Abs(_damageCapacity - value) < 0.0001) return;
                _damageCapacity = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DamageCapacity)));
            }
        }

        public double? FrontHeight
        {
            get => _frontHeight;
            set => SetNullable(ref _frontHeight, value, nameof(FrontHeight));
        }

        public double? FrontStrength
        {
            get => _frontStrength;
            set => SetNullable(ref _frontStrength, value, nameof(FrontStrength));
        }

        public double? FrontDamping
        {
            get => _frontDamping;
            set => SetNullable(ref _frontDamping, value, nameof(FrontDamping), nameof(FrontDampingText));
        }

        public string FrontDampingText
        {
            get => FormatOptionalDamping(HasFront, _frontDamping);
            set => TryParseOptionalDamping(value, HasFront, v => FrontDamping = v);
        }

        public double? RearHeight
        {
            get => _rearHeight;
            set => SetNullable(ref _rearHeight, value, nameof(RearHeight));
        }

        public double? RearStrength
        {
            get => _rearStrength;
            set => SetNullable(ref _rearStrength, value, nameof(RearStrength));
        }

        public double? RearDamping
        {
            get => _rearDamping;
            set => SetNullable(ref _rearDamping, value, nameof(RearDamping), nameof(RearDampingText));
        }

        public string RearDampingText
        {
            get => FormatOptionalDamping(HasRear, _rearDamping);
            set => TryParseOptionalDamping(value, HasRear, v => RearDamping = v);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetNullable(ref double? field, double? value, string propertyName, string? relatedPropertyName = null)
        {
            if (field == value
                || (field is double left && value is double right && Math.Abs(left - right) < 0.0001))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (relatedPropertyName is not null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(relatedPropertyName));
            }
        }

        private static string FormatOptionalDamping(bool hasAxle, double? value)
        {
            if (!hasAxle)
            {
                return "";
            }

            return value is double number
                ? number.ToString("0.##", CultureInfo.InvariantCulture)
                : UiText.Suspension.MissingValuePlaceholder;
        }

        private static void TryParseOptionalDamping(string? text, bool hasAxle, Action<double?> assign)
        {
            if (!hasAxle)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(text)
                || text.Equals(UiText.Suspension.MissingValuePlaceholder, StringComparison.OrdinalIgnoreCase))
            {
                assign(null);
                return;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
            {
                assign(parsed);
            }
        }

        public static SuspensionRowViewModel FromDefinition(SuspensionDefinition definition) =>
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
                HasFront = definition.HasFront,
                HasRear = definition.HasRear,
                DamageCapacity = definition.DamageCapacity,
                FrontHeight = definition.FrontHeight,
                FrontStrength = definition.FrontStrength,
                FrontDamping = definition.FrontDamping,
                RearHeight = definition.RearHeight,
                RearStrength = definition.RearStrength,
                RearDamping = definition.RearDamping,
            };

        public SuspensionDefinition ToDefinition() =>
            new()
            {
                EntryPath = EntryPath,
                Name = Name,
                DisplayName = DisplayName,
                SourceFile = SourceFile,
                SetId = SetId,
                SetName = SetName,
                UsedBy = UsedBy,
                UsedByTooltip = UsedByTooltip,
                Category = Category,
                Price = Price,
                HasFront = HasFront,
                HasRear = HasRear,
                DamageCapacity = DamageCapacity,
                FrontHeight = FrontHeight,
                FrontStrength = FrontStrength,
                FrontDamping = FrontDamping,
                RearHeight = RearHeight,
                RearStrength = RearStrength,
                RearDamping = RearDamping,
            };
    }
}
