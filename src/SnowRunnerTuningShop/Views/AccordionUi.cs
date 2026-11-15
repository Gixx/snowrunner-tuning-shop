using System.Windows;
using System.Windows.Controls;

namespace SnowRunnerTuningShop.Views;

/// <summary>Keeps at most one expander open in a group (accordion behavior).</summary>
public sealed class AccordionExclusiveGroup
{
    private readonly Expander[] _expanders;
    private bool _suppress;

    public AccordionExclusiveGroup(params Expander[] expanders)
    {
        _expanders = expanders ?? throw new ArgumentNullException(nameof(expanders));
        foreach (var expander in _expanders)
        {
            expander.Expanded += OnExpanded;
        }
    }

    private void OnExpanded(object sender, RoutedEventArgs e)
    {
        if (_suppress || sender is not Expander opened)
        {
            return;
        }

        _suppress = true;
        try
        {
            foreach (var expander in _expanders)
            {
                if (!ReferenceEquals(expander, opened))
                {
                    expander.IsExpanded = false;
                }
            }
        }
        finally
        {
            _suppress = false;
        }
    }
}

/// <summary>Prefixes an expander header with "* " while the panel has unapplied edits.</summary>
public sealed class AccordionDirtyHeader
{
    private readonly Expander _expander;
    private string _baseTitle;
    private bool _dirty;

    public AccordionDirtyHeader(Expander expander, string baseTitle)
    {
        _expander = expander ?? throw new ArgumentNullException(nameof(expander));
        _baseTitle = baseTitle ?? "";
        Apply();
    }

    public bool IsDirty => _dirty;

    public void SetBaseTitle(string baseTitle)
    {
        _baseTitle = baseTitle ?? "";
        Apply();
    }

    public void SetDirty(bool dirty)
    {
        if (_dirty == dirty)
        {
            return;
        }

        _dirty = dirty;
        Apply();
    }

    public void MarkDirty() => SetDirty(true);

    public void ClearDirty() => SetDirty(false);

    private void Apply() =>
        _expander.Header = _dirty ? "* " + _baseTitle : _baseTitle;
}
