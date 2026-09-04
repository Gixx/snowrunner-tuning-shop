using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Suspension;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class SuspensionTuningView : UserControl
{
    private readonly ObservableCollection<SuspensionRowViewModel> _suspensions = [];
    private readonly ICollectionView _suspensionsView;
    private bool _pakWritesAllowed = true;
    private AppSession? _session;

    public SuspensionTuningView()
    {
        InitializeComponent();
        _suspensionsView = CollectionViewSource.GetDefaultView(_suspensions);
        _suspensionsView.Filter = MatchesFilter;
        SuspensionsGrid.ItemsSource = _suspensionsView;
        ResetMultiplierSlidersToBaseline();
    }

    public string? PakPath { get; private set; }

    public void AttachSession(AppSession session) => _session = session;

    public void SetPakWritesAllowed(bool allowed)
    {
        _pakWritesAllowed = allowed;
        RefreshRestoreButton();
    }

    public void LoadFromPak(string pakPath)
    {
        PakPath = pakPath;
        RefreshRestoreButton();
        ReloadSuspensions();
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
        ApplyMultipliersButton.IsEnabled = false;
        SaveIndividualButton.IsEnabled = false;
        RestoreSuspensionsButton.IsEnabled = false;
    }

    public void RefreshRestoreButton()
    {
        var hasPak = !string.IsNullOrWhiteSpace(PakPath);
        ApplyMultipliersButton.IsEnabled = hasPak && _pakWritesAllowed;
        SaveIndividualButton.IsEnabled = hasPak && _pakWritesAllowed;
        RestoreSuspensionsButton.IsEnabled = hasPak
            && _pakWritesAllowed
            && PakBaselineService.HasBaseline(PakPath!);
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRestoreButton();
        ReloadSuspensions();
    }

    private void RestoreSuspensionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryBeginWrite(_session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => MessageBox.Show(UiText.Suspension.LoadPakFirst, UiText.Suspension.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Information)))
        {
            return;
        }

        try
        {
            var result = SuspensionService.RestoreSuspensionsFromBaseline(PakPath);
            ResetMultiplierSlidersToBaseline();
            ReloadSuspensions();
            MessageBox.Show(
                UiText.Suspension.RestoreSuspensionsMessage(
                    result.ChangedSuspensions,
                    result.UpdatedFiles),
                UiText.Suspension.RestoreSuspensionsSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Suspension.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyMultipliersButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryBeginWrite(_session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => MessageBox.Show(UiText.Suspension.LoadPakFirst, UiText.Suspension.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Information)))
        {
            return;
        }

        try
        {
            var result = SuspensionService.ApplyGlobalMultipliers(
                PakPath,
                GetMultiplier(HeightMultiplierSlider),
                GetMultiplier(StrengthMultiplierSlider),
                GetMultiplier(DampingMultiplierSlider),
                GetMultiplier(DamageMultiplierSlider));

            ReloadSuspensions();
            MessageBox.Show(
                UiText.Suspension.MultipliersSavedMessage(
                    result.ChangedSuspensions,
                    result.UpdatedFiles),
                UiText.Suspension.SaveSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Suspension.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveIndividualButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryBeginWrite(_session, PakPath, _pakWritesAllowed, requireBaseline: false,
                () => MessageBox.Show(UiText.Suspension.LoadPakFirst, UiText.Suspension.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Information)))
        {
            return;
        }

        try
        {
            SuspensionsGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
            SuspensionsGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

            var suspensions = _suspensions
                .Select(row => row.ToDefinition())
                .ToArray();

            var result = SuspensionService.SaveSuspensionChanges(PakPath, suspensions);
            ReloadSuspensions();
            MessageBox.Show(
                UiText.Suspension.IndividualSavedMessage(result.ChangedSuspensions),
                UiText.Suspension.SaveSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Suspension.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show(ex.Message, UiText.Suspension.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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
            // Stale load discarded after tab/pak switch.
        }
        catch (Exception ex)
        {
            Clear();
            MessageBox.Show(ex.Message, UiText.Suspension.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplySuspensions(IReadOnlyList<SuspensionDefinition> suspensions)
    {
        _suspensions.Clear();
        foreach (var suspension in suspensions)
        {
            _suspensions.Add(SuspensionRowViewModel.FromDefinition(suspension));
        }
    }

    private void MultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        UpdateMultiplierLabels();

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        TuningListFilter.UpdatePlaceholderVisibility(FilterTextBox, FilterPlaceholder);
        _suspensionsView.Refresh();
    }

    private bool MatchesFilter(object item) =>
        item is SuspensionRowViewModel row
        && TuningListFilter.Matches(
            FilterTextBox.Text,
            row.Category,
            row.DisplayName,
            row.Name,
            row.SetName,
            row.SetId,
            row.UsedBy,
            row.UsedByTooltip);

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
            UiText.Slider.Height,
            GetMultiplierIndex(HeightMultiplierSlider));
        StrengthMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.Strength,
            GetMultiplierIndex(StrengthMultiplierSlider));
        DampingMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.Damping,
            GetMultiplierIndex(DampingMultiplierSlider));
        DamageMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.DamageCapacity,
            GetMultiplierIndex(DamageMultiplierSlider));
    }

    private static int GetMultiplierIndex(Slider slider) =>
        TuningMultiplierPresets.ClampIndex((int)Math.Round(slider.Value));

    private static double GetMultiplier(Slider slider) =>
        TuningMultiplierPresets.GetValue(GetMultiplierIndex(slider));

    public sealed class SuspensionRowViewModel : INotifyPropertyChanged
    {
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
        public int Price { get; init; }
        public bool HasFront { get; init; }
        public bool HasRear { get; init; }

        public double DamageCapacity
        {
            get => _damageCapacity;
            set
            {
                if (Math.Abs(_damageCapacity - value) < 0.0001)
                {
                    return;
                }

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
