using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SnowRunnerTuningShop.Core.Trucks;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class TruckWheelSetsWindow : Window
{
    private readonly List<WheelSetRowVm> _allRows = [];
    private readonly ObservableCollection<WheelSetRowVm> _visibleRows = [];

    public TruckWheelSetsWindow()
    {
        InitializeComponent();
    }

    public TruckWheelSetsWindow(TruckWheelSetsSnapshot snapshot)
        : this()
    {
        Title = UiText.Vehicles.WheelSetsTitle;
        HintText.Text = UiText.Vehicles.WheelSetsHint;
        SearchBox.PlaceholderText = UiText.Vehicles.WheelSetsSearchPlaceholder;
        CancelButton.Content = UiText.Vehicles.WheelSetsCancel;
        ApplyButton.Content = UiText.Vehicles.WheelSetsApply;

        if (SetsGrid.Columns.Count >= 4)
        {
            SetsGrid.Columns[1].Header = UiText.Vehicles.WheelSetColumn;
            SetsGrid.Columns[2].Header = UiText.Vehicles.WheelTireNamesColumn;
            SetsGrid.Columns[3].Header = UiText.Vehicles.WheelSetNoteColumn;
        }

        foreach (var set in snapshot.Sets)
        {
            _allRows.Add(new WheelSetRowVm(
                set.SetId,
                set.TireNamesText,
                set.TireNames,
                set.TireLabels,
                UiText.Vehicles.FormatWheelSetNote(set.NoteKind, set.RelatedSetId),
                set.IsAssigned));
        }

        ApplySearchFilter();
        SetsGrid.ItemsSource = _visibleRows;
        ApplyButton.IsEnabled = snapshot.HasCompatibleWheels;
        if (!snapshot.HasCompatibleWheels)
        {
            StatusText.Text = UiText.Vehicles.WheelSetsMissing;
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
            StatusText.Text = UiText.Vehicles.WheelSetsNeedOne;
            return;
        }

        SelectedSetIds = selected;
        Close(true);
    }

    private sealed class WheelSetRowVm : INotifyPropertyChanged
    {
        private bool _isAssigned;

        public WheelSetRowVm(
            string setId,
            string tireNamesText,
            IReadOnlyList<string> tireNames,
            IReadOnlyList<string> tireLabels,
            string noteText,
            bool isAssigned)
        {
            SetId = setId;
            TireNamesText = tireNamesText;
            TireNames = tireNames;
            TireLabels = tireLabels;
            NoteText = noteText;
            _isAssigned = isAssigned;
        }

        public string SetId { get; }

        public string TireNamesText { get; }

        public IReadOnlyList<string> TireNames { get; }

        public IReadOnlyList<string> TireLabels { get; }

        public string NoteText { get; }

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

            if (TireNamesText.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (var name in TireNames)
            {
                if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            foreach (var label in TireLabels)
            {
                if (label.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (NoteText.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
