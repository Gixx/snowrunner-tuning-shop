using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using SnowRunnerTuningShop.Controls;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Diagnostics;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Trucks;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Localization;
using SnowRunnerTuningShop.Vehicles;

namespace SnowRunnerTuningShop.Views;

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

    public VehiclesView()
    {
        InitializeComponent();
        VehiclesItems.ItemsSource = _visible;
        Loaded += VehiclesView_Loaded;

        DiffLockCombo.DisplayMemberPath = nameof(LabeledValue<TruckDiffLockMode>.Label);
        DiffLockCombo.SelectedValuePath = nameof(LabeledValue<TruckDiffLockMode>.Value);

        DriveCombo.DisplayMemberPath = nameof(LabeledValue<TruckDriveLayout>.Label);
        DriveCombo.SelectedValuePath = nameof(LabeledValue<TruckDriveLayout>.Value);
        DriveCombo.ItemsSource = new LabeledValue<TruckDriveLayout>[]
        {
            new(UiText.Vehicles.DriveRwd, TruckDriveLayout.Rwd),
            new(UiText.Vehicles.DriveAlwaysAwd, TruckDriveLayout.AlwaysAwd),
            new(UiText.Vehicles.DriveSelectableAwd, TruckDriveLayout.SelectableAwd),
        };
    }

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

    private void VehiclesView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_ready)
        {
            return;
        }

        _ready = true;
        FilterAll.IsChecked = true;
        LoadCatalog();
        UpdateSearchPlaceholder();
        RefreshGlobalMultipliersPanel();
    }

    private void OnPakChanged()
    {
        _trucks = [];
        _trucksPakPath = null;
        RefreshGlobalMultipliersPanel();
        if (_currentCard is not null && DetailPanel.Visibility == Visibility.Visible)
        {
            LoadTuning(_currentCard);
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

    private void GlobalMultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready)
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

    private void ApplyGlobalMultipliersButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryProceed(_session))
        {
            return;
        }

        if (_session?.HasPak != true || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            MessageBox.Show(
                UiText.Vehicles.LoadPakForGlobalHint,
                UiText.Vehicles.SaveErrorTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!PakBaselineService.HasBaseline(_session.PakPath))
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
            var result = TruckTuningService.ApplyGlobalMultipliers(
                _session.PakPath,
                GetMultiplier(FuelMultiplierSlider),
                GetFrontSteerGlobalMode(FrontSteerGlobalSlider),
                GetMultiplier(ResponsivenessMultiplierSlider),
                GetMultiplier(PriceMultiplierSlider),
                AlwaysOnDiffLockCheckBox.IsChecked == true,
                AlwaysOnAwdCheckBox.IsChecked == true);

            _trucksPakPath = null;
            if (_currentCard is not null && DetailPanel.Visibility == Visibility.Visible)
            {
                LoadTuning(_currentCard);
            }

            MessageBox.Show(
                UiText.Vehicles.GlobalMultipliersSavedMessage(result.ChangedTrucks, result.UpdatedFiles),
                UiText.Vehicles.SaveSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Vehicles.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyStoreUnlocksButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryProceed(_session))
        {
            return;
        }

        if (_session?.HasPak != true || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            MessageBox.Show(
                UiText.Vehicles.LoadPakForGlobalHint,
                UiText.Vehicles.SaveErrorTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!PakBaselineService.HasBaseline(_session.PakPath))
        {
            MessageBox.Show(
                UiText.Main.BaselineMissingShort,
                UiText.Main.BaselineTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var releaseRegionLock = ReleaseRegionLockCheckBox.IsChecked == true;
        var unlockAll = UnlockAllVehiclesCheckBox.IsChecked == true;
        if (!releaseRegionLock && !unlockAll)
        {
            MessageBox.Show(
                UiText.Vehicles.StoreUnlocksNothingSelected,
                UiText.Vehicles.SaveErrorTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            var result = TruckTuningService.ApplyGlobalStoreUnlocks(
                _session.PakPath,
                releaseRegionLock,
                unlockAll);

            _trucksPakPath = null;
            if (_currentCard is not null && DetailPanel.Visibility == Visibility.Visible)
            {
                LoadTuning(_currentCard);
            }

            MessageBox.Show(
                UiText.Vehicles.StoreUnlocksSavedMessage(result.ChangedTrucks, result.UpdatedFiles),
                UiText.Vehicles.SaveSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Vehicles.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RestoreAllVehiclesButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryProceed(_session))
        {
            return;
        }

        if (_session?.HasPak != true || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            MessageBox.Show(
                UiText.Vehicles.LoadPakForGlobalHint,
                UiText.Vehicles.SaveErrorTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!PakBaselineService.HasBaseline(_session.PakPath))
        {
            MessageBox.Show(
                UiText.Main.BaselineMissingShort,
                UiText.Main.BaselineTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            UiText.Vehicles.RestoreAllVehiclesConfirmMessage,
            UiText.Vehicles.RestoreAllVehiclesConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var result = TruckTuningService.RestoreAllVehiclesFromBaseline(_session.PakPath);
            _trucksPakPath = null;
            ResetGlobalMultiplierSlidersToBaseline();
            ReleaseRegionLockCheckBox.IsChecked = false;
            UnlockAllVehiclesCheckBox.IsChecked = false;
            UpdateGlobalMultiplierLabels();

            MessageBox.Show(
                UiText.Vehicles.RestoreAllVehiclesSavedMessage(result.ChangedTrucks, result.UpdatedFiles),
                UiText.Vehicles.RestoreAllVehiclesSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Vehicles.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadCatalog()
    {
        _all.Clear();
        _metadata = VehicleMetadata.Load();
        var entries = VehicleCatalog.Load();
        if (entries.Count == 0)
        {
            CountTextBlock.Text = UiText.Vehicles.CatalogMissing;
            return;
        }

        var flagCache = new Dictionary<string, BitmapImage?>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            _metadata.TryGetValue(entry.Id, out var meta);
            BitmapImage? flag = null;
            var oval = "";
            if (meta?.Country is { } country)
            {
                oval = country.OvalCode;
                if (!string.IsNullOrWhiteSpace(country.Code))
                {
                    if (!flagCache.TryGetValue(country.Code, out flag))
                    {
                        flag = VehicleMetadata.TryLoadImage(country.FlagPath, decodePixelWidth: 56);
                        flagCache[country.Code] = flag;
                    }
                }
            }

            _all.Add(new VehicleCard(
                entry.Id,
                entry.PakId,
                entry.DisplayName,
                entry.Category,
                VehicleCatalog.TryLoadImage(entry.ImagePath),
                oval,
                flag));
        }

        ApplyFilter();
    }

    private void Filter_Checked(object sender, RoutedEventArgs e)
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

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchPlaceholder();
        if (_ready)
        {
            ApplyFilter();
        }
    }

    private void UpdateSearchPlaceholder()
    {
        SearchPlaceholder.Visibility = string.IsNullOrWhiteSpace(SearchTextBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
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

    private void VehicleCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not VehicleCard card)
        {
            return;
        }

        try
        {
            ShowDetail(card);
        }
        catch (Exception ex)
        {
            GlobalExceptionHandler.Handle(ex, isTerminating: false);
        }
    }

    private void ShowDetail(VehicleCard card)
    {
        CrashReportContext.SetVehicle(card.PakId, card.DisplayName);
        _currentCard = card;
        DetailTitleText.Text = card.DisplayName;
        DetailImage.Source = card.Image;

        _metadata.TryGetValue(card.Id, out var meta);
        BindDetailMetadata(meta, card.Category);
        LoadTuning(card);

        ListPanel.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
    }

    private void BindDetailMetadata(VehicleMetaInfo? meta, string role)
    {
        var manufacturer = meta?.Manufacturer;
        var logo = manufacturer is null
            ? null
            : VehicleMetadata.TryLoadImage(manufacturer.LogoPath, decodePixelWidth: 160);
        DetailManufacturerLogo.Source = logo;
        ManufacturerLogoPlate.Visibility = logo is null ? Visibility.Collapsed : Visibility.Visible;
        ManufacturerLogoPlate.ToolTip = manufacturer?.Name;

        var basedOn = VehicleBasedOnFormatter.Parse(meta?.BasedOn);
        var hasBasedOn = basedOn is not null;
        var hasRole = !string.IsNullOrWhiteSpace(role);
        var hasYear = !string.IsNullOrWhiteSpace(meta?.YearDisplay);
        var hasCountry = meta?.Country is not null;
        DetailMetaPanel.Visibility = hasBasedOn || hasRole || hasYear || hasCountry
            ? Visibility.Visible
            : Visibility.Collapsed;

        BasedOnRow.Visibility = hasBasedOn ? Visibility.Visible : Visibility.Collapsed;
        SetBasedOnDisplay(basedOn);
        BasedOnRow.Margin = hasRole || hasYear || hasCountry ? new Thickness(0, 0, 0, 10) : new Thickness(0);

        RoleRow.Visibility = hasRole ? Visibility.Visible : Visibility.Collapsed;
        DetailRoleText.Text = UiText.Vehicles.CategoryDisplay(role);
        RoleRow.Margin = hasYear || hasCountry ? new Thickness(0, 0, 0, 10) : new Thickness(0);

        YearsRow.Visibility = hasYear ? Visibility.Visible : Visibility.Collapsed;
        DetailYearsText.Text = meta?.YearDisplay ?? "";
        YearsRow.Margin = hasCountry ? new Thickness(0, 0, 0, 10) : new Thickness(0);

        CountryRow.Visibility = hasCountry ? Visibility.Visible : Visibility.Collapsed;
        if (hasCountry)
        {
            var country = meta!.Country!;
            DetailCountryFlag.Source = VehicleMetadata.TryLoadImage(country.FlagPath, decodePixelWidth: 56);
            DetailCountryFlag.Visibility = DetailCountryFlag.Source is null
                ? Visibility.Collapsed
                : Visibility.Visible;
            DetailCountryName.Text = UiText.Vehicles.CountryDisplay(country.Code, country.Name);
        }
        else
        {
            DetailCountryFlag.Source = null;
            DetailCountryName.Text = "";
        }
    }

    private void LoadTuning(VehicleCard card)
    {
        _currentTruck = null;
        TuningStatusText.Text = "";
        RefreshRestoreButton();

        if (_session?.HasPak != true || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            ShowTuningHint(UiText.Vehicles.LoadPakHint);
            return;
        }

        try
        {
            EnsureTrucksLoaded(_session.PakPath);
        }
        catch (Exception ex)
        {
            ShowTuningHint(ex.Message);
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
        ResponsivenessTextBox.Text = truck.Responsiveness.ToString("0.######", CultureInfo.InvariantCulture);
        BindStoreUnlockFields(truck);
        FrontSteerRow.Visibility = truck.HasFrontSteer ? Visibility.Visible : Visibility.Collapsed;
        FrontSteerHintText.Visibility = truck.HasFrontSteer ? Visibility.Visible : Visibility.Collapsed;
        if (truck.HasFrontSteer && truck.FrontSteerAngle is { } frontAngle)
        {
            FrontSteerTextBox.Text = frontAngle.ToString("0.######", CultureInfo.InvariantCulture);
        }
        else
        {
            FrontSteerTextBox.Text = "";
        }

        RearSteerRow.Visibility = truck.HasRearSteer ? Visibility.Visible : Visibility.Collapsed;
        RearSteerHintText.Visibility = truck.HasRearSteer ? Visibility.Visible : Visibility.Collapsed;
        if (truck.HasRearSteer && truck.RearSteerAngle is { } rearAngle)
        {
            RearSteerTextBox.Text = rearAngle.ToString("0.######", CultureInfo.InvariantCulture);
        }
        else
        {
            RearSteerTextBox.Text = "";
        }

        BindDiffLockOptions(truck);
        DriveCombo.SelectedValue = truck.DriveLayout;
        RefreshSafeRangeHints();
        TuningHintText.Visibility = Visibility.Collapsed;
        TuningForm.Visibility = Visibility.Visible;
        RefreshRestoreButton();
    }

    private void EnsureTrucksLoaded(string pakPath)
    {
        if (string.Equals(_trucksPakPath, pakPath, StringComparison.OrdinalIgnoreCase) && _trucks.Count > 0)
        {
            return;
        }

        _trucks = TruckTuningService.LoadTrucks(pakPath, AppLanguage.Current);
        _trucksPakPath = pakPath;
    }

    private void ShowTuningHint(string message)
    {
        TuningForm.Visibility = Visibility.Collapsed;
        TuningHintText.Text = message;
        TuningHintText.Visibility = Visibility.Visible;
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

        DiffLockCombo.SelectedValue = mode;
    }

    private void RefreshRestoreButton()
    {
        var canWrite = PakWriteUi.CanWrite(_session);
        RestoreVehicleButton.IsEnabled = _currentTruck is not null
            && !string.IsNullOrWhiteSpace(_session?.PakPath)
            && PakBaselineService.HasBaseline(_session.PakPath)
            && canWrite;
        SaveTuningButton.IsEnabled = _currentTruck is not null && canWrite;
    }

    private void SaveTuningButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryProceed(_session))
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

        try
        {
            var result = TruckTuningService.SaveTruckChanges(_session.PakPath, _currentTruck);
            _trucksPakPath = null;
            LoadTuning(_currentCard);
            TuningStatusText.Text = result.UpdatedFiles <= 0
                ? UiText.Vehicles.NoChangesToSave
                : UiText.Vehicles.SavedMessage();

            if (result.UpdatedFiles > 0)
            {
                MessageBox.Show(
                    UiText.Vehicles.SavedMessage(),
                    UiText.Vehicles.SaveSuccessTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            TuningStatusText.Text = UiText.Main.ErrorStatus(ex.Message);
            MessageBox.Show(ex.Message, UiText.Vehicles.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RestoreVehicleButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryProceed(_session))
        {
            return;
        }

        if (_currentTruck is null || string.IsNullOrWhiteSpace(_session?.PakPath) || _currentCard is null)
        {
            TuningStatusText.Text = UiText.Vehicles.LoadPakHint;
            return;
        }

        if (!PakBaselineService.HasBaseline(_session.PakPath))
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
            var result = TruckTuningService.RestoreTruckFromBaseline(_session.PakPath, _currentTruck.EntryPath);
            _trucksPakPath = null;
            LoadTuning(_currentCard);
            TuningStatusText.Text = result.UpdatedFiles <= 0
                ? UiText.Vehicles.NoChangesToSave
                : UiText.Vehicles.RestoredMessage();

            if (result.UpdatedFiles > 0)
            {
                MessageBox.Show(
                    UiText.Vehicles.RestoredMessage(),
                    UiText.Vehicles.RestoreSuccessTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            TuningStatusText.Text = UiText.Main.ErrorStatus(ex.Message);
            MessageBox.Show(ex.Message, UiText.Vehicles.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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

        if (!int.TryParse(FuelCapacityTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out fuel)
            || fuel is < 1 or > 10000)
        {
            TuningStatusText.Text = UiText.Vehicles.InvalidFuel;
            return false;
        }

        if (!int.TryParse(StorePriceTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out price)
            || price is < 0 or > 9_999_999)
        {
            TuningStatusText.Text = UiText.Vehicles.InvalidPrice;
            return false;
        }

        if (!int.TryParse(UnlockRankTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out unlockRank)
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
                ResponsivenessTextBox.Text.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out responsiveness)
            || responsiveness is < 0 or > 1)
        {
            TuningStatusText.Text = UiText.Vehicles.InvalidResponsiveness;
            return false;
        }

        if (_currentTruck?.HasFrontSteer == true)
        {
            if (!double.TryParse(
                    FrontSteerTextBox.Text.Trim(),
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
                    RearSteerTextBox.Text.Trim(),
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

        if (DiffLockCombo.SelectedValue is not TruckDiffLockMode selectedDiff
            || DriveCombo.SelectedValue is not TruckDriveLayout selectedDrive)
        {
            TuningStatusText.Text = UiText.Vehicles.LoadPakHint;
            return false;
        }

        if (_currentTruck is not null
            && !_currentTruck.HasNativeDiffLockOptions
            && selectedDiff is TruckDiffLockMode.Switchable or TruckDiffLockMode.Upgradeable)
        {
            selectedDiff = TruckDiffLockMode.None;
        }

        diffLock = selectedDiff;
        drive = selectedDrive;
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

    private void RegionFreeCheckBox_Changed(object sender, RoutedEventArgs e)
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

    private void UnlockRankSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressUnlockRankSync || !_ready)
        {
            return;
        }

        var rank = (int)Math.Round(UnlockRankSlider.Value, MidpointRounding.AwayFromZero);
        _suppressUnlockRankSync = true;
        UnlockRankTextBox.Text = rank.ToString(CultureInfo.InvariantCulture);
        _suppressUnlockRankSync = false;
    }

    private void UnlockRankTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressUnlockRankSync || !_ready)
        {
            return;
        }

        if (!int.TryParse(UnlockRankTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank)
            || rank is < 0 or > 30)
        {
            return;
        }

        _suppressUnlockRankSync = true;
        UnlockRankSlider.Value = rank;
        _suppressUnlockRankSync = false;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        _currentCard = null;
        _currentTruck = null;
        CrashReportContext.ClearVehicle();
        DetailPanel.Visibility = Visibility.Collapsed;
        ListPanel.Visibility = Visibility.Visible;
    }

    private void TuningNumericTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready || TuningForm.Visibility != Visibility.Visible)
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
            FrontSteerSafeRangeHint.Visibility = Visibility.Visible;
            SafeRangeHintPresenter.Refresh(
                FrontSteerSafeRangeHint,
                FrontSteerTextBox,
                TuningFieldRange.FrontSteerDegrees(_currentTruck.BaselineFrontSteerAngle));
        }
        else
        {
            FrontSteerSafeRangeHint.Visibility = Visibility.Collapsed;
        }

        if (_currentTruck.HasRearSteer)
        {
            RearSteerSafeRangeHint.Visibility = Visibility.Visible;
            SafeRangeHintPresenter.Refresh(
                RearSteerSafeRangeHint,
                RearSteerTextBox,
                TuningFieldRange.RearSteerDegrees(_currentTruck.BaselineRearSteerAngle));
        }
        else
        {
            RearSteerSafeRangeHint.Visibility = Visibility.Collapsed;
        }

        SafeRangeHintPresenter.Refresh(
            ResponsivenessSafeRangeHint,
            ResponsivenessTextBox,
            TuningFieldRange.Responsiveness(_currentTruck.BaselineResponsiveness));
    }

    private void SetBasedOnDisplay(VehicleBasedOnFormatter.ParsedBasedOn? basedOn)
    {
        DetailBasedOnText.Inlines.Clear();
        if (basedOn is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(basedOn.Url))
        {
            DetailBasedOnText.Inlines.Add(new Run(basedOn.DisplayText));
            return;
        }

        var link = new Hyperlink(new Run(basedOn.DisplayText))
        {
            NavigateUri = new Uri(basedOn.Url, UriKind.Absolute),
        };
        link.RequestNavigate += BasedOnLink_RequestNavigate;
        DetailBasedOnText.Inlines.Add(link);
    }

    private static void BasedOnLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    public sealed record LabeledValue<T>(string Label, T Value);

    private sealed class VehicleCard
    {
        public VehicleCard(
            string id,
            string pakId,
            string displayName,
            string category,
            BitmapImage? image,
            string ovalCode,
            BitmapImage? flag)
        {
            Id = id;
            PakId = pakId;
            DisplayName = displayName;
            Category = category;
            Image = image;
            OvalCode = ovalCode;
            Flag = flag;
            HeaderBrush = VehicleCategoryColors.ForCategory(category);
        }

        public string Id { get; }
        public string PakId { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public BitmapImage? Image { get; }
        public string OvalCode { get; }
        public BitmapImage? Flag { get; }
        public Brush HeaderBrush { get; }
        public Visibility OvalVisibility =>
            string.IsNullOrWhiteSpace(OvalCode) ? Visibility.Collapsed : Visibility.Visible;
        public Visibility FlagVisibility =>
            Flag is null ? Visibility.Collapsed : Visibility.Visible;
    }
}
