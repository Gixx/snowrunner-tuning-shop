using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SnowRunnerTuningShop.Core.Trucks;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class TruckEngineSetsWindow : Window
{
    private readonly List<EngineSetRowVm> _allRows = [];
    private readonly ObservableCollection<EngineSetRowVm> _visibleRows = [];

    public TruckEngineSetsWindow(TruckEngineSetsSnapshot snapshot)
    {
        InitializeComponent();
        HintText.Text = UiText.Vehicles.EngineSetsHint;

        foreach (var set in snapshot.Sets)
        {
            _allRows.Add(new EngineSetRowVm(
                set.SetId,
                set.EngineNamesText,
                set.EngineNames,
                set.IsAssigned));
        }

        ApplySearchFilter();
        SetsGrid.ItemsSource = _visibleRows;
        ApplyButton.IsEnabled = snapshot.HasEngineSocket;
        if (!snapshot.HasEngineSocket)
        {
            StatusText.Text = UiText.Vehicles.EngineSetsMissingSocket;
        }
    }

    public IReadOnlyList<string> SelectedSetIds { get; private set; } = [];

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = string.IsNullOrWhiteSpace(SearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        ApplySearchFilter();
    }

    private void ApplySearchFilter()
    {
        var query = SearchBox.Text?.Trim() ?? "";
        _visibleRows.Clear();
        foreach (var row in _allRows)
        {
            if (row.Matches(query))
            {
                _visibleRows.Add(row);
            }
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = _allRows
            .Where(row => row.IsAssigned)
            .Select(row => row.SetId)
            .ToArray();

        if (selected.Length == 0)
        {
            StatusText.Text = UiText.Vehicles.EngineSetsNeedOne;
            return;
        }

        SelectedSetIds = selected;
        DialogResult = true;
        Close();
    }

    private sealed class EngineSetRowVm : INotifyPropertyChanged
    {
        private bool _isAssigned;

        public EngineSetRowVm(
            string setId,
            string engineNamesText,
            IReadOnlyList<string> engineNames,
            bool isAssigned)
        {
            SetId = setId;
            EngineNamesText = engineNamesText;
            EngineNames = engineNames;
            _isAssigned = isAssigned;
        }

        public string SetId { get; }

        public string EngineNamesText { get; }

        public IReadOnlyList<string> EngineNames { get; }

        public bool IsAssigned
        {
            get => _isAssigned;
            set
            {
                if (_isAssigned == value)
                {
                    return;
                }

                _isAssigned = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAssigned)));
            }
        }

        public bool Matches(string query)
        {
            if (query.Length == 0)
            {
                return true;
            }

            if (SetId.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (EngineNamesText.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (var name in EngineNames)
            {
                if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
