using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.General;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class GeneralView : UserControl
{
    private AppSession? _session;
    private bool _suppressRockSlider;

    public GeneralView()
    {
        InitializeComponent();
        ApplyStaticText();
        BindCameraModes();
        RefreshRockSizeLabel();
    }

    public void AttachSession(AppSession session)
    {
        _session = session;
        _session.PakChanged += (_, _) => ReloadFromPak();
        _session.BaselineChanged += (_, _) => ReloadFromPak();
        _session.GameRunningChanged += (_, _) => RefreshWriteGates();
        ReloadFromPak();
    }

    private Window? OwnerWindow => TopLevel.GetTopLevel(this) as Window;

    private void ApplyStaticText()
    {
        TitleText.Text = UiText.General.Title;
        CameraTitleText.Text = UiText.General.CameraTitle;
        CameraHintText.Text = UiText.General.CameraHint;
        CameraModeLabelText.Text = UiText.General.CameraModeLabel;
        ApplyCameraButton.Content = UiText.General.ApplyCamera;
        RestoreCameraButton.Content = UiText.General.RestoreCameraBaseline;
        RockTitleText.Text = UiText.General.RockTitle;
        RockHintText.Text = UiText.General.RockHint;
        ApplyRockSizeButton.Content = UiText.General.ApplyRockSize;
        RestoreRockSizeButton.Content = UiText.General.RestoreRockBaseline;
    }

    private void BindCameraModes()
    {
        CameraModeCombo.ItemsSource = new LabeledCameraMode[]
        {
            new(UiText.General.CameraCollisionsOff, CameraCollisionMode.CollisionsOff),
            new(UiText.General.CameraCollisionsOn, CameraCollisionMode.CollisionsOn),
        };
        CameraModeCombo.SelectedIndex = 0;
    }

    private void ReloadFromPak()
    {
        if (_session is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            HintText.Text = UiText.General.LoadPakHint;
            HintText.IsVisible = true;
            StatusText.Text = "";
            ApplyCameraButton.IsEnabled = false;
            ApplyRockSizeButton.IsEnabled = false;
            RestoreCameraButton.IsEnabled = false;
            RestoreRockSizeButton.IsEnabled = false;
            return;
        }

        HintText.IsVisible = false;

        try
        {
            var settings = GeneralService.LoadSettings(_session.PakPath, AppPaths.TryFindGeneralAssetsDirectory());
            CameraModeCombo.SelectedItem = CameraModeCombo.Items
                .OfType<LabeledCameraMode>()
                .FirstOrDefault(item => item.Value == (settings.CameraCollisionState switch
                {
                    CameraCollisionState.CollisionsOff => CameraCollisionMode.CollisionsOff,
                    _ => CameraCollisionMode.CollisionsOn,
                }))
                ?? CameraModeCombo.Items.OfType<LabeledCameraMode>().FirstOrDefault();

            _suppressRockSlider = true;
            RockSizeSlider.Value = RockSizePresets.FindNearestIndex(settings.RockSizeScale);
            _suppressRockSlider = false;
            RefreshRockSizeLabel();
            StatusText.Text = UiText.General.LoadedStatus(settings.CameraEligibleModels, settings.RockSizeScale);
        }
        catch (Exception ex)
        {
            StatusText.Text = UiText.Main.ErrorStatus(ex.Message);
        }

        RefreshWriteGates();
    }

    private void RefreshWriteGates()
    {
        if (_session is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            ApplyCameraButton.IsEnabled = false;
            ApplyRockSizeButton.IsEnabled = false;
            RestoreCameraButton.IsEnabled = false;
            RestoreRockSizeButton.IsEnabled = false;
            return;
        }

        var canWrite = PakWriteUi.CanWrite(_session);
        var hasBaseline = PakBaselineService.HasBaseline(_session.PakPath);
        ApplyCameraButton.IsEnabled = canWrite;
        ApplyRockSizeButton.IsEnabled = canWrite;
        RestoreCameraButton.IsEnabled = hasBaseline && canWrite;
        RestoreRockSizeButton.IsEnabled = hasBaseline && canWrite;
    }

    private async void ApplyCameraButton_Click(object? sender, RoutedEventArgs e) =>
        await ApplyCameraAsync();

    private async void RestoreCameraButton_Click(object? sender, RoutedEventArgs e) =>
        await ApplyCameraAsync(CameraCollisionMode.Baseline);

    private async Task ApplyCameraAsync(CameraCollisionMode? mode = null)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryProceed(owner, _session))
        {
            return;
        }

        if (_session is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            StatusText.Text = UiText.General.LoadPakHint;
            return;
        }

        if (!PakBaselineService.HasBaseline(_session.PakPath))
        {
            await AppDialogs.ShowWarning(owner, UiText.Main.BaselineMissingShort, UiText.Main.BaselineTitle);
            return;
        }

        var selectedMode = mode
            ?? (CameraModeCombo.SelectedItem as LabeledCameraMode)?.Value
            ?? CameraCollisionMode.CollisionsOff;

        using (PakWriteUi.BeginBusyWrite(owner, ApplyCameraButton, RestoreCameraButton))
        {
            try
            {
                var pakPath = _session.PakPath;
                var result = await Task.Run(() => GeneralService.ApplyCameraCollisions(pakPath, selectedMode));
                ReloadFromPak();
                StatusText.Text = result.UpdatedFiles <= 0
                    ? UiText.General.NoChangesToSave
                    : UiText.General.CameraSaved(result.UpdatedFiles);
            }
            catch (Exception ex)
            {
                StatusText.Text = UiText.Main.ErrorStatus(ex.Message);
                await AppDialogs.ShowError(owner, ex.Message, UiText.General.SaveErrorTitle);
            }
        }
    }

    private async void ApplyRockSizeButton_Click(object? sender, RoutedEventArgs e) =>
        await ApplyRockSizeAsync(RockSizePresets.GetValue((int)Math.Round(RockSizeSlider.Value)));

    private async void RestoreRockSizeButton_Click(object? sender, RoutedEventArgs e) =>
        await ApplyRockSizeAsync(1.0);

    private async Task ApplyRockSizeAsync(double scale)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryProceed(owner, _session))
        {
            return;
        }

        if (_session is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            StatusText.Text = UiText.General.LoadPakHint;
            return;
        }

        if (!PakBaselineService.HasBaseline(_session.PakPath))
        {
            await AppDialogs.ShowWarning(owner, UiText.Main.BaselineMissingShort, UiText.Main.BaselineTitle);
            return;
        }

        var assetsDir = AppPaths.TryFindGeneralAssetsDirectory();
        if (assetsDir is null)
        {
            StatusText.Text = UiText.General.AssetsMissing;
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyRockSizeButton, RestoreRockSizeButton))
        {
            try
            {
                var pakPath = _session.PakPath;
                var result = await Task.Run(() => GeneralService.ApplyRockSize(pakPath, scale, assetsDir));
                ReloadFromPak();
                StatusText.Text = result.UpdatedFiles <= 0
                    ? UiText.General.NoChangesToSave
                    : UiText.General.RockSaved(result.UpdatedFiles);
            }
            catch (Exception ex)
            {
                StatusText.Text = UiText.Main.ErrorStatus(ex.Message);
                await AppDialogs.ShowError(owner, ex.Message, UiText.General.SaveErrorTitle);
            }
        }
    }

    private void RockSizeSlider_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != RangeBase.ValueProperty || _suppressRockSlider)
        {
            return;
        }

        RefreshRockSizeLabel();
    }

    private void RefreshRockSizeLabel()
    {
        var index = RockSizePresets.ClampIndex((int)Math.Round(RockSizeSlider.Value));
        RockSizeLabel.Text = UiText.General.RockPhysicsCaption(index);
    }

    public sealed record LabeledCameraMode(string Label, CameraCollisionMode Value)
    {
        public override string ToString() => Label;
    }
}
