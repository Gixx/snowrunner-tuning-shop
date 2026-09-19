using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Diagnostics;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Trucks;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Desktop.Audio;
using SnowRunnerTuningShop.Desktop.Vehicles;
using SnowRunnerTuningShop.Localization;
using SnowRunnerTuningShop.Vehicles;
using AvaloniaMedia = Avalonia.Media;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class VehiclesView : UserControl
{
    private readonly List<VehicleCard> _all = [];
    private readonly ObservableCollection<VehicleCard> _visible = [];
    private IReadOnlyDictionary<string, VehicleMetaInfo> _metadata =
        new Dictionary<string, VehicleMetaInfo>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<TruckTuningDefinition> _trucks = [];
    private string? _trucksPakPath;
    private AppSession? _session;
    private VehicleCard? _currentCard;
    private TruckTuningDefinition? _currentTruck;
    private string _filter = "All";
    private bool _ready;
    private bool _suppressUnlockRankSync;
    private bool _suppressRegionFreeSync;
    private int _loadVersion;
    private CancellationTokenSource? _loadCts;
    private string? _basedOnUrl;
    private TruckSoundCatalog? _soundCatalog;
    private string? _soundCatalogPakPath;
    private readonly TruckSoundPreviewPlayer _soundPreview = new();
    private bool _suppressSoundComboSync;

    public VehiclesView()
    {
        InitializeComponent();
        VehiclesItems.ItemsSource = _visible;
        ApplyStaticText();
        Unloaded += (_, _) => _soundPreview.Dispose();
        _soundPreview.PlayingChanged += (_, _) =>
            Dispatcher.UIThread.Post(RefreshSoundPlayButtons);
        DriveCombo.ItemsSource = new LabeledValue<TruckDriveLayout>[]
        {
            new(UiText.Vehicles.DriveRwd, TruckDriveLayout.Rwd),
            new(UiText.Vehicles.DriveAlwaysAwd, TruckDriveLayout.AlwaysAwd),
            new(UiText.Vehicles.DriveSelectableAwd, TruckDriveLayout.SelectableAwd),
        };
        Loaded += VehiclesView_Loaded;
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
        GlobalMultipliersExpander.Header = UiText.Vehicles.GlobalMultipliersTitle;
        GlobalMultipliersHintText.Text = UiText.Vehicles.GlobalMultipliersHint;
        ApplyGlobalMultipliersButton.Content = UiText.Vehicles.ApplyGlobalMultipliers;
        AlwaysOnDiffLockCheckBox.Content = UiText.Vehicles.AlwaysOnDiffLock;
        AlwaysOnAwdCheckBox.Content = UiText.Vehicles.AlwaysOnAwd;
        FuelMultiplierLabel.Text = UiText.Vehicles.FuelMultiplierDefault;
        FrontSteerGlobalLabel.Text = UiText.Vehicles.FrontSteerGlobalDefault;
        ResponsivenessMultiplierLabel.Text = UiText.Vehicles.ResponsivenessMultiplierDefault;
        PriceMultiplierLabel.Text = UiText.Vehicles.PriceMultiplierDefault;

        StoreUnlocksExpander.Header = UiText.Vehicles.StoreUnlocksTitle;
        StoreUnlocksHintText.Text = UiText.Vehicles.StoreUnlocksHint;
        ReleaseRegionLockCheckBox.Content = UiText.Vehicles.ReleaseRegionLock;
        UnlockAllVehiclesCheckBox.Content = UiText.Vehicles.UnlockAllVehicles;
        ApplyStoreUnlocksButton.Content = UiText.Vehicles.ApplyStoreUnlocks;

        FilterAll.Content = UiText.Vehicles.All;
        FilterHighway.Content = UiText.Vehicles.Highway;
        FilterHeavyDuty.Content = UiText.Vehicles.HeavyDuty;
        FilterHeavy.Content = UiText.Vehicles.Heavy;
        FilterOffroad.Content = UiText.Vehicles.Offroad;
        FilterScout.Content = UiText.Vehicles.Scout;
        RestoreAllVehiclesButton.Content = UiText.Vehicles.RestoreAllVehicles;
        SearchTextBox.PlaceholderText = UiText.Vehicles.SearchPlaceholder;

        BackButton.Content = UiText.Vehicles.BackToList;
        BasedOnLabelText.Text = UiText.Vehicles.BasedOnLabel;
        RoleLabelText.Text = UiText.Vehicles.RoleLabel;
        YearsLabelText.Text = UiText.Vehicles.YearsLabel;
        CountryLabelText.Text = UiText.Vehicles.CountryLabel;
        CountryHintText.Text = UiText.Vehicles.CountryHint;
        TuningHintText.Text = UiText.Vehicles.LoadPakHint;
        TuningTitleText.Text = UiText.Vehicles.TuningTitle;
        FuelTankLabelText.Text = UiText.Vehicles.FuelTankLabel;
        FuelUnitText.Text = UiText.Vehicles.FuelUnit;
        StorePriceLabelText.Text = UiText.Vehicles.StorePriceLabel;
        RegionFreeLabelText.Text = UiText.Vehicles.RegionFreeLabel;
        RegionFreeHintText.Text = UiText.Vehicles.RegionFreeHint;
        UnlockRankLabelText.Text = UiText.Vehicles.UnlockRankLabel;
        UnlockRankHintText.Text = UiText.Vehicles.UnlockRankHint;
        FrontSteerLabelText.Text = UiText.Vehicles.FrontSteerLabel;
        FrontSteerUnitText.Text = UiText.Vehicles.SteerAngleUnit;
        FrontSteerHintText.Text = UiText.Vehicles.FrontSteerHint;
        RearSteerLabelText.Text = UiText.Vehicles.RearSteerLabel;
        RearSteerUnitText.Text = UiText.Vehicles.SteerAngleUnit;
        RearSteerHintText.Text = UiText.Vehicles.RearSteerHint;
        ResponsivenessLabelText.Text = UiText.Vehicles.ResponsivenessLabel;
        ResponsivenessHintText.Text = UiText.Vehicles.ResponsivenessHint;
        DiffLockLabelText.Text = UiText.Vehicles.DiffLockLabel;
        DriveLabelText.Text = UiText.Vehicles.DriveLabel;
        DriveHintText.Text = UiText.Vehicles.DriveHint;
        EngineSetsLabelText.Text = UiText.Vehicles.EngineSetsLabel;
        EngineSetsHintText.Text = UiText.Vehicles.EngineSetsHint;
        EngineSetsButton.Content = UiText.Vehicles.EngineSetsButton(0);
        HornSoundLabelText.Text = UiText.Vehicles.HornSoundLabel;
        HornSoundHintText.Text = UiText.Vehicles.HornSoundHint;
        EngineSoundLabelText.Text = UiText.Vehicles.EngineSoundLabel;
        EngineSoundHintText.Text = UiText.Vehicles.EngineSoundHint;
        HornSoundPlayButton.Content = UiText.Vehicles.SoundPlay;
        EngineIdlePlayButton.Content = UiText.Vehicles.SoundPlay;
        EngineHighPlayButton.Content = UiText.Vehicles.SoundPlay;
        ToolTip.SetTip(HornSoundPlayButton, UiText.Vehicles.SoundPlayHornTooltip);
        ToolTip.SetTip(EngineIdlePlayButton, UiText.Vehicles.SoundPlayIdleTooltip);
        ToolTip.SetTip(EngineHighPlayButton, UiText.Vehicles.SoundPlayHighTooltip);
        SaveTuningButton.Content = UiText.Vehicles.SaveChanges;
        RestoreVehicleButton.Content = UiText.Vehicles.RestoreThisVehicle;
        LoadingText.Text = UiText.Parts.Loading;
    }

    private void VehiclesView_Loaded(object? sender, RoutedEventArgs e)
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
        _trucks = [];
        _trucksPakPath = null;
        _soundCatalog = null;
        _soundCatalogPakPath = null;
        _soundPreview.Stop();
        RefreshGlobalMultipliersPanel();
        if (_currentCard is not null && DetailPanel.IsVisible)
        {
            _ = LoadTuningAsync(_currentCard);
        }
        else
        {
            ShowTuningHint(UiText.Vehicles.LoadPakHint);
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
        FrontSteerGlobalSlider.IsEnabled = canApply;
        ResponsivenessMultiplierSlider.IsEnabled = canApply;
        PriceMultiplierSlider.IsEnabled = canApply;
        AlwaysOnDiffLockCheckBox.IsEnabled = canApply;
        AlwaysOnAwdCheckBox.IsEnabled = canApply;
        ReleaseRegionLockCheckBox.IsEnabled = canApply;
        UnlockAllVehiclesCheckBox.IsEnabled = canApply;
        ApplyStoreUnlocksButton.IsEnabled = canApply;
        RestoreAllVehiclesButton.IsEnabled = canApply;

        if (!canApply)
        {
            ResetGlobalMultiplierSlidersToBaseline();
        }

        UpdateGlobalMultiplierLabels();
    }

    private const int FrontSteerGlobalBaselineIndex = 1;

    private void ResetGlobalMultiplierSlidersToBaseline()
    {
        FuelMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        FrontSteerGlobalSlider.Value = FrontSteerGlobalBaselineIndex;
        ResponsivenessMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        PriceMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
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
        FrontSteerGlobalLabel.Text = GetFrontSteerGlobalLabel(GetFrontSteerGlobalIndex(FrontSteerGlobalSlider));
        ResponsivenessMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.Responsiveness,
            GetMultiplierIndex(ResponsivenessMultiplierSlider));
        PriceMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.StorePrice,
            GetMultiplierIndex(PriceMultiplierSlider));
    }

    private static int GetFrontSteerGlobalIndex(Slider slider) =>
        Math.Clamp((int)Math.Round(slider.Value, MidpointRounding.AwayFromZero), 0, 2);

    private static TruckFrontSteerGlobalMode GetFrontSteerGlobalMode(Slider slider) =>
        (TruckFrontSteerGlobalMode)GetFrontSteerGlobalIndex(slider);

    private static string GetFrontSteerGlobalLabel(int index) =>
        index switch
        {
            0 => UiText.Vehicles.FrontSteerGlobalMin,
            2 => UiText.Vehicles.FrontSteerGlobalMax,
            _ => UiText.Vehicles.FrontSteerGlobalDefault,
        };

    private static int GetMultiplierIndex(Slider slider) =>
        TuningMultiplierPresets.ClampIndex((int)Math.Round(slider.Value, MidpointRounding.AwayFromZero));

    private static double GetMultiplier(Slider slider) =>
        TuningMultiplierPresets.GetValue(GetMultiplierIndex(slider));

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
                onMissingPak: () => TuningStatusText.Text = UiText.Vehicles.LoadPakForGlobalHint))
        {
            if (_session?.HasPak != true || string.IsNullOrWhiteSpace(_session.PakPath))
            {
                await AppDialogs.ShowWarning(
                    owner,
                    UiText.Vehicles.LoadPakForGlobalHint,
                    UiText.Vehicles.SaveErrorTitle);
            }

            return;
        }

        using (PakWriteUi.BeginBusyWrite(
                   owner,
                   ApplyGlobalMultipliersButton,
                   ApplyStoreUnlocksButton,
                   RestoreAllVehiclesButton))
        {
            try
            {
                var pakPath = _session!.PakPath!;
                // Capture UI control values on the UI thread before Task.Run.
                var fuel = GetMultiplier(FuelMultiplierSlider);
                var frontSteer = GetFrontSteerGlobalMode(FrontSteerGlobalSlider);
                var responsiveness = GetMultiplier(ResponsivenessMultiplierSlider);
                var price = GetMultiplier(PriceMultiplierSlider);
                var alwaysOnDiffLock = AlwaysOnDiffLockCheckBox.IsChecked == true;
                var alwaysOnAwd = AlwaysOnAwdCheckBox.IsChecked == true;

                var result = await Task.Run(() => TruckTuningService.ApplyGlobalMultipliers(
                    pakPath,
                    fuel,
                    frontSteer,
                    responsiveness,
                    price,
                    alwaysOnDiffLock,
                    alwaysOnAwd));

                _trucksPakPath = null;
                if (_currentCard is not null && DetailPanel.IsVisible)
                {
                    _ = LoadTuningAsync(_currentCard);
                }

                TuningStatusText.Text = UiText.Vehicles.GlobalMultipliersAppliedStatus(
                    result.ChangedTrucks,
                    result.UpdatedFiles);
            }
            catch (Exception ex)
            {
                TuningStatusText.Text = UiText.Main.ErrorStatus(ex.Message);
                await AppDialogs.ShowError(owner, ex.Message, UiText.Vehicles.SaveErrorTitle);
            }
        }
    }

    private async void ApplyStoreUnlocksButton_Click(object? sender, RoutedEventArgs e)
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
                onMissingPak: () => TuningStatusText.Text = UiText.Vehicles.LoadPakForGlobalHint))
        {
            if (_session?.HasPak != true || string.IsNullOrWhiteSpace(_session.PakPath))
            {
                await AppDialogs.ShowWarning(
                    owner,
                    UiText.Vehicles.LoadPakForGlobalHint,
                    UiText.Vehicles.SaveErrorTitle);
            }

            return;
        }

        var releaseRegionLock = ReleaseRegionLockCheckBox.IsChecked == true;
        var unlockAll = UnlockAllVehiclesCheckBox.IsChecked == true;
        if (!releaseRegionLock && !unlockAll)
        {
            await AppDialogs.ShowInfo(
                owner,
                UiText.Vehicles.StoreUnlocksNothingSelected,
                UiText.Vehicles.SaveErrorTitle);
            return;
        }

        using (PakWriteUi.BeginBusyWrite(
                   owner,
                   ApplyGlobalMultipliersButton,
                   ApplyStoreUnlocksButton,
                   RestoreAllVehiclesButton))
        {
            try
            {
                var pakPath = _session!.PakPath!;
                var result = await Task.Run(() => TruckTuningService.ApplyGlobalStoreUnlocks(
                    pakPath,
                    releaseRegionLock,
                    unlockAll));

                _trucksPakPath = null;
                if (_currentCard is not null && DetailPanel.IsVisible)
                {
                    _ = LoadTuningAsync(_currentCard);
                }

                TuningStatusText.Text = UiText.Vehicles.StoreUnlocksSavedMessage(
                    result.ChangedTrucks,
                    result.UpdatedFiles);
            }
            catch (Exception ex)
            {
                TuningStatusText.Text = UiText.Main.ErrorStatus(ex.Message);
                await AppDialogs.ShowError(owner, ex.Message, UiText.Vehicles.SaveErrorTitle);
            }
        }
    }

    private async void RestoreAllVehiclesButton_Click(object? sender, RoutedEventArgs e)
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
                onMissingPak: () => TuningStatusText.Text = UiText.Vehicles.LoadPakForGlobalHint))
        {
            if (_session?.HasPak != true || string.IsNullOrWhiteSpace(_session.PakPath))
            {
                await AppDialogs.ShowWarning(
                    owner,
                    UiText.Vehicles.LoadPakForGlobalHint,
                    UiText.Vehicles.SaveErrorTitle);
            }

            return;
        }

        if (!await AppDialogs.Confirm(
                owner,
                UiText.Vehicles.RestoreAllVehiclesConfirmMessage,
                UiText.Vehicles.RestoreAllVehiclesConfirmTitle))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(
                   owner,
                   ApplyGlobalMultipliersButton,
                   ApplyStoreUnlocksButton,
                   RestoreAllVehiclesButton))
        {
            try
            {
                var pakPath = _session!.PakPath!;
                var result = await Task.Run(() => TruckTuningService.RestoreAllVehiclesFromBaseline(pakPath));
                _trucksPakPath = null;
                ResetGlobalMultiplierSlidersToBaseline();
                ReleaseRegionLockCheckBox.IsChecked = false;
                UnlockAllVehiclesCheckBox.IsChecked = false;
                UpdateGlobalMultiplierLabels();

                TuningStatusText.Text = UiText.Vehicles.RestoreAllVehiclesSavedMessage(
                    result.ChangedTrucks,
                    result.UpdatedFiles);
            }
            catch (Exception ex)
            {
                TuningStatusText.Text = UiText.Main.ErrorStatus(ex.Message);
                await AppDialogs.ShowError(owner, ex.Message, UiText.Vehicles.SaveErrorTitle);
            }
        }
    }

    private void LoadCatalog()
    {
        _all.Clear();
        _metadata = VehicleMetadataLoader.Load();
        var entries = VehicleCatalogLoader.Load();
        if (entries.Count == 0)
        {
            CountTextBlock.Text = UiText.Vehicles.CatalogMissing;
            return;
        }

        var flagCache = new Dictionary<string, Bitmap?>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            _metadata.TryGetValue(entry.Id, out var meta);
            Bitmap? flag = null;
            var oval = "";
            if (meta?.Country is { } country)
            {
                oval = country.OvalCode;
                if (!string.IsNullOrWhiteSpace(country.Code))
                {
                    if (!flagCache.TryGetValue(country.Code, out flag))
                    {
                        flag = VehicleImages.TryLoadBitmap(country.FlagPath, decodeWidth: 56);
                        flagCache[country.Code] = flag;
                    }
                }
            }

            _all.Add(new VehicleCard(
                entry.Id,
                entry.PakId,
                entry.DisplayName,
                entry.Category,
                entry.ImagePath,
                oval,
                flag));
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

        IEnumerable<VehicleCard> query = _all;
        if (!string.Equals(_filter, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(card => card.Category.Equals(_filter, StringComparison.OrdinalIgnoreCase));
        }

        if (search.Length > 0)
        {
            query = query.Where(card =>
                card.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || card.Id.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        _visible.Clear();
        foreach (var card in query.OrderBy(card => card.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            _visible.Add(card);
        }

        CountTextBlock.Text = UiText.Vehicles.CountLabel(_visible.Count);
    }

    private async void VehicleCard_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left
            || sender is not Control element
            || element.DataContext is not VehicleCard card)
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

    private async Task ShowDetailAsync(VehicleCard card)
    {
        CrashReportContext.SetVehicle(card.PakId, card.DisplayName);
        _currentCard = card;
        DetailTitleText.Text = card.DisplayName;
        DetailImage.Source = card.Image;

        _metadata.TryGetValue(card.Id, out var meta);
        BindDetailMetadata(meta, card.Category);

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

    private void BindDetailMetadata(VehicleMetaInfo? meta, string role)
    {
        var manufacturer = meta?.Manufacturer;
        var logo = manufacturer is null
            ? null
            : VehicleImages.TryLoadBitmap(manufacturer.LogoPath, decodeWidth: 160);
        DetailManufacturerLogo.Source = logo;
        ManufacturerLogoPlate.IsVisible = logo is not null;
        ToolTip.SetTip(ManufacturerLogoPlate, manufacturer?.Name);

        var basedOn = VehicleBasedOnFormatter.Parse(meta?.BasedOn);
        var hasBasedOn = basedOn is not null;
        var hasRole = !string.IsNullOrWhiteSpace(role);
        var hasYear = !string.IsNullOrWhiteSpace(meta?.YearDisplay);
        var hasCountry = meta?.Country is not null;
        DetailMetaPanel.IsVisible = hasBasedOn || hasRole || hasYear || hasCountry;

        BasedOnRow.IsVisible = hasBasedOn;
        SetBasedOnDisplay(basedOn);
        BasedOnRow.Margin = hasRole || hasYear || hasCountry
            ? new Thickness(0, 0, 0, 10)
            : new Thickness(0);

        RoleRow.IsVisible = hasRole;
        DetailRoleText.Text = UiText.Vehicles.CategoryDisplay(role);
        RoleRow.Margin = hasYear || hasCountry
            ? new Thickness(0, 0, 0, 10)
            : new Thickness(0);

        YearsRow.IsVisible = hasYear;
        DetailYearsText.Text = meta?.YearDisplay ?? "";
        YearsRow.Margin = hasCountry
            ? new Thickness(0, 0, 0, 10)
            : new Thickness(0);

        CountryRow.IsVisible = hasCountry;
        if (hasCountry)
        {
            var country = meta!.Country!;
            var flag = VehicleImages.TryLoadBitmap(country.FlagPath, decodeWidth: 56);
            DetailCountryFlag.Source = flag;
            DetailCountryFlag.IsVisible = flag is not null;
            DetailCountryName.Text = UiText.Vehicles.CountryDisplay(country.Code, country.Name);
        }
        else
        {
            DetailCountryFlag.Source = null;
            DetailCountryFlag.IsVisible = false;
            DetailCountryName.Text = "";
        }
    }

    private async Task LoadTuningAsync(VehicleCard card)
    {
        var version = ++_loadVersion;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var cancellationToken = _loadCts.Token;

        _currentTruck = null;
        TuningStatusText.Text = "";
        RefreshRestoreButton();

        if (_session?.HasPak != true || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            ShowTuningHint(UiText.Vehicles.LoadPakHint);
            return;
        }

        var pakPath = _session.PakPath;
        var needsLoad = !string.Equals(_trucksPakPath, pakPath, StringComparison.OrdinalIgnoreCase)
            || _trucks.Count == 0;
        if (needsLoad)
        {
            SetLoading(true);
        }

        try
        {
            try
            {
                await EnsureTrucksLoadedAsync(pakPath, cancellationToken);
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

            var truck = TruckTuningService.FindByCatalog(_trucks, card.Id, card.PakId);
            if (truck is null)
            {
                ShowTuningHint(UiText.Vehicles.TruckNotFound);
                return;
            }

            _currentTruck = truck;
            FuelCapacityTextBox.Text = truck.FuelCapacity.ToString(CultureInfo.InvariantCulture);
            StorePriceTextBox.Text = truck.Price.ToString(CultureInfo.InvariantCulture);
            ResponsivenessTextBox.Text = TruckTuningService.FormatResponsiveness(truck.Responsiveness);
            BindStoreUnlockFields(truck);
            FrontSteerRow.IsVisible = truck.HasFrontSteer;
            FrontSteerHintText.IsVisible = truck.HasFrontSteer;
            if (truck.HasFrontSteer && truck.FrontSteerAngle is { } frontAngle)
            {
                FrontSteerTextBox.Text = frontAngle.ToString("0.######", CultureInfo.InvariantCulture);
            }
            else
            {
                FrontSteerTextBox.Text = "";
            }

            RearSteerRow.IsVisible = truck.HasRearSteer;
            RearSteerHintText.IsVisible = truck.HasRearSteer;
            if (truck.HasRearSteer && truck.RearSteerAngle is { } rearAngle)
            {
                RearSteerTextBox.Text = rearAngle.ToString("0.######", CultureInfo.InvariantCulture);
            }
            else
            {
                RearSteerTextBox.Text = "";
            }

            BindDiffLockOptions(truck);
            SelectDrive(truck.DriveLayout);
            RefreshEngineSetsButton(truck);
            await BindSoundCombosAsync(truck);
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

    private async Task EnsureTrucksLoadedAsync(string pakPath, CancellationToken cancellationToken = default)
    {
        if (string.Equals(_trucksPakPath, pakPath, StringComparison.OrdinalIgnoreCase) && _trucks.Count > 0)
        {
            return;
        }

        var language = AppLanguage.Current;
        var trucks = await Task.Run(() => TruckTuningService.LoadTrucks(pakPath, language), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _trucks = trucks;
        _trucksPakPath = pakPath;
    }

    private void SetLoading(bool isLoading) => LoadingOverlay.IsVisible = isLoading;

    private void ShowTuningHint(string message)
    {
        TuningForm.IsVisible = false;
        TuningHintText.Text = message;
        TuningHintText.IsVisible = true;
        RestoreVehicleButton.IsEnabled = false;
    }

    private void BindDiffLockOptions(TruckTuningDefinition truck)
    {
        LabeledValue<TruckDiffLockMode>[] options;
        if (truck.HasNativeDiffLockOptions)
        {
            options =
            [
                new(UiText.Vehicles.DiffLockAlwaysOn, TruckDiffLockMode.AlwaysOn),
                new(UiText.Vehicles.DiffLockSwitchable, TruckDiffLockMode.Switchable),
                new(UiText.Vehicles.DiffLockUpgradeable, TruckDiffLockMode.Upgradeable),
                new(UiText.Vehicles.DiffLockNone, TruckDiffLockMode.None),
            ];
            DiffLockHintText.Text = UiText.Vehicles.DiffLockHintNative;
        }
        else
        {
            options =
            [
                new(UiText.Vehicles.DiffLockNone, TruckDiffLockMode.None),
                new(UiText.Vehicles.DiffLockAlwaysOn, TruckDiffLockMode.AlwaysOn),
            ];
            DiffLockHintText.Text = UiText.Vehicles.DiffLockHintSimple;
        }

        DiffLockCombo.ItemsSource = options;

        var mode = truck.DiffLock;
        if (!truck.HasNativeDiffLockOptions
            && mode is TruckDiffLockMode.Switchable or TruckDiffLockMode.Upgradeable)
        {
            mode = TruckDiffLockMode.None;
        }

        DiffLockCombo.SelectedItem = options.FirstOrDefault(option => Equals(option.Value, mode));
    }

    private void SelectDrive(TruckDriveLayout drive)
    {
        if (DriveCombo.ItemsSource is IEnumerable<LabeledValue<TruckDriveLayout>> items)
        {
            DriveCombo.SelectedItem = items.FirstOrDefault(item => Equals(item.Value, drive));
        }
    }

    private void RefreshRestoreButton()
    {
        var canWrite = PakWriteUi.CanWrite(_session);
        RestoreVehicleButton.IsEnabled = _currentTruck is not null
            && !string.IsNullOrWhiteSpace(_session?.PakPath)
            && PakBaselineService.HasBaseline(_session.PakPath)
            && canWrite;
        SaveTuningButton.IsEnabled = _currentTruck is not null && canWrite;
        EngineSetsButton.IsEnabled = _currentTruck is not null && canWrite;
    }

    private void RefreshEngineSetsButton(TruckTuningDefinition truck)
    {
        EngineSetsButton.Content = UiText.Vehicles.EngineSetsButton(0);
        if (string.IsNullOrWhiteSpace(_session?.PakPath))
        {
            return;
        }

        try
        {
            var assigned = TruckEngineSetsService.GetAssignedSetIds(_session.PakPath, truck.EntryPath);
            var hasSocket = TruckEngineSetsService.HasEngineSocket(_session.PakPath, truck.EntryPath);
            EngineSetsButton.Content = UiText.Vehicles.EngineSetsButton(assigned.Count);
            EngineSetsButton.IsEnabled = hasSocket && PakWriteUi.CanWrite(_session);
        }
        catch (Exception ex)
        {
            TuningStatusText.Text = ex.Message;
            EngineSetsButton.IsEnabled = false;
        }
    }

    private async Task BindSoundCombosAsync(TruckTuningDefinition truck)
    {
        if (string.IsNullOrWhiteSpace(_session?.PakPath))
        {
            return;
        }

        var pakPath = _session.PakPath;
        if (!string.Equals(_soundCatalogPakPath, pakPath, StringComparison.OrdinalIgnoreCase) || _soundCatalog is null)
        {
            _soundCatalog = await Task.Run(() => TruckSoundsService.LoadCatalog(pakPath));
            _soundCatalogPakPath = pakPath;
        }

        var canPreview = TruckSoundPreviewPlayer.IsSupported
            && TruckSoundsService.CanPreviewSounds(pakPath);
        HornSoundPlayButton.IsVisible = canPreview;
        EngineIdlePlayButton.IsVisible = canPreview;
        EngineHighPlayButton.IsVisible = canPreview;

        _suppressSoundComboSync = true;
        try
        {
            HornSoundCombo.ItemsSource = _soundCatalog.HornSetIds.ToArray();
            EngineSoundCombo.ItemsSource = _soundCatalog.EngineSetIds.ToArray();
            HornSoundCombo.SelectedItem = truck.HornSoundSetId;
            EngineSoundCombo.SelectedItem = truck.EngineSoundSetId;
        }
        finally
        {
            _suppressSoundComboSync = false;
        }

        RefreshSoundPlayButtons();
    }

    private void HornSoundCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSoundComboSync || _currentTruck is null)
        {
            return;
        }

        _currentTruck.HornSoundSetId = HornSoundCombo.SelectedItem as string;
        RefreshSoundPlayButtons();
    }

    private void EngineSoundCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSoundComboSync || _currentTruck is null)
        {
            return;
        }

        _currentTruck.EngineSoundSetId = EngineSoundCombo.SelectedItem as string;
        RefreshSoundPlayButtons();
    }

    private void HornSoundPlayButton_Click(object? sender, RoutedEventArgs e) =>
        ToggleSoundPreview("horn", "Honk", HornSoundCombo.SelectedItem as string);

    private void EngineIdlePlayButton_Click(object? sender, RoutedEventArgs e) =>
        ToggleSoundPreview("idle", "EngineIdle", EngineSoundCombo.SelectedItem as string);

    private void EngineHighPlayButton_Click(object? sender, RoutedEventArgs e) =>
        ToggleSoundPreview("high", "EngineHigh", EngineSoundCombo.SelectedItem as string);

    private void ToggleSoundPreview(string key, string tag, string? soundSetId)
    {
        if (string.IsNullOrWhiteSpace(_session?.PakPath) || _soundCatalog is null || string.IsNullOrWhiteSpace(soundSetId))
        {
            return;
        }

        var logical = TruckSoundsService.ResolvePreviewPath(_soundCatalog, soundSetId, tag);
        if (logical is null
            || !TruckSoundsService.TryReadSoundWav(_session.PakPath, logical, out var wav))
        {
            TuningStatusText.Text = UiText.Vehicles.SoundPreviewUnavailable;
            return;
        }

        try
        {
            _soundPreview.Toggle(key, wav);
            RefreshSoundPlayButtons();
        }
        catch (Exception ex)
        {
            TuningStatusText.Text = UiText.Main.ErrorStatus(ex.Message);
        }
    }

    private void RefreshSoundPlayButtons()
    {
        HornSoundPlayButton.Content = _soundPreview.IsPlaying("horn")
            ? UiText.Vehicles.SoundStop
            : UiText.Vehicles.SoundPlay;
        EngineIdlePlayButton.Content = _soundPreview.IsPlaying("idle")
            ? UiText.Vehicles.SoundStop
            : UiText.Vehicles.SoundPlay;
        EngineHighPlayButton.Content = _soundPreview.IsPlaying("high")
            ? UiText.Vehicles.SoundStop
            : UiText.Vehicles.SoundPlay;
    }

    private async void EngineSetsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentTruck is null || string.IsNullOrWhiteSpace(_session?.PakPath) || _currentCard is null)
        {
            return;
        }

        var owner = OwnerWindow;
        if (owner is null)
        {
            return;
        }

        if (!await PakWriteUi.TryProceed(owner, _session))
        {
            return;
        }

        TruckEngineSetsSnapshot snapshot;
        try
        {
            var pakPath = _session.PakPath;
            var entryPath = _currentTruck.EntryPath;
            snapshot = await Task.Run(() => TruckEngineSetsService.Load(pakPath, entryPath, AppLanguage.Current));
        }
        catch (Exception ex)
        {
            TuningStatusText.Text = ex.Message;
            return;
        }

        var dialog = new TruckEngineSetsWindow(snapshot);
        var applied = await dialog.ShowDialog<bool?>(owner);
        if (applied != true)
        {
            return;
        }

        try
        {
            using (PakWriteUi.BeginBusyWrite(owner, SaveTuningButton, RestoreVehicleButton, EngineSetsButton))
            {
                var pakPath = _session.PakPath;
                var entryPath = _currentTruck.EntryPath;
                var selected = dialog.SelectedSetIds;
                await Task.Run(() => TruckEngineSetsService.Apply(pakPath, entryPath, selected));
            }

            TuningStatusText.Text = UiText.Vehicles.EngineSetsSavedStatus;
            await LoadTuningAsync(_currentCard);
        }
        catch (Exception ex)
        {
            TuningStatusText.Text = ex.Message;
        }
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

        if (_currentTruck is null || string.IsNullOrWhiteSpace(_session?.PakPath) || _currentCard is null)
        {
            TuningStatusText.Text = UiText.Vehicles.LoadPakHint;
            return;
        }

        if (!TryReadForm(
                out var fuel,
                out var price,
                out var storeCountries,
                out var unlockRank,
                out var diffLock,
                out var drive,
                out var responsiveness,
                out var frontSteer,
                out var rearSteer))
        {
            return;
        }

        _currentTruck.FuelCapacity = fuel;
        _currentTruck.Price = price;
        _currentTruck.StoreCountries = storeCountries;
        _currentTruck.UnlockByRank = unlockRank;
        _currentTruck.DiffLock = diffLock;
        _currentTruck.DriveLayout = drive;
        _currentTruck.Responsiveness = responsiveness;
        _currentTruck.FrontSteerAngle = frontSteer;
        _currentTruck.RearSteerAngle = rearSteer;
        _currentTruck.HornSoundSetId = HornSoundCombo.SelectedItem as string;
        _currentTruck.EngineSoundSetId = EngineSoundCombo.SelectedItem as string;

        using (PakWriteUi.BeginBusyWrite(owner, SaveTuningButton, RestoreVehicleButton))
        {
            try
            {
                var pakPath = _session.PakPath;
                var truck = _currentTruck;
                var result = await Task.Run(() => TruckTuningService.SaveTruckChanges(pakPath, truck));
                _trucksPakPath = null;
                await LoadTuningAsync(_currentCard);
                TuningStatusText.Text = result.UpdatedFiles <= 0
                    ? UiText.Vehicles.NoChangesToSave
                    : UiText.Vehicles.SavedMessage();
            }
            catch (Exception ex)
            {
                TuningStatusText.Text = UiText.Main.ErrorStatus(ex.Message);
                await AppDialogs.ShowError(owner, ex.Message, UiText.Vehicles.SaveErrorTitle);
            }
        }
    }

    private async void RestoreVehicleButton_Click(object? sender, RoutedEventArgs e)
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

        if (_currentTruck is null || string.IsNullOrWhiteSpace(_session?.PakPath) || _currentCard is null)
        {
            TuningStatusText.Text = UiText.Vehicles.LoadPakHint;
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, SaveTuningButton, RestoreVehicleButton))
        {
            try
            {
                var pakPath = _session.PakPath;
                var entryPath = _currentTruck.EntryPath;
                var result = await Task.Run(() => TruckTuningService.RestoreTruckFromBaseline(pakPath, entryPath));
                _trucksPakPath = null;
                await LoadTuningAsync(_currentCard);
                TuningStatusText.Text = result.UpdatedFiles <= 0
                    ? UiText.Vehicles.NoChangesToSave
                    : UiText.Vehicles.RestoredMessage();
            }
            catch (Exception ex)
            {
                TuningStatusText.Text = UiText.Main.ErrorStatus(ex.Message);
                await AppDialogs.ShowError(owner, ex.Message, UiText.Vehicles.SaveErrorTitle);
            }
        }
    }

    private bool TryReadForm(
        out int fuel,
        out int price,
        out string storeCountries,
        out int unlockRank,
        out TruckDiffLockMode diffLock,
        out TruckDriveLayout drive,
        out double responsiveness,
        out double? frontSteer,
        out double? rearSteer)
    {
        fuel = 0;
        price = 0;
        storeCountries = "";
        unlockRank = 0;
        diffLock = TruckDiffLockMode.Switchable;
        drive = TruckDriveLayout.AlwaysAwd;
        responsiveness = 0;
        frontSteer = null;
        rearSteer = null;

        if (!int.TryParse(FuelCapacityTextBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out fuel)
            || fuel is < 1 or > 10000)
        {
            TuningStatusText.Text = UiText.Vehicles.InvalidFuel;
            return false;
        }

        if (!int.TryParse(StorePriceTextBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out price)
            || price is < 0 or > 9_999_999)
        {
            TuningStatusText.Text = UiText.Vehicles.InvalidPrice;
            return false;
        }

        if (!int.TryParse(UnlockRankTextBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out unlockRank)
            || unlockRank is < 0 or > 30)
        {
            TuningStatusText.Text = UiText.Vehicles.InvalidUnlockRank;
            return false;
        }

        storeCountries = _currentTruck?.StoreCountries ?? "";
        if (RegionFreeCheckBox.IsChecked == true)
        {
            storeCountries = TruckStoreRegions.AllCountriesAttributeValue;
        }
        else if (string.IsNullOrWhiteSpace(storeCountries))
        {
            storeCountries = _currentTruck?.BaselineStoreCountries ?? "";
        }

        if (!double.TryParse(
                ResponsivenessTextBox.Text?.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out responsiveness)
            || responsiveness is < 0 or > 1)
        {
            TuningStatusText.Text = UiText.Vehicles.InvalidResponsiveness;
            return false;
        }

        ResponsivenessTextBox.Text = TruckTuningService.FormatResponsiveness(responsiveness);

        if (_currentTruck?.HasFrontSteer == true)
        {
            if (!double.TryParse(
                    FrontSteerTextBox.Text?.Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsedFront)
                || parsedFront is < 0 or > 90)
            {
                TuningStatusText.Text = UiText.Vehicles.InvalidFrontSteer;
                return false;
            }

            frontSteer = parsedFront;
        }

        if (_currentTruck?.HasRearSteer == true)
        {
            if (!double.TryParse(
                    RearSteerTextBox.Text?.Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsedRear)
                || parsedRear is < -90 or > 0)
            {
                TuningStatusText.Text = UiText.Vehicles.InvalidRearSteer;
                return false;
            }

            rearSteer = parsedRear;
        }

        if (DiffLockCombo.SelectedItem is not LabeledValue<TruckDiffLockMode> selectedDiff
            || DriveCombo.SelectedItem is not LabeledValue<TruckDriveLayout> selectedDrive)
        {
            TuningStatusText.Text = UiText.Vehicles.LoadPakHint;
            return false;
        }

        var diffMode = selectedDiff.Value;
        if (_currentTruck is not null
            && !_currentTruck.HasNativeDiffLockOptions
            && diffMode is TruckDiffLockMode.Switchable or TruckDiffLockMode.Upgradeable)
        {
            diffMode = TruckDiffLockMode.None;
        }

        diffLock = diffMode;
        drive = selectedDrive.Value;
        return true;
    }

    private void BindStoreUnlockFields(TruckTuningDefinition truck)
    {
        _suppressRegionFreeSync = true;
        RegionFreeCheckBox.IsChecked = truck.IsRegionFree;
        _suppressRegionFreeSync = false;
        RefreshStoreRegionsLabel(truck);

        _suppressUnlockRankSync = true;
        UnlockRankSlider.Value = truck.UnlockByRank;
        UnlockRankTextBox.Text = truck.UnlockByRank.ToString(CultureInfo.InvariantCulture);
        _suppressUnlockRankSync = false;
    }

    private void RefreshStoreRegionsLabel(TruckTuningDefinition truck)
    {
        var source = RegionFreeCheckBox.IsChecked == true
            ? TruckStoreRegions.AllCountriesAttributeValue
            : (string.IsNullOrWhiteSpace(truck.StoreCountries)
                ? truck.BaselineStoreCountries
                : truck.StoreCountries);

        var formatted = TruckStoreRegions.FormatLockedRegions(source);
        StoreRegionsText.Text = string.IsNullOrWhiteSpace(formatted)
            ? $"{UiText.Vehicles.StoreRegionsLabel}: —"
            : $"{UiText.Vehicles.StoreRegionsLabel}: {formatted}";
    }

    private void RegionFreeCheckBox_Changed(object? sender, RoutedEventArgs e)
    {
        if (_suppressRegionFreeSync || _currentTruck is null)
        {
            return;
        }

        if (RegionFreeCheckBox.IsChecked == true)
        {
            _currentTruck.StoreCountries = TruckStoreRegions.AllCountriesAttributeValue;
        }
        else
        {
            _currentTruck.StoreCountries = _currentTruck.BaselineStoreCountries;
        }

        RefreshStoreRegionsLabel(_currentTruck);
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
        _currentTruck = null;
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
        if (_currentTruck is null)
        {
            return;
        }

        SafeRangeHintPresenter.Refresh(
            FuelSafeRangeHint,
            FuelCapacityTextBox,
            TuningFieldRange.FuelLiters(_currentTruck.BaselineFuelCapacity));
        SafeRangeHintPresenter.Refresh(
            PriceSafeRangeHint,
            StorePriceTextBox,
            TuningFieldRange.StorePrice(_currentTruck.BaselinePrice));

        if (_currentTruck.HasFrontSteer)
        {
            FrontSteerSafeRangeHint.IsVisible = true;
            SafeRangeHintPresenter.Refresh(
                FrontSteerSafeRangeHint,
                FrontSteerTextBox,
                TuningFieldRange.FrontSteerDegrees(_currentTruck.BaselineFrontSteerAngle));
        }
        else
        {
            FrontSteerSafeRangeHint.IsVisible = false;
        }

        if (_currentTruck.HasRearSteer)
        {
            RearSteerSafeRangeHint.IsVisible = true;
            SafeRangeHintPresenter.Refresh(
                RearSteerSafeRangeHint,
                RearSteerTextBox,
                TuningFieldRange.RearSteerDegrees(_currentTruck.BaselineRearSteerAngle));
        }
        else
        {
            RearSteerSafeRangeHint.IsVisible = false;
        }

        SafeRangeHintPresenter.Refresh(
            ResponsivenessSafeRangeHint,
            ResponsivenessTextBox,
            TuningFieldRange.Responsiveness(_currentTruck.BaselineResponsiveness));
    }

    private void SetBasedOnDisplay(VehicleBasedOnFormatter.ParsedBasedOn? basedOn)
    {
        _basedOnUrl = null;
        DetailBasedOnText.IsVisible = false;
        DetailBasedOnLink.IsVisible = false;
        DetailBasedOnText.Text = "";
        DetailBasedOnLink.Content = null;

        if (basedOn is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(basedOn.Url))
        {
            DetailBasedOnText.Text = basedOn.DisplayText;
            DetailBasedOnText.IsVisible = true;
            return;
        }

        _basedOnUrl = basedOn.Url;
        DetailBasedOnLink.Content = basedOn.DisplayText;
        DetailBasedOnLink.IsVisible = true;
    }

    private async void DetailBasedOnLink_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner || string.IsNullOrWhiteSpace(_basedOnUrl))
        {
            return;
        }

        await AppDialogs.OpenUrl(owner, _basedOnUrl);
    }

    public sealed record LabeledValue<T>(string Label, T Value)
    {
        public override string ToString() => Label;
    }

    private sealed class VehicleCard
    {
        private static readonly Dictionary<string, Bitmap?> ThumbCache =
            new(StringComparer.OrdinalIgnoreCase);

        private Bitmap? _image;
        private bool _imageResolved;
        private readonly string _imagePath;

        public VehicleCard(
            string id,
            string pakId,
            string displayName,
            string category,
            string imagePath,
            string ovalCode,
            Bitmap? flag)
        {
            Id = id;
            PakId = pakId;
            DisplayName = displayName;
            Category = category;
            _imagePath = imagePath ?? "";
            OvalCode = ovalCode;
            Flag = flag;
            HeaderBrush = VehicleCategoryBrushes.ForCategory(category);
        }

        public string Id { get; }
        public string PakId { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public Bitmap? Image
        {
            get
            {
                if (_imageResolved)
                {
                    return _image;
                }

                _imageResolved = true;
                if (string.IsNullOrWhiteSpace(_imagePath))
                {
                    return null;
                }

                if (!ThumbCache.TryGetValue(_imagePath, out _image))
                {
                    _image = VehicleImages.TryLoadBitmap(_imagePath, decodeWidth: 220);
                    ThumbCache[_imagePath] = _image;
                }

                return _image;
            }
        }
        public string OvalCode { get; }
        public Bitmap? Flag { get; }
        public AvaloniaMedia.IBrush HeaderBrush { get; }
        public bool HasOval => !string.IsNullOrWhiteSpace(OvalCode);
        public bool HasFlag => Flag is not null;
    }
}
