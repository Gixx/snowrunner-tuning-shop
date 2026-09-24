using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Diagnostics;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Trailers;
using SnowRunnerTuningShop.Core.Trucks;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Desktop.Trailers;
using SnowRunnerTuningShop.Desktop.Vehicles;
using SnowRunnerTuningShop.Localization;
using AvaloniaMedia = Avalonia.Media;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class TrailersView : UserControl
{
    private readonly List<TrailerCard> _all = [];
    private readonly ObservableCollection<TrailerCard> _visible = [];
    private IReadOnlyList<TrailerTuningDefinition> _trailers = [];
    private string? _trailersPakPath;
    private AppSession? _session;
    private TrailerCard? _currentCard;
    private TrailerTuningDefinition? _currentTrailer;
    private string _filter = "All";
    private bool _ready;
    private bool _suppressUnlockRankSync;
    private int _loadVersion;
    private CancellationTokenSource? _loadCts;

    public TrailersView()
    {
        InitializeComponent();
        TrailersItems.ItemsSource = _visible;
        ApplyStaticText();
        Loaded += TrailersView_Loaded;
    }

    private Window? OwnerWindow => TopLevel.GetTopLevel(this) as Window;

    public void AttachSession(AppSession session)
    {
        _session = session;
        _session.PakChanged += (_, _) => OnPakChanged();
        _session.BaselineChanged += (_, _) =>
        {
            RefreshRestoreButton();
            RefreshGlobalMultipliersPanel();
        };
        _session.GameRunningChanged += (_, _) =>
        {
            RefreshRestoreButton();
            RefreshGlobalMultipliersPanel();
        };
        OnPakChanged();
    }

    private void ApplyStaticText()
    {
        GlobalMultipliersExpander.Header = UiText.Trailers.GlobalMultipliersTitle;
        GlobalMultipliersHintText.Text = UiText.Trailers.GlobalMultipliersHint;
        ApplyGlobalMultipliersButton.Content = UiText.Trailers.ApplyGlobalMultipliers;
        FuelMultiplierLabel.Text = UiText.Trailers.FuelMultiplierDefault;
        RepairsMultiplierLabel.Text = UiText.Trailers.RepairsMultiplierDefault;
        WheelsMultiplierLabel.Text = UiText.Trailers.WheelsMultiplierDefault;
        PriceMultiplierLabel.Text = UiText.Trailers.PriceMultiplierDefault;
        MassMultiplierLabel.Text = UiText.Trailers.MassMultiplierDefault;

        StoreUnlocksExpander.Header = UiText.Trailers.StoreUnlocksTitle;
        StoreUnlocksHintText.Text = UiText.Trailers.StoreUnlocksHint;
        MakeMissionTrailersPurchasableButton.Content = UiText.Trailers.MakeMissionTrailersPurchasable;

        FilterAll.Content = UiText.Trailers.All;
        FilterScout.Content = UiText.Trailers.HitchScout;
        FilterStandard.Content = UiText.Trailers.HitchStandard;
        FilterSaddleLow.Content = UiText.Trailers.HitchSaddleLow;
        FilterSaddleHigh.Content = UiText.Trailers.HitchSaddleHigh;
        FilterMission.Content = UiText.Trailers.Mission;
        RestoreAllTrailersButton.Content = UiText.Trailers.RestoreAllTrailers;
        SearchTextBox.PlaceholderText = UiText.Trailers.SearchPlaceholder;

        BackButton.Content = UiText.Trailers.BackToList;
        HitchLabelText.Text = UiText.Trailers.HitchLabel;
        FunctionLabelText.Text = UiText.Trailers.FunctionLabel;
        MissionLabelText.Text = UiText.Trailers.Mission;
        TuningHintText.Text = UiText.Trailers.LoadPakHint;
        TuningTitleText.Text = UiText.Trailers.TuningTitle;
        FuelTankLabelText.Text = UiText.Trailers.FuelTankLabel;
        FuelUnitText.Text = UiText.Trailers.FuelUnit;
        MassLabelText.Text = UiText.Trailers.MassLabel;
        MassHintText.Text = UiText.Trailers.MassHint;
        WaterTankLabelText.Text = UiText.Trailers.WaterTankLabel;
        WaterUnitText.Text = UiText.Trailers.FuelUnit;
        RepairPartsLabelText.Text = UiText.Trailers.RepairPartsLabel;
        SpareWheelsLabelText.Text = UiText.Trailers.SpareWheelsLabel;
        StorePriceLabelText.Text = UiText.Trailers.StorePriceLabel;
        AvailableInStoreLabelText.Text = UiText.Trailers.AvailableInStoreLabel;
        AvailableInStoreHintText.Text = UiText.Trailers.AvailableInStoreHint;
        UnlockRankLabelText.Text = UiText.Trailers.UnlockRankLabel;
        UnlockRankHintText.Text = UiText.Trailers.UnlockRankHint;
        SaveTuningButton.Content = UiText.Trailers.SaveChanges;
        RestoreTrailerButton.Content = UiText.Trailers.RestoreThisTrailer;
        LoadingText.Text = UiText.Parts.Loading;
    }

    private void TrailersView_Loaded(object? sender, RoutedEventArgs e)
    {
        if (_ready)
        {
            return;
        }

        _ready = true;
        FilterAll.IsChecked = true;
        LoadCatalog();
        RefreshGlobalMultipliersPanel();
    }

    private void OnPakChanged()
    {
        _trailers = [];
        _trailersPakPath = null;
        RefreshGlobalMultipliersPanel();
        if (_currentCard is not null && DetailPanel.IsVisible)
        {
            _ = LoadTuningAsync(_currentCard);
        }
        else
        {
            ShowTuningHint(UiText.Trailers.LoadPakHint);
        }
    }

    private void RefreshGlobalMultipliersPanel()
    {
        var canApply = _session?.HasPak == true
            && !string.IsNullOrWhiteSpace(_session.PakPath)
            && PakBaselineService.HasBaseline(_session.PakPath)
            && PakWriteUi.CanWrite(_session);

        ApplyGlobalMultipliersButton.IsEnabled = canApply;
        FuelMultiplierSlider.IsEnabled = canApply;
        RepairsMultiplierSlider.IsEnabled = canApply;
        WheelsMultiplierSlider.IsEnabled = canApply;
        PriceMultiplierSlider.IsEnabled = canApply;
        MassMultiplierSlider.IsEnabled = canApply;
        RestoreAllTrailersButton.IsEnabled = canApply;
        MakeMissionTrailersPurchasableButton.IsEnabled = canApply;

        if (!canApply)
        {
            ResetGlobalMultiplierSlidersToBaseline();
        }

        UpdateGlobalMultiplierLabels();
    }

    private void ResetGlobalMultiplierSlidersToBaseline()
    {
        FuelMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        RepairsMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        WheelsMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        PriceMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        MassMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
    }

    private void GlobalMultiplierSlider_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!_ready || e.Property != RangeBase.ValueProperty)
        {
            return;
        }

        UpdateGlobalMultiplierLabels();
    }

    private void UpdateGlobalMultiplierLabels()
    {
        FuelMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.FuelTank,
            GetMultiplierIndex(FuelMultiplierSlider));
        RepairsMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.RepairParts,
            GetMultiplierIndex(RepairsMultiplierSlider));
        WheelsMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.SpareWheels,
            GetMultiplierIndex(WheelsMultiplierSlider));
        PriceMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.StorePrice,
            GetMultiplierIndex(PriceMultiplierSlider));
        MassMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.Mass,
            GetMassMultiplierIndex(MassMultiplierSlider));
    }

    private static int GetMultiplierIndex(Slider slider) =>
        TuningMultiplierPresets.ClampIndex((int)Math.Round(slider.Value, MidpointRounding.AwayFromZero));

    private static int GetMassMultiplierIndex(Slider slider) =>
        TuningMultiplierPresets.ClampMassIndex((int)Math.Round(slider.Value, MidpointRounding.AwayFromZero));

    private static double GetMultiplier(Slider slider) =>
        TuningMultiplierPresets.GetValue(GetMultiplierIndex(slider));

    private static double GetMassMultiplier(Slider slider) =>
        TuningMultiplierPresets.GetValue(GetMassMultiplierIndex(slider));

    private async void ApplyGlobalMultipliersButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryBeginWrite(
                owner,
                _session,
                _session?.PakPath,
                PakWriteUi.CanWrite(_session),
                requireBaseline: true,
                onMissingPak: () => TuningStatusText.Text = UiText.Trailers.LoadPakForGlobalHint))
        {
            if (_session?.HasPak != true || string.IsNullOrWhiteSpace(_session.PakPath))
            {
                await AppDialogs.ShowWarning(
                    owner,
                    UiText.Trailers.LoadPakForGlobalHint,
                    UiText.Trailers.SaveErrorTitle);
            }

            return;
        }

        using (PakWriteUi.BeginBusyWrite(
                   owner,
                   ApplyGlobalMultipliersButton,
                   MakeMissionTrailersPurchasableButton,
                   RestoreAllTrailersButton))
        {
            try
            {
                var pakPath = _session!.PakPath!;
                // Capture UI control values on the UI thread before Task.Run.
                var fuel = GetMultiplier(FuelMultiplierSlider);
                var repairs = GetMultiplier(RepairsMultiplierSlider);
                var wheels = GetMultiplier(WheelsMultiplierSlider);
                var price = GetMultiplier(PriceMultiplierSlider);
                var mass = GetMassMultiplier(MassMultiplierSlider);

                var result = await Task.Run(() => TrailerTuningService.ApplyGlobalMultipliers(
                    pakPath,
                    fuel,
                    repairs,
                    wheels,
                    price,
                    mass));

                _trailersPakPath = null;
                if (_currentCard is not null && DetailPanel.IsVisible)
                {
                    _ = LoadTuningAsync(_currentCard);
                }

                TuningStatusText.Text = UiText.Trailers.GlobalMultipliersSavedMessage(
                    result.ChangedTrailers,
                    result.UpdatedFiles);
            }
            catch (Exception ex)
            {
                TuningStatusText.Text = UiText.Main.ErrorStatus(ex.Message);
                await AppDialogs.ShowError(owner, ex.Message, UiText.Trailers.SaveErrorTitle);
            }
        }
    }

    private async void MakeMissionTrailersPurchasableButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryBeginWrite(
                owner,
                _session,
                _session?.PakPath,
                PakWriteUi.CanWrite(_session),
                requireBaseline: true,
                onMissingPak: () => TuningStatusText.Text = UiText.Trailers.LoadPakForGlobalHint))
        {
            if (_session?.HasPak != true || string.IsNullOrWhiteSpace(_session.PakPath))
            {
                await AppDialogs.ShowWarning(
                    owner,
                    UiText.Trailers.LoadPakForGlobalHint,
                    UiText.Trailers.SaveErrorTitle);
            }

            return;
        }

        using (PakWriteUi.BeginBusyWrite(
                   owner,
                   ApplyGlobalMultipliersButton,
                   MakeMissionTrailersPurchasableButton,
                   RestoreAllTrailersButton))
        {
            try
            {
                var pakPath = _session!.PakPath!;
                var result = await Task.Run(() => TrailerTuningService.MakeQuestTrailersPurchasable(pakPath));

                _trailersPakPath = null;
                if (_currentCard is not null && DetailPanel.IsVisible)
                {
                    _ = LoadTuningAsync(_currentCard);
                }

                TuningStatusText.Text = UiText.Trailers.StoreUnlocksSavedMessage(
                    result.ChangedTrailers,
                    result.UpdatedFiles);
            }
            catch (Exception ex)
            {
                TuningStatusText.Text = UiText.Main.ErrorStatus(ex.Message);
                await AppDialogs.ShowError(owner, ex.Message, UiText.Trailers.SaveErrorTitle);
            }
        }
    }

    private async void RestoreAllTrailersButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryBeginWrite(
                owner,
                _session,
                _session?.PakPath,
                PakWriteUi.CanWrite(_session),
                requireBaseline: true,
                onMissingPak: () => TuningStatusText.Text = UiText.Trailers.LoadPakForGlobalHint))
        {
            if (_session?.HasPak != true || string.IsNullOrWhiteSpace(_session.PakPath))
            {
                await AppDialogs.ShowWarning(
                    owner,
                    UiText.Trailers.LoadPakForGlobalHint,
                    UiText.Trailers.SaveErrorTitle);
            }

            return;
        }

        if (!await AppDialogs.Confirm(
                owner,
                UiText.Trailers.RestoreAllTrailersConfirmMessage,
                UiText.Trailers.RestoreAllTrailersConfirmTitle))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(
                   owner,
                   ApplyGlobalMultipliersButton,
                   MakeMissionTrailersPurchasableButton,
                   RestoreAllTrailersButton))
        {
            try
            {
                var pakPath = _session!.PakPath!;
                var result = await Task.Run(() => TrailerTuningService.RestoreAllTrailersFromBaseline(pakPath));
                _trailersPakPath = null;
                ResetGlobalMultiplierSlidersToBaseline();
                UpdateGlobalMultiplierLabels();
                if (_currentCard is not null && DetailPanel.IsVisible)
                {
                    _ = LoadTuningAsync(_currentCard);
                }

                TuningStatusText.Text = UiText.Trailers.RestoreAllTrailersSavedMessage(
                    result.ChangedTrailers,
                    result.UpdatedFiles);
            }
            catch (Exception ex)
            {
                TuningStatusText.Text = UiText.Main.ErrorStatus(ex.Message);
                await AppDialogs.ShowError(owner, ex.Message, UiText.Trailers.SaveErrorTitle);
            }
        }
    }

    private void LoadCatalog()
    {
        _all.Clear();
        var entries = TrailerCatalogLoader.Load();
        if (entries.Count == 0)
        {
            CountTextBlock.Text = UiText.Trailers.CatalogMissing;
            return;
        }

        foreach (var entry in entries)
        {
            _all.Add(new TrailerCard(entry));
        }

        ApplyFilter();
    }

    private void Filter_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (!_ready || sender is not RadioButton radio || radio.IsChecked != true)
        {
            return;
        }

        _filter = radio.Tag as string ?? "All";
        if (ReferenceEquals(radio, FilterAll))
        {
            _filter = "All";
        }

        ApplyFilter();
    }

    private void SearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_ready)
        {
            ApplyFilter();
        }
    }

    private void ApplyFilter()
    {
        if (!_ready)
        {
            return;
        }

        var search = SearchTextBox.Text?.Trim() ?? "";

        IEnumerable<TrailerCard> query = _all;
        if (!string.Equals(_filter, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = string.Equals(_filter, "mission", StringComparison.OrdinalIgnoreCase)
                ? query.Where(card => card.IsMission)
                : query.Where(card => card.Hitch.Equals(_filter, StringComparison.OrdinalIgnoreCase));
        }

        if (search.Length > 0)
        {
            query = query.Where(card =>
                card.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || card.Id.Contains(search, StringComparison.OrdinalIgnoreCase)
                || card.HitchLabel.Contains(search, StringComparison.OrdinalIgnoreCase)
                || card.FunctionLabel.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        _visible.Clear();
        foreach (var card in query.OrderBy(card => card.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            _visible.Add(card);
        }

        CountTextBlock.Text = UiText.Trailers.CountLabel(_visible.Count);
    }

    private async void TrailerCard_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left
            || sender is not Control element
            || element.DataContext is not TrailerCard card)
        {
            return;
        }

        try
        {
            await ShowDetailAsync(card);
        }
        catch (Exception ex)
        {
            GlobalExceptionHandler.Handle(ex, isTerminating: false);
        }
    }

    private async Task ShowDetailAsync(TrailerCard card)
    {
        CrashReportContext.SetVehicle(card.Id, card.DisplayName);
        _currentCard = card;
        DetailTitleText.Text = card.DisplayName;
        DetailImage.Source = VehicleImages.TryLoadBitmap(card.ImagePath, decodeWidth: 720) ?? card.Image;
        DetailHitchText.Text = card.HitchLabel;
        DetailFunctionText.Text = card.FunctionLabel;
        DetailMissionText.Text = card.IsMission ? UiText.Trailers.MissionYes : UiText.Trailers.MissionNo;

        ListPanel.IsVisible = false;
        DetailPanel.IsVisible = true;
        Focus();
        await LoadTuningAsync(card);
    }

    private void View_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !DetailPanel.IsVisible)
        {
            return;
        }

        NavigateBackToList();
        e.Handled = true;
    }

    private async Task LoadTuningAsync(TrailerCard card)
    {
        var version = ++_loadVersion;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var cancellationToken = _loadCts.Token;

        _currentTrailer = null;
        TuningStatusText.Text = "";
        RefreshRestoreButton();

        if (_session?.HasPak != true || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            ShowTuningHint(UiText.Trailers.LoadPakHint);
            return;
        }

        var pakPath = _session.PakPath;
        var needsLoad = !string.Equals(_trailersPakPath, pakPath, StringComparison.OrdinalIgnoreCase)
            || _trailers.Count == 0;
        if (needsLoad)
        {
            SetLoading(true);
        }

        try
        {
            try
            {
                await EnsureTrailersLoadedAsync(pakPath, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                if (version != _loadVersion)
                {
                    return;
                }

                ShowTuningHint(ex.Message);
                return;
            }

            if (cancellationToken.IsCancellationRequested
                || version != _loadVersion
                || !ReferenceEquals(_currentCard, card))
            {
                return;
            }

            var trailer = TrailerTuningService.FindByCatalog(_trailers, card.Id);
            if (trailer is null)
            {
                ShowTuningHint(UiText.Trailers.TrailerNotFound);
                return;
            }

            _currentTrailer = trailer;
            BindCapacityField(FuelRow, FuelCapacityTextBox, trailer.HasFuel, trailer.FuelCapacity);
            BindCapacityField(WaterRow, WaterCapacityTextBox, trailer.HasWater, trailer.WaterCapacity);
            BindCapacityField(RepairsRow, RepairsCapacityTextBox, trailer.HasRepairs, trailer.RepairsCapacity);
            BindCapacityField(WheelsRow, WheelRepairsTextBox, trailer.HasWheels, trailer.WheelRepairsCapacity);

            MassRow.IsVisible = trailer.HasMass;
            MassHintText.IsVisible = trailer.HasMass;
            MassSafeRangeHint.IsVisible = trailer.HasMass;
            MassTextBox.Text = trailer.HasMass ? TruckTuningService.FormatMass(trailer.Mass) : "";

            FuelSafeRangeHint.IsVisible = trailer.HasFuel;
            WaterSafeRangeHint.IsVisible = trailer.HasWater;
            RepairsSafeRangeHint.IsVisible = trailer.HasRepairs;
            WheelsSafeRangeHint.IsVisible = trailer.HasWheels;

            PriceRow.IsVisible = trailer.HasGameData;
            PriceSafeRangeHint.IsVisible = trailer.HasGameData;
            AvailableInStoreRow.IsVisible = trailer.HasGameData;
            AvailableInStoreHintText.IsVisible = trailer.HasGameData;
            UnlockRankRow.IsVisible = trailer.HasGameData;
            UnlockRankHintText.IsVisible = trailer.HasGameData;
            if (trailer.HasGameData)
            {
                StorePriceTextBox.Text = trailer.Price.ToString(CultureInfo.InvariantCulture);
                AvailableInStoreCheckBox.IsChecked = trailer.IsAvailableInStore;
                _suppressUnlockRankSync = true;
                UnlockRankSlider.Value = trailer.UnlockByRank;
                UnlockRankTextBox.Text = trailer.UnlockByRank.ToString(CultureInfo.InvariantCulture);
                _suppressUnlockRankSync = false;
            }

            if (!trailer.HasFuel && !trailer.HasWater && !trailer.HasRepairs && !trailer.HasWheels && !trailer.HasGameData && !trailer.HasMass)
            {
                ShowTuningHint(UiText.Trailers.NoTunableFields);
                return;
            }

            RefreshSafeRangeHints();
            TuningHintText.IsVisible = false;
            TuningForm.IsVisible = true;
            RefreshRestoreButton();
        }
        finally
        {
            if (version == _loadVersion)
            {
                SetLoading(false);
            }
        }
    }

    private static void BindCapacityField(Control row, TextBox box, bool visible, int value)
    {
        row.IsVisible = visible;
        box.Text = visible ? value.ToString(CultureInfo.InvariantCulture) : "";
    }

    private async Task EnsureTrailersLoadedAsync(string pakPath, CancellationToken cancellationToken = default)
    {
        if (string.Equals(_trailersPakPath, pakPath, StringComparison.OrdinalIgnoreCase) && _trailers.Count > 0)
        {
            return;
        }

        var language = AppLanguage.Current;
        var trailers = await Task.Run(() => TrailerTuningService.LoadTrailers(pakPath, language), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _trailers = trailers;
        _trailersPakPath = pakPath;
    }

    private void SetLoading(bool isLoading) => LoadingOverlay.IsVisible = isLoading;

    private void ShowTuningHint(string message)
    {
        TuningForm.IsVisible = false;
        TuningHintText.Text = message;
        TuningHintText.IsVisible = true;
        RestoreTrailerButton.IsEnabled = false;
    }

    private void RefreshRestoreButton()
    {
        var canWrite = PakWriteUi.CanWrite(_session);
        RestoreTrailerButton.IsEnabled = _currentTrailer is not null
            && !string.IsNullOrWhiteSpace(_session?.PakPath)
            && PakBaselineService.HasBaseline(_session.PakPath)
            && canWrite;
        SaveTuningButton.IsEnabled = _currentTrailer is not null && canWrite;
    }

    private async void SaveTuningButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryProceed(owner, _session))
        {
            return;
        }

        if (_currentTrailer is null || string.IsNullOrWhiteSpace(_session?.PakPath) || _currentCard is null)
        {
            TuningStatusText.Text = UiText.Trailers.LoadPakHint;
            return;
        }

        if (!TryReadForm(out var fuel, out var water, out var repairs, out var wheels, out var price, out var unlockRank, out var mass))
        {
            return;
        }

        if (_currentTrailer.HasFuel)
        {
            _currentTrailer.FuelCapacity = fuel;
        }

        if (_currentTrailer.HasWater)
        {
            _currentTrailer.WaterCapacity = water;
        }

        if (_currentTrailer.HasRepairs)
        {
            _currentTrailer.RepairsCapacity = repairs;
        }

        if (_currentTrailer.HasWheels)
        {
            _currentTrailer.WheelRepairsCapacity = wheels;
        }

        if (_currentTrailer.HasMass)
        {
            _currentTrailer.Mass = mass;
        }

        if (_currentTrailer.HasGameData)
        {
            _currentTrailer.Price = price;
            _currentTrailer.UnlockByRank = unlockRank;
            var wantAvailable = AvailableInStoreCheckBox.IsChecked == true;
            _currentTrailer.MakeAvailableInStore = wantAvailable;
            // Special hitches (train) stay IsQuest=false when unavailable — hitch alone keeps them out of the store.
            _currentTrailer.IsQuest = !wantAvailable
                && (_currentTrailer.HasStoreCompatibleHitch || _currentTrailer.BaselineIsQuest);
        }

        using (PakWriteUi.BeginBusyWrite(owner, SaveTuningButton, RestoreTrailerButton))
        {
            try
            {
                var pakPath = _session.PakPath;
                var trailer = _currentTrailer;
                var result = await Task.Run(() => TrailerTuningService.SaveTrailerChanges(pakPath, trailer));
                _trailersPakPath = null;
                await LoadTuningAsync(_currentCard);
                TuningStatusText.Text = result.UpdatedFiles <= 0
                    ? UiText.Trailers.NoChangesToSave
                    : UiText.Trailers.SavedMessage();
            }
            catch (Exception ex)
            {
                TuningStatusText.Text = UiText.Main.ErrorStatus(ex.Message);
                await AppDialogs.ShowError(owner, ex.Message, UiText.Trailers.SaveErrorTitle);
            }
        }
    }

    private async void RestoreTrailerButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryBeginWrite(
                owner,
                _session,
                _session?.PakPath,
                PakWriteUi.CanWrite(_session),
                requireBaseline: true))
        {
            return;
        }

        if (_currentTrailer is null || string.IsNullOrWhiteSpace(_session?.PakPath) || _currentCard is null)
        {
            TuningStatusText.Text = UiText.Trailers.LoadPakHint;
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, SaveTuningButton, RestoreTrailerButton))
        {
            try
            {
                var pakPath = _session.PakPath;
                var entryPath = _currentTrailer.EntryPath;
                var result = await Task.Run(() => TrailerTuningService.RestoreTrailerFromBaseline(pakPath, entryPath));
                _trailersPakPath = null;
                await LoadTuningAsync(_currentCard);
                TuningStatusText.Text = result.UpdatedFiles <= 0
                    ? UiText.Trailers.NoChangesToSave
                    : UiText.Trailers.RestoredMessage();
            }
            catch (Exception ex)
            {
                TuningStatusText.Text = UiText.Main.ErrorStatus(ex.Message);
                await AppDialogs.ShowError(owner, ex.Message, UiText.Trailers.SaveErrorTitle);
            }
        }
    }

    private bool TryReadForm(
        out int fuel,
        out int water,
        out int repairs,
        out int wheels,
        out int price,
        out int unlockRank,
        out double mass)
    {
        fuel = 0;
        water = 0;
        repairs = 0;
        wheels = 0;
        price = 0;
        unlockRank = 0;
        mass = 0;

        if (_currentTrailer?.HasFuel == true
            && (!int.TryParse(FuelCapacityTextBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out fuel)
                || fuel is < 1 or > 10000))
        {
            TuningStatusText.Text = UiText.Trailers.InvalidFuel;
            return false;
        }

        if (_currentTrailer?.HasWater == true
            && (!int.TryParse(WaterCapacityTextBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out water)
                || water is < 1 or > 10000))
        {
            TuningStatusText.Text = UiText.Trailers.InvalidWater;
            return false;
        }

        if (_currentTrailer?.HasRepairs == true
            && (!int.TryParse(RepairsCapacityTextBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out repairs)
                || repairs is < 0 or > 10_000))
        {
            TuningStatusText.Text = UiText.Trailers.InvalidRepairs;
            return false;
        }

        if (_currentTrailer?.HasWheels == true
            && (!int.TryParse(WheelRepairsTextBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out wheels)
                || wheels is < 0 or > 99))
        {
            TuningStatusText.Text = UiText.Trailers.InvalidWheels;
            return false;
        }

        if (_currentTrailer?.HasMass == true)
        {
            if (!double.TryParse(
                    MassTextBox.Text?.Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out mass)
                || mass is < 0.01 or > 200_000)
            {
                TuningStatusText.Text = UiText.Trailers.InvalidMass;
                return false;
            }

            MassTextBox.Text = TruckTuningService.FormatMass(mass);
        }

        if (_currentTrailer?.HasGameData == true)
        {
            if (!int.TryParse(StorePriceTextBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out price)
                || price is < 0 or > 9_999_999)
            {
                TuningStatusText.Text = UiText.Trailers.InvalidPrice;
                return false;
            }

            if (!int.TryParse(UnlockRankTextBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out unlockRank)
                || unlockRank is < 0 or > 30)
            {
                TuningStatusText.Text = UiText.Trailers.InvalidUnlockRank;
                return false;
            }
        }

        return true;
    }

    private void UnlockRankSlider_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_suppressUnlockRankSync || !_ready || e.Property != RangeBase.ValueProperty)
        {
            return;
        }

        var rank = (int)Math.Round(UnlockRankSlider.Value, MidpointRounding.AwayFromZero);
        _suppressUnlockRankSync = true;
        UnlockRankTextBox.Text = rank.ToString(CultureInfo.InvariantCulture);
        _suppressUnlockRankSync = false;
    }

    private void UnlockRankTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressUnlockRankSync || !_ready)
        {
            return;
        }

        if (!int.TryParse(UnlockRankTextBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank)
            || rank is < 0 or > 30)
        {
            return;
        }

        _suppressUnlockRankSync = true;
        UnlockRankSlider.Value = rank;
        _suppressUnlockRankSync = false;
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e) => NavigateBackToList();

    private void NavigateBackToList()
    {
        _loadVersion++;
        _loadCts?.Cancel();
        SetLoading(false);
        _currentCard = null;
        _currentTrailer = null;
        CrashReportContext.ClearVehicle();
        DetailPanel.IsVisible = false;
        ListPanel.IsVisible = true;
    }

    private void TuningNumericTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_ready || !TuningForm.IsVisible)
        {
            return;
        }

        RefreshSafeRangeHints();
    }

    private void RefreshSafeRangeHints()
    {
        if (_currentTrailer is null)
        {
            return;
        }

        if (_currentTrailer.HasFuel)
        {
            SafeRangeHintPresenter.Refresh(
                FuelSafeRangeHint,
                FuelCapacityTextBox,
                TuningFieldRange.FuelLiters(_currentTrailer.BaselineFuelCapacity));
        }

        if (_currentTrailer.HasMass)
        {
            SafeRangeHintPresenter.Refresh(
                MassSafeRangeHint,
                MassTextBox,
                TuningFieldRange.PhysicsMass(_currentTrailer.BaselineMass));
        }

        if (_currentTrailer.HasWater)
        {
            SafeRangeHintPresenter.Refresh(
                WaterSafeRangeHint,
                WaterCapacityTextBox,
                TuningFieldRange.WaterLiters(_currentTrailer.BaselineWaterCapacity));
        }

        if (_currentTrailer.HasRepairs)
        {
            SafeRangeHintPresenter.Refresh(
                RepairsSafeRangeHint,
                RepairsCapacityTextBox,
                TuningFieldRange.RepairParts(_currentTrailer.BaselineRepairsCapacity));
        }

        if (_currentTrailer.HasWheels)
        {
            SafeRangeHintPresenter.Refresh(
                WheelsSafeRangeHint,
                WheelRepairsTextBox,
                TuningFieldRange.SpareWheels(_currentTrailer.BaselineWheelRepairsCapacity));
        }

        if (_currentTrailer.HasGameData)
        {
            SafeRangeHintPresenter.Refresh(
                PriceSafeRangeHint,
                StorePriceTextBox,
                TuningFieldRange.StorePrice(_currentTrailer.BaselinePrice));
        }
    }

    private sealed class TrailerCard
    {
        private static readonly Dictionary<string, Bitmap?> ThumbCache =
            new(StringComparer.OrdinalIgnoreCase);

        private Bitmap? _image;
        private bool _imageResolved;

        public TrailerCard(TrailerCatalogEntry entry)
        {
            Id = entry.Id;
            DisplayName = entry.DisplayName;
            Hitch = entry.Hitch;
            Function = entry.Function;
            IsQuest = entry.IsQuest;
            ImagePath = entry.ImagePath;
            HitchLabel = UiText.Trailers.HitchName(entry.Hitch);
            FunctionLabel = UiText.Trailers.FunctionName(entry.Function);
            MissionBadgeText = UiText.Trailers.Mission;
            HeaderBrush = TrailerCategoryBrushes.ForHitch(entry.Hitch);
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Hitch { get; }
        public string Function { get; }
        public bool IsQuest { get; }
        public string ImagePath { get; }
        public bool IsMission =>
            IsQuest || Function.Equals("mission", StringComparison.OrdinalIgnoreCase);
        public Bitmap? Image
        {
            get
            {
                if (_imageResolved)
                {
                    return _image;
                }

                _imageResolved = true;
                if (string.IsNullOrWhiteSpace(ImagePath))
                {
                    return null;
                }

                if (!ThumbCache.TryGetValue(ImagePath, out _image))
                {
                    _image = VehicleImages.TryLoadBitmap(ImagePath, decodeWidth: 220);
                    ThumbCache[ImagePath] = _image;
                }

                return _image;
            }
        }
        public string HitchLabel { get; }
        public string FunctionLabel { get; }
        public string MissionBadgeText { get; }
        public AvaloniaMedia.IBrush HeaderBrush { get; }
    }
}
