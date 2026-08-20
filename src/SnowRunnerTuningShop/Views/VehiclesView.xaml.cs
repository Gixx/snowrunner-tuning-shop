using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using SnowRunnerTuningShop.Localization;
using SnowRunnerTuningShop.Vehicles;

namespace SnowRunnerTuningShop.Views;

public partial class VehiclesView : UserControl
{
    private readonly List<VehicleCard> _all = [];
    private readonly ObservableCollection<VehicleCard> _visible = [];
    private string _filter = "All";
    private bool _ready;

    public VehiclesView()
    {
        InitializeComponent();
        VehiclesItems.ItemsSource = _visible;
        Loaded += VehiclesView_Loaded;
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
    }

    private void LoadCatalog()
    {
        _all.Clear();
        var entries = VehicleCatalog.Load();
        if (entries.Count == 0)
        {
            CountTextBlock.Text = UiText.Vehicles.CatalogMissing;
            return;
        }

        foreach (var entry in entries)
        {
            _all.Add(new VehicleCard(
                entry.Id,
                entry.DisplayName,
                entry.Category,
                VehicleCatalog.TryLoadImage(entry.ImagePath)));
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

    private void ApplyFilter()
    {
        if (!_ready)
        {
            return;
        }

        _visible.Clear();
        IEnumerable<VehicleCard> query = _all;
        if (!string.Equals(_filter, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = _all.Where(card => card.Category.Equals(_filter, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var card in query)
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

        ShowDetail(card);
    }

    private void ShowDetail(VehicleCard card)
    {
        DetailTitleText.Text = card.DisplayName;
        DetailCategoryText.Text = card.Category;
        DetailImage.Source = card.Image;
        ListPanel.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        DetailPanel.Visibility = Visibility.Collapsed;
        ListPanel.Visibility = Visibility.Visible;
    }

    private sealed class VehicleCard
    {
        public VehicleCard(string id, string displayName, string category, BitmapImage? image)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
            Image = image;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public BitmapImage? Image { get; }
    }
}
