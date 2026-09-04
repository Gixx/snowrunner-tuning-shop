using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnowRunnerTuningShop.Controls;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Diagnostics;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Trailers;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Localization;
using SnowRunnerTuningShop.Trailers;

namespace SnowRunnerTuningShop.Views;

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

    public TrailersView()
    {
        InitializeComponent();
        TrailersItems.ItemsSource = _visible;
        Loaded += TrailersView_Loaded;
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

    private void TrailersView_Loaded(object sender, RoutedEventArgs e)
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
        _trailers = [];
        _trailersPakPath = null;
        RefreshGlobalMultipliersPanel();
        if (_currentCard is not null && DetailPanel.Visibility == Visibility.Visible)
        {
            LoadTuning(_currentCard);
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
        RepairsMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.RepairParts,
            GetMultiplierIndex(RepairsMultiplierSlider));
        WheelsMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.SpareWheels,
            GetMultiplierIndex(WheelsMultiplierSlider));
        PriceMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.StorePrice,
            GetMultiplierIndex(PriceMultiplierSlider));
    }

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
                UiText.Trailers.LoadPakForGlobalHint,
                UiText.Trailers.SaveErrorTitle,
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
            var result = TrailerTuningService.ApplyGlobalMultipliers(
                _session.PakPath,
                GetMultiplier(FuelMultiplierSlider),
                GetMultiplier(RepairsMultiplierSlider),
                GetMultiplier(WheelsMultiplierSlider),
                GetMultiplier(PriceMultiplierSlider));

            _trailersPakPath = null;
            if (_currentCard is not null && DetailPanel.Visibility == Visibility.Visible)
            {
                LoadTuning(_currentCard);
            }

            MessageBox.Show(
                UiText.Trailers.GlobalMultipliersSavedMessage(result.ChangedTrailers, result.UpdatedFiles),
                UiText.Trailers.SaveSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Trailers.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MakeMissionTrailersPurchasableButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryProceed(_session))
        {
            return;
        }

        if (_session?.HasPak != true || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            MessageBox.Show(
                UiText.Trailers.LoadPakForGlobalHint,
                UiText.Trailers.SaveErrorTitle,
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
            var result = TrailerTuningService.MakeQuestTrailersPurchasable(_session.PakPath);
            _trailersPakPath = null;
            if (_currentCard is not null && DetailPanel.Visibility == Visibility.Visible)
            {
                LoadTuning(_currentCard);
            }

            MessageBox.Show(
                UiText.Trailers.StoreUnlocksSavedMessage(result.ChangedTrailers, result.UpdatedFiles),
                UiText.Trailers.SaveSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Trailers.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RestoreAllTrailersButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryProceed(_session))
        {
            return;
        }

        if (_session?.HasPak != true || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            MessageBox.Show(
                UiText.Trailers.LoadPakForGlobalHint,
                UiText.Trailers.SaveErrorTitle,
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
            UiText.Trailers.RestoreAllTrailersConfirmMessage,
            UiText.Trailers.RestoreAllTrailersConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var result = TrailerTuningService.RestoreAllTrailersFromBaseline(_session.PakPath);
            _trailersPakPath = null;
            ResetGlobalMultiplierSlidersToBaseline();
            UpdateGlobalMultiplierLabels();
            if (_currentCard is not null && DetailPanel.Visibility == Visibility.Visible)
            {
                LoadTuning(_currentCard);
            }

            MessageBox.Show(
                UiText.Trailers.RestoreAllTrailersSavedMessage(result.ChangedTrailers, result.UpdatedFiles),
                UiText.Trailers.RestoreAllTrailersSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Trailers.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadCatalog()
    {
        _all.Clear();
        var entries = TrailerCatalog.Load();
        if (entries.Count == 0)
        {
            CountTextBlock.Text = UiText.Trailers.CatalogMissing;
            return;
        }

        var imageCache = new Dictionary<string, BitmapImage?>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            BitmapImage? image = null;
            if (!string.IsNullOrWhiteSpace(entry.ImagePath))
            {
                if (!imageCache.TryGetValue(entry.ImagePath, out image))
                {
                    image = TrailerCatalog.TryLoadImage(entry.ImagePath);
                    imageCache[entry.ImagePath] = image;
                }
            }

            _all.Add(new TrailerCard(entry, image));
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

    private void TrailerCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TrailerCard card)
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

    private void ShowDetail(TrailerCard card)
    {
        CrashReportContext.SetVehicle(card.Id, card.DisplayName);
        _currentCard = card;
        DetailTitleText.Text = card.DisplayName;
        DetailImage.Source = TrailerCatalog.TryLoadImage(card.ImagePath, decodePixelWidth: 720) ?? card.Image;
        DetailHitchText.Text = card.HitchLabel;
        DetailFunctionText.Text = card.FunctionLabel;
        DetailMissionText.Text = card.IsMission ? UiText.Trailers.MissionYes : UiText.Trailers.MissionNo;
        LoadTuning(card);

        ListPanel.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
    }

    private void LoadTuning(TrailerCard card)
    {
        _currentTrailer = null;
        TuningStatusText.Text = "";
        RefreshRestoreButton();

        if (_session?.HasPak != true || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            ShowTuningHint(UiText.Trailers.LoadPakHint);
            return;
        }

        try
        {
            EnsureTrailersLoaded(_session.PakPath);
        }
        catch (Exception ex)
        {
            ShowTuningHint(ex.Message);
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

        FuelSafeRangeHint.Visibility = trailer.HasFuel ? Visibility.Visible : Visibility.Collapsed;
        WaterSafeRangeHint.Visibility = trailer.HasWater ? Visibility.Visible : Visibility.Collapsed;
        RepairsSafeRangeHint.Visibility = trailer.HasRepairs ? Visibility.Visible : Visibility.Collapsed;
        WheelsSafeRangeHint.Visibility = trailer.HasWheels ? Visibility.Visible : Visibility.Collapsed;

        PriceRow.Visibility = trailer.HasGameData ? Visibility.Visible : Visibility.Collapsed;
        PriceSafeRangeHint.Visibility = trailer.HasGameData ? Visibility.Visible : Visibility.Collapsed;
        AvailableInStoreRow.Visibility = trailer.HasGameData ? Visibility.Visible : Visibility.Collapsed;
        AvailableInStoreHintText.Visibility = trailer.HasGameData ? Visibility.Visible : Visibility.Collapsed;
        UnlockRankRow.Visibility = trailer.HasGameData ? Visibility.Visible : Visibility.Collapsed;
        UnlockRankHintText.Visibility = trailer.HasGameData ? Visibility.Visible : Visibility.Collapsed;
        if (trailer.HasGameData)
        {
            StorePriceTextBox.Text = trailer.Price.ToString(CultureInfo.InvariantCulture);
            AvailableInStoreCheckBox.IsChecked = trailer.IsAvailableInStore;
            _suppressUnlockRankSync = true;
            UnlockRankSlider.Value = trailer.UnlockByRank;
            UnlockRankTextBox.Text = trailer.UnlockByRank.ToString(CultureInfo.InvariantCulture);
            _suppressUnlockRankSync = false;
        }

        if (!trailer.HasFuel && !trailer.HasWater && !trailer.HasRepairs && !trailer.HasWheels && !trailer.HasGameData)
        {
            ShowTuningHint(UiText.Trailers.NoTunableFields);
            return;
        }

        RefreshSafeRangeHints();
        TuningHintText.Visibility = Visibility.Collapsed;
        TuningForm.Visibility = Visibility.Visible;
        RefreshRestoreButton();
    }

    private static void BindCapacityField(FrameworkElement row, TextBox box, bool visible, int value)
    {
        row.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        box.Text = visible ? value.ToString(CultureInfo.InvariantCulture) : "";
    }

    private void EnsureTrailersLoaded(string pakPath)
    {
        if (string.Equals(_trailersPakPath, pakPath, StringComparison.OrdinalIgnoreCase) && _trailers.Count > 0)
        {
            return;
        }

        _trailers = TrailerTuningService.LoadTrailers(pakPath, AppLanguage.Current);
        _trailersPakPath = pakPath;
    }

    private void ShowTuningHint(string message)
    {
        TuningForm.Visibility = Visibility.Collapsed;
        TuningHintText.Text = message;
        TuningHintText.Visibility = Visibility.Visible;
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

    private void SaveTuningButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryProceed(_session))
        {
            return;
        }

        if (_currentTrailer is null || string.IsNullOrWhiteSpace(_session?.PakPath) || _currentCard is null)
        {
            TuningStatusText.Text = UiText.Trailers.LoadPakHint;
            return;
        }

        if (!TryReadForm(out var fuel, out var water, out var repairs, out var wheels, out var price, out var unlockRank))
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

        try
        {
            var result = TrailerTuningService.SaveTrailerChanges(_session.PakPath, _currentTrailer);
            _trailersPakPath = null;
            LoadTuning(_currentCard);
            TuningStatusText.Text = result.UpdatedFiles <= 0
                ? UiText.Trailers.NoChangesToSave
                : UiText.Trailers.SavedMessage();

            if (result.UpdatedFiles > 0)
            {
                MessageBox.Show(
                    UiText.Trailers.SavedMessage(),
                    UiText.Trailers.SaveSuccessTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            TuningStatusText.Text = UiText.Main.ErrorStatus(ex.Message);
            MessageBox.Show(ex.Message, UiText.Trailers.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RestoreTrailerButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryProceed(_session))
        {
            return;
        }

        if (_currentTrailer is null || string.IsNullOrWhiteSpace(_session?.PakPath) || _currentCard is null)
        {
            TuningStatusText.Text = UiText.Trailers.LoadPakHint;
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
            var result = TrailerTuningService.RestoreTrailerFromBaseline(_session.PakPath, _currentTrailer.EntryPath);
            _trailersPakPath = null;
            LoadTuning(_currentCard);
            TuningStatusText.Text = result.UpdatedFiles <= 0
                ? UiText.Trailers.NoChangesToSave
                : UiText.Trailers.RestoredMessage();

            if (result.UpdatedFiles > 0)
            {
                MessageBox.Show(
                    UiText.Trailers.RestoredMessage(),
                    UiText.Trailers.RestoreSuccessTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            TuningStatusText.Text = UiText.Main.ErrorStatus(ex.Message);
            MessageBox.Show(ex.Message, UiText.Trailers.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool TryReadForm(
        out int fuel,
        out int water,
        out int repairs,
        out int wheels,
        out int price,
        out int unlockRank)
    {
        fuel = 0;
        water = 0;
        repairs = 0;
        wheels = 0;
        price = 0;
        unlockRank = 0;

        if (_currentTrailer?.HasFuel == true
            && (!int.TryParse(FuelCapacityTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out fuel)
                || fuel is < 1 or > 10000))
        {
            TuningStatusText.Text = UiText.Trailers.InvalidFuel;
            return false;
        }

        if (_currentTrailer?.HasWater == true
            && (!int.TryParse(WaterCapacityTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out water)
                || water is < 1 or > 10000))
        {
            TuningStatusText.Text = UiText.Trailers.InvalidWater;
            return false;
        }

        if (_currentTrailer?.HasRepairs == true
            && (!int.TryParse(RepairsCapacityTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out repairs)
                || repairs is < 0 or > 10_000))
        {
            TuningStatusText.Text = UiText.Trailers.InvalidRepairs;
            return false;
        }

        if (_currentTrailer?.HasWheels == true
            && (!int.TryParse(WheelRepairsTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out wheels)
                || wheels is < 0 or > 99))
        {
            TuningStatusText.Text = UiText.Trailers.InvalidWheels;
            return false;
        }

        if (_currentTrailer?.HasGameData == true)
        {
            if (!int.TryParse(StorePriceTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out price)
                || price is < 0 or > 9_999_999)
            {
                TuningStatusText.Text = UiText.Trailers.InvalidPrice;
                return false;
            }

            if (!int.TryParse(UnlockRankTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out unlockRank)
                || unlockRank is < 0 or > 30)
            {
                TuningStatusText.Text = UiText.Trailers.InvalidUnlockRank;
                return false;
            }
        }

        return true;
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
        _currentTrailer = null;
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
        public TrailerCard(TrailerCatalogEntry entry, BitmapImage? image)
        {
            Id = entry.Id;
            DisplayName = entry.DisplayName;
            Hitch = entry.Hitch;
            Function = entry.Function;
            IsQuest = entry.IsQuest;
            ImagePath = entry.ImagePath;
            Image = image;
            HitchLabel = UiText.Trailers.HitchName(entry.Hitch);
            FunctionLabel = UiText.Trailers.FunctionName(entry.Function);
            HeaderBrush = TrailerCategoryColors.ForHitch(entry.Hitch);
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Hitch { get; }
        public string Function { get; }
        public bool IsQuest { get; }
        public string ImagePath { get; }
        public bool IsMission =>
            IsQuest || Function.Equals("mission", StringComparison.OrdinalIgnoreCase);
        public BitmapImage? Image { get; }
        public string HitchLabel { get; }
        public string FunctionLabel { get; }
        public Brush HeaderBrush { get; }
        public Visibility MissionBadgeVisibility =>
            IsMission ? Visibility.Visible : Visibility.Collapsed;
    }
}
