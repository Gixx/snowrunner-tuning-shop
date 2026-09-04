using System.Windows;
using System.Windows.Controls;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.General;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class GeneralView : UserControl
{
    private sealed record LabeledValue<T>(string Label, T Value);

    private AppSession? _session;
    private bool _suppressRockSlider;

    public GeneralView()
    {
        InitializeComponent();
        BindCameraModes();
        RefreshRockSizeLabel();
    }

    public void AttachSession(AppSession session)
    {
        _session = session;
        _session.PakChanged += (_, _) => ReloadFromPak();
        _session.BaselineChanged += (_, _) => ReloadFromPak();
        ReloadFromPak();
    }

    private void BindCameraModes()
    {
        CameraModeCombo.ItemsSource = new LabeledValue<CameraCollisionMode>[]
        {
            new(UiText.General.CameraCollisionsOff, CameraCollisionMode.CollisionsOff),
            new(UiText.General.CameraCollisionsOn, CameraCollisionMode.CollisionsOn),
        };
        CameraModeCombo.DisplayMemberPath = nameof(LabeledValue<CameraCollisionMode>.Label);
        CameraModeCombo.SelectedValuePath = nameof(LabeledValue<CameraCollisionMode>.Value);
        CameraModeCombo.SelectedValue = CameraCollisionMode.CollisionsOff;
    }

    private void ReloadFromPak()
    {
        if (_session is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            HintText.Text = UiText.General.LoadPakHint;
            HintText.Visibility = Visibility.Visible;
            StatusText.Text = "";
            RestoreCameraButton.IsEnabled = false;
            RestoreRockSizeButton.IsEnabled = false;
            return;
        }

        HintText.Visibility = Visibility.Collapsed;
        var hasBaseline = PakBaselineService.HasBaseline(_session.PakPath);
        RestoreCameraButton.IsEnabled = hasBaseline;
        RestoreRockSizeButton.IsEnabled = hasBaseline;

        try
        {
            var settings = GeneralService.LoadSettings(_session.PakPath, AppPaths.TryFindGeneralAssetsDirectory());
            CameraModeCombo.SelectedValue = settings.CameraCollisionState switch
            {
                CameraCollisionState.CollisionsOff => CameraCollisionMode.CollisionsOff,
                _ => CameraCollisionMode.CollisionsOn,
            };

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
    }

    private void ApplyCameraButton_Click(object sender, RoutedEventArgs e) =>
        ApplyCamera();

    private void RestoreCameraButton_Click(object sender, RoutedEventArgs e) =>
        ApplyCamera(CameraCollisionMode.Baseline);

    private void ApplyCamera(CameraCollisionMode? mode = null)
    {
        if (_session is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            StatusText.Text = UiText.General.LoadPakHint;
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

        var selectedMode = mode
            ?? (CameraModeCombo.SelectedValue as CameraCollisionMode?)
            ?? CameraCollisionMode.CollisionsOff;

        try
        {
            var result = GeneralService.ApplyCameraCollisions(_session.PakPath, selectedMode);
            ReloadFromPak();
            StatusText.Text = result.UpdatedFiles <= 0
                ? UiText.General.NoChangesToSave
                : UiText.General.CameraSaved(result.UpdatedFiles);
            if (result.UpdatedFiles > 0)
            {
                MessageBox.Show(
                    UiText.General.CameraSaved(result.UpdatedFiles),
                    UiText.General.SaveSuccessTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = UiText.Main.ErrorStatus(ex.Message);
            MessageBox.Show(ex.Message, UiText.General.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyRockSizeButton_Click(object sender, RoutedEventArgs e) =>
        ApplyRockSize(RockSizePresets.GetValue((int)Math.Round(RockSizeSlider.Value)));

    private void RestoreRockSizeButton_Click(object sender, RoutedEventArgs e) =>
        ApplyRockSize(1.0);

    private void ApplyRockSize(double scale)
    {
        if (_session is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            StatusText.Text = UiText.General.LoadPakHint;
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

        var assetsDir = AppPaths.TryFindGeneralAssetsDirectory();
        if (assetsDir is null)
        {
            StatusText.Text = UiText.General.AssetsMissing;
            return;
        }

        try
        {
            var result = GeneralService.ApplyRockSize(_session.PakPath, scale, assetsDir);
            ReloadFromPak();
            StatusText.Text = result.UpdatedFiles <= 0
                ? UiText.General.NoChangesToSave
                : UiText.General.RockSaved(result.UpdatedFiles);
            if (result.UpdatedFiles > 0)
            {
                MessageBox.Show(
                    UiText.General.RockSaved(result.UpdatedFiles),
                    UiText.General.SaveSuccessTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = UiText.Main.ErrorStatus(ex.Message);
            MessageBox.Show(ex.Message, UiText.General.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RockSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressRockSlider)
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
}
