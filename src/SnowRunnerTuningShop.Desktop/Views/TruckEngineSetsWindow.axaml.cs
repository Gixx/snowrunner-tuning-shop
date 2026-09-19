using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SnowRunnerTuningShop.Core.Trucks;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class TruckEngineSetsWindow : Window
{
    private readonly List<EngineSetRowVm> _allRows = [];
    private readonly ObservableCollection<EngineSetRowVm> _visibleRows = [];

    public TruckEngineSetsWindow()
    {
        InitializeComponent();
    }

    public TruckEngineSetsWindow(TruckEngineSetsSnapshot snapshot)
        : this()
    {
        Title = UiText.Vehicles.EngineSetsTitle;
        HintText.Text = UiText.Vehicles.EngineSetsHint;
        SearchBox.PlaceholderText = UiText.Vehicles.EngineSetsSearchPlaceholder;
        CancelButton.Content = UiText.Vehicles.EngineSetsCancel;
        ApplyButton.Content = UiText.Vehicles.EngineSetsApply;

        if (SetsGrid.Columns.Count >= 3)
        {
            SetsGrid.Columns[1].Header = UiText.Vehicles.EngineSetColumn;
            SetsGrid.Columns[2].Header = UiText.Vehicles.EngineNamesColumn;
        }

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

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        ApplySearchFilter();

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

    private void CancelButton_Click(object? sender, RoutedEventArgs e) =>
        Close(false);

    private void ApplyButton_Click(object? sender, RoutedEventArgs e)
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
        Close(true);
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
