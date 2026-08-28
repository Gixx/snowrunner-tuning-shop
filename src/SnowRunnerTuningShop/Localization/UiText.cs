namespace SnowRunnerTuningShop.Localization;

using SnowRunnerTuningShop.Core;
using SnowRunnerTuningShop.Core.Profile;
using SnowRunnerTuningShop.Core.Tuning;

public static class UiText
{
    public static class Nav
    {
        public static string Home => StringResources.Get("Nav.Home", "Home");
        public static string General => StringResources.Get("Nav.General", "General");
        public static string Parts => StringResources.Get("Nav.Parts", "Parts");
        public static string Vehicles => StringResources.Get("Nav.Vehicles", "Vehicles");
        public static string Settings => StringResources.Get("Nav.Settings", "Settings");
        public static string OpenMenu => StringResources.Get("Nav.OpenMenu", "Menu");
        public static string PinMenu => StringResources.Get("Nav.PinMenu", "Keep menu open");
        public static string VersionLabel => StringResources.Format("Nav.VersionLabel", "Version {0}", AppInfo.Version);
    }

    public static class Main
    {
        public static string Subtitle => StringResources.Get("Main.Subtitle", "Fine-tune SnowRunner initial.pak");
        public static string OverviewTitle => StringResources.Get("Main.OverviewTitle", "Overview");
        public static string OverviewPlaceholder => StringResources.Get("Main.OverviewPlaceholder", "Set a baseline from your original initial.pak to load the workspace.");
        public static string CategoriesTitle => StringResources.Get("Main.CategoriesTitle", "Tuning categories");
        public static string CategoryColumn => StringResources.Get("Main.CategoryColumn", "Category");
        public static string ItemsColumn => StringResources.Get("Main.ItemsColumn", "Items");
        public static string FilesColumn => StringResources.Get("Main.FilesColumn", "Files");
        public static string SampleFileColumn => StringResources.Get("Main.SampleFileColumn", "Sample file");
        public static string BrowseDialogFilter => StringResources.Get("Main.BrowseDialogFilter", "SnowRunner pak (*.pak)|*.pak|All files (*.*)|*.*");
        public static string LoadingPakStatus => StringResources.Get("Main.LoadingPakStatus", "Loading pak...");
        public static string LoadErrorTitle => StringResources.Get("Main.LoadErrorTitle", "Load error");

        public static string BaselineTitle => StringResources.Get("Main.BaselineTitle", "Baseline required");
        public static string BaselineWarning => StringResources.Get("Main.BaselineWarning", "Choose your unmodified original initial.pak (Steam, GOG, Epic, Xbox, etc.). The app saves a read-only baseline for that edition and remembers this file as the one you will edit.");
        public static string BaselineReadyTitle => StringResources.Get("Main.BaselineReadyTitle", "Baseline ready");
        public static string BaselineReadyNote => StringResources.Get("Main.BaselineReadyNote", "Baseline is healthy and ready. Keep the baseline file read-only.");
        public static string BaselineMissingShort => StringResources.Get("Main.BaselineMissingShort", "Baseline is not set. On Home, use Set baseline from original.");
        public static string SetBaselineFromOriginal => StringResources.Get("Main.SetBaselineFromOriginal", "Set baseline from original...");
        public static string ChangeLocation => StringResources.Get("Main.ChangeLocation", "Change location...");
        public static string RestoreFullBaseline => StringResources.Get("Main.RestoreFullBaseline", "Restore full baseline");
        public static string SelectOriginalPakDialogTitle => StringResources.Get("Main.SelectOriginalPakDialogTitle", "Select unmodified original initial.pak");
        public static string ChangeLocationDialogTitle => StringResources.Get("Main.ChangeLocationDialogTitle", "Select initial.pak for another store/location");
        public static string BaselineUpdatedTitle => StringResources.Get("Main.BaselineUpdatedTitle", "Baseline set");
        public static string LocationChangedTitle => StringResources.Get("Main.LocationChangedTitle", "Location changed");
        public static string BaselineErrorTitle => StringResources.Get("Main.BaselineErrorTitle", "Baseline error");
        public static string RestoreFullBaselineConfirmTitle => StringResources.Get("Main.RestoreFullBaselineConfirmTitle", "Restore full baseline?");
        public static string RestoreFullBaselineConfirmMessage => StringResources.Get("Main.RestoreFullBaselineConfirmMessage", "This replaces the entire working initial.pak with the read-only baseline copy. All tuning changes in the pak will be lost. This cannot be undone from inside the tuner.\n\nYour saved tuning profile is kept so you can reapply the changes afterwards.");
        public static string RestoreFullBaselineSuccessTitle => StringResources.Get("Main.RestoreFullBaselineSuccessTitle", "Pak restored");

        public static string LoadSuccessStatus(int entryCount) =>
            $"Loaded successfully: {entryCount:N0} entries.";

        public static string ErrorStatus(string message) => $"Error: {message}";

        public static string AutoLoadedStatus(string editionDisplayName) =>
            $"Loaded saved {editionDisplayName} workspace.";

        public static string BaselineSetStatus(string editionDisplayName) =>
            $"Baseline set for {editionDisplayName}.";

        public static string LocationChangedStatus(string editionDisplayName) =>
            $"Switched to {editionDisplayName}.";

        public static string BaselineCreatedMessage(
            string editionDisplayName,
            string workingPakPath,
            string baselinePath) =>
            $"Baseline created for {editionDisplayName}.{Environment.NewLine}{Environment.NewLine}" +
            $"Working pak:{Environment.NewLine}{workingPakPath}{Environment.NewLine}{Environment.NewLine}" +
            $"Read-only baseline:{Environment.NewLine}{baselinePath}";

        public static string LocationChangedMessage(
            string editionDisplayName,
            string workingPakPath,
            string baselinePath,
            bool baselineCreated) =>
            $"Now editing the {editionDisplayName} edition.{Environment.NewLine}{Environment.NewLine}" +
            $"Working pak:{Environment.NewLine}{workingPakPath}{Environment.NewLine}{Environment.NewLine}" +
            (baselineCreated
                ? $"No previous baseline existed for this edition, so a new read-only baseline was created:{Environment.NewLine}{baselinePath}"
                : $"Using the existing read-only baseline for this edition:{Environment.NewLine}{baselinePath}");

        public static string BaselineReadyStatus(string editionDisplayName, string fileName, DateTime lastWriteUtc) =>
            $"Baseline OK for {editionDisplayName} ({fileName}, {lastWriteUtc.ToLocalTime():yyyy-MM-dd HH:mm}).";

        public static string WorkingPakStatus(string editionDisplayName, string workingPakPath) =>
            $"Editing ({editionDisplayName}): {workingPakPath}";

        public static string RestoreFullBaselineMessage => StringResources.Get("Main.RestoreFullBaselineMessage", "The entire initial.pak was restored from the baseline. You can reapply saved changes from Home or Settings.");

        public static string OverviewDetails(
            string filePath,
            string fileSize,
            int totalEntries,
            int xmlEntries,
            int dlcPackages,
            string uncompressedSize,
            IEnumerable<string> topLevelFolders) =>
            $"File: {filePath}{Environment.NewLine}" +
            $"Size: {fileSize}{Environment.NewLine}" +
            $"Entries: {totalEntries:N0}{Environment.NewLine}" +
            $"XML files: {xmlEntries:N0}{Environment.NewLine}" +
            $"DLC packages: {dlcPackages}{Environment.NewLine}" +
            $"Unpacked: {uncompressedSize}{Environment.NewLine}{Environment.NewLine}" +
            $"Top level:{Environment.NewLine}" +
            string.Join(Environment.NewLine, topLevelFolders.Select(folder => $"• {folder}"));
    }

    public static class Parts
    {
        public static string Winch => StringResources.Get("Parts.Winch", "Winch");
        public static string Engine => StringResources.Get("Parts.Engine", "Engine");
        public static string Gearbox => StringResources.Get("Parts.Gearbox", "Gearbox");
        public static string Suspension => StringResources.Get("Parts.Suspension", "Suspension");
        public static string Tires => StringResources.Get("Parts.Tires", "Tires");
        public static string ComingSoon => StringResources.Get("Parts.ComingSoon", "Coming soon.");
        public static string LoadPakHint => StringResources.Get("Parts.LoadPakHint", "Load an initial.pak on the Home page first.");
        public static string Loading => StringResources.Get("Parts.Loading", "Loading…");
    }

    public static class General
    {
        public static string Title => StringResources.Get("General.Title", "General tuning");
        public static string LoadPakHint => StringResources.Get("General.LoadPakHint", "Load an initial.pak on the Home page first.");
        public static string AssetsMissing => StringResources.Get("General.AssetsMissing", "General mod assets were not found.");
        public static string CameraTitle => StringResources.Get("General.CameraTitle", "Camera collisions");
        public static string CameraHint => StringResources.Get("General.CameraHint", "Sets ClipCamera on map object models. Pass through stops the chase camera from clipping against buildings, bridges, and similar objects.");
        public static string CameraModeLabel => StringResources.Get("General.CameraModeLabel", "Mode");
        public static string CameraCollisionsOff => StringResources.Get("General.CameraCollisionsOff", "Pass through objects");
        public static string CameraCollisionsOn => StringResources.Get("General.CameraCollisionsOn", "Game default (collisions)");
        public static string ApplyCamera => StringResources.Get("General.ApplyCamera", "Apply camera setting");
        public static string RestoreCameraBaseline => StringResources.Get("General.RestoreCameraBaseline", "Restore camera baseline");
        public static string RockTitle => StringResources.Get("General.RockTitle", "Trail rock size");
        public static string RockHint => StringResources.Get("General.RockHint", "Adjusts trail pebbles (SmallRock plants: small_rock, small_forest_rock, burnt_small_rock — base game and DLC). Left = no collision; right = vanilla baseline.");
        public static string RockSizeDefault => StringResources.Get("General.RockSizeDefault", "Rock physics: Vanilla (baseline)");
        public static string ApplyRockSize => StringResources.Get("General.ApplyRockSize", "Apply rock size");
        public static string RestoreRockBaseline => StringResources.Get("General.RestoreRockBaseline", "Restore rock baseline");
        public static string NoChangesToSave => StringResources.Get("General.NoChangesToSave", "No general changes were detected to save.");
        public static string SaveSuccessTitle => StringResources.Get("General.SaveSuccessTitle", "Saved successfully");
        public static string SaveErrorTitle => StringResources.Get("General.SaveErrorTitle", "Save error");
        public static string CameraSaved(int files) =>
            $"Camera collisions updated in {files} model file(s). Reload the game to test.";
        public static string RockSaved(int files) =>
            $"Trail rock settings updated in {files} pak file(s). Reload the game to test.";
        public static string LoadedStatus(int cameraModels, double rockScale) =>
            $"Detected {cameraModels} camera-eligible models; reference rock scale {rockScale:0%}.";
    }

    public static class Vehicles
    {
        public static string All => StringResources.Get("Vehicles.All", "All");
        public static string Highway => StringResources.Get("Vehicles.Highway", "Highway");
        public static string HeavyDuty => StringResources.Get("Vehicles.HeavyDuty", "Heavy Duty");
        public static string Heavy => StringResources.Get("Vehicles.Heavy", "Heavy");
        public static string Offroad => StringResources.Get("Vehicles.Offroad", "Offroad");
        public static string Scout => StringResources.Get("Vehicles.Scout", "Scout");
        public static string BackToList => StringResources.Get("Vehicles.BackToList", "← Back to list");
        public static string ManufacturerLabel => StringResources.Get("Vehicles.ManufacturerLabel", "Manufacturer");
        public static string BasedOnLabel => StringResources.Get("Vehicles.BasedOnLabel", "Based on");
        public static string RoleLabel => StringResources.Get("Vehicles.RoleLabel", "Role");
        public static string YearsLabel => StringResources.Get("Vehicles.YearsLabel", "Year");
        public static string CountryLabel => StringResources.Get("Vehicles.CountryLabel", "Country");
        public static string CountryHint => StringResources.Get("Vehicles.CountryHint", "Brand origin of the real-world basis.");
        public static string CatalogMissing => StringResources.Get("Vehicles.CatalogMissing", "Vehicle catalog assets were not found.");
        public static string SearchPlaceholder => StringResources.Get("Vehicles.SearchPlaceholder", "Search by name…");
        public static string GlobalMultipliersTitle => StringResources.Get("Vehicles.GlobalMultipliersTitle", "Global multipliers (relative to the baseline values)");
        public static string FuelMultiplierDefault => StringResources.Get("Vehicles.FuelMultiplierDefault", "Fuel tank: 1 (baseline)");
        public static string FrontSteerGlobalDefault => StringResources.Get("Vehicles.FrontSteerGlobalDefault", "Front steer: Default (baseline)");
        public static string FrontSteerGlobalMin => StringResources.Get("Vehicles.FrontSteerGlobalMin", "Front steer: Min (10°)");
        public static string FrontSteerGlobalMax => StringResources.Get("Vehicles.FrontSteerGlobalMax", "Front steer: Max (60°)");
        public static string ResponsivenessMultiplierDefault => StringResources.Get("Vehicles.ResponsivenessMultiplierDefault", "Responsiveness: 1 (baseline)");
        public static string PriceMultiplierDefault => StringResources.Get("Vehicles.PriceMultiplierDefault", "Store price: 1 (baseline)");
        public static string ApplyGlobalMultipliers => StringResources.Get("Vehicles.ApplyGlobalMultipliers", "Apply to all vehicles");
        public static string GlobalMultipliersHint => StringResources.Get("Vehicles.GlobalMultipliersHint", "Fuel tank, store price, and responsiveness scale from baseline. Front steer uses three presets: Min (10°), Default (baseline per truck), Max (60°). Independent of the category filter below.");
        public static string StoreUnlocksTitle => StringResources.Get("Vehicles.StoreUnlocksTitle", "Store unlocks (all vehicles)");
        public static string StoreUnlocksHint => StringResources.Get("Vehicles.StoreUnlocksHint", "Apply region and rank unlocks across every truck XML. Region-free makes trucks appear in every regional truck store. Unlock all sets UnlockByRank to 0.");
        public static string ReleaseRegionLock => StringResources.Get("Vehicles.ReleaseRegionLock", "Release region lock (all stores)");
        public static string UnlockAllVehicles => StringResources.Get("Vehicles.UnlockAllVehicles", "Unlock all vehicles (rank 0)");
        public static string ApplyStoreUnlocks => StringResources.Get("Vehicles.ApplyStoreUnlocks", "Apply store unlocks");
        public static string LoadPakForGlobalHint => StringResources.Get("Vehicles.LoadPakForGlobalHint", "Load an initial.pak on the Home page to enable global vehicle multipliers.");
        public static string GlobalMultipliersAppliedStatus(int changedTrucks, int updatedFiles) =>
            $"Applied global vehicle multipliers to {changedTrucks} truck(s) across {updatedFiles} file(s).";
        public static string GlobalMultipliersSavedMessage(int changedTrucks, int updatedFiles) =>
            $"Global vehicle multipliers applied to {changedTrucks} truck(s) ({updatedFiles} file(s) updated).";
        public static string StoreUnlocksSavedMessage(int changedTrucks, int updatedFiles) =>
            $"Store unlocks applied to {changedTrucks} truck(s) ({updatedFiles} file(s) updated).";
        public static string StoreUnlocksNothingSelected => StringResources.Get("Vehicles.StoreUnlocksNothingSelected", "Select at least one store unlock option before applying.");
        public static string TuningTitle => StringResources.Get("Vehicles.TuningTitle", "Vehicle tuning");
        public static string FuelTankLabel => StringResources.Get("Vehicles.FuelTankLabel", "Fuel tank");
        public static string FuelUnit => StringResources.Get("Vehicles.FuelUnit", "L");
        public static string StorePriceLabel => StringResources.Get("Vehicles.StorePriceLabel", "Store price");
        public static string RegionFreeLabel => StringResources.Get("Vehicles.RegionFreeLabel", "Region-free");
        public static string RegionFreeHint => StringResources.Get("Vehicles.RegionFreeHint", "When checked, this truck is listed in every regional truck store (GameData Country = all regions).");
        public static string StoreRegionsLabel => StringResources.Get("Vehicles.StoreRegionsLabel", "Store regions");
        public static string UnlockRankLabel => StringResources.Get("Vehicles.UnlockRankLabel", "Unlock rank");
        public static string UnlockRankHint => StringResources.Get("Vehicles.UnlockRankHint", "Player rank required in the truck store (GameData UnlockByRank). Use 0 to clear the rank gate.");
        public static string FrontSteerLabel => StringResources.Get("Vehicles.FrontSteerLabel", "Front steer");
        public static string RearSteerLabel => StringResources.Get("Vehicles.RearSteerLabel", "Rear steer");
        public static string SteerAngleUnit => StringResources.Get("Vehicles.SteerAngleUnit", "°");
        public static string ResponsivenessLabel => StringResources.Get("Vehicles.ResponsivenessLabel", "Responsiveness");
        public static string FrontSteerHint => StringResources.Get("Vehicles.FrontSteerHint", "Maximum turn for front steering wheels. Applies to all front steer axles. Range: 0° to 90°.");
        public static string RearSteerHint => StringResources.Get("Vehicles.RearSteerHint", "Rear-axle counter-steer (turns opposite to the front). Applies to all rear steer axles. Range: −90° to 0°.");
        public static string ResponsivenessHint => StringResources.Get("Vehicles.ResponsivenessHint", "How quickly the steering wheel returns to center (TruckData Responsiveness). Range: 0–1; higher = snappier.");
        public static string DiffLockLabel => StringResources.Get("Vehicles.DiffLockLabel", "Diff lock");
        public static string DriveLabel => StringResources.Get("Vehicles.DriveLabel", "Drive");
        public static string DiffLockAlwaysOn => StringResources.Get("Vehicles.DiffLockAlwaysOn", "Always on");
        public static string DiffLockSwitchable => StringResources.Get("Vehicles.DiffLockSwitchable", "Switchable");
        public static string DiffLockUpgradeable => StringResources.Get("Vehicles.DiffLockUpgradeable", "Upgradeable");
        public static string DiffLockNone => StringResources.Get("Vehicles.DiffLockNone", "None");
        public static string DriveRwd => StringResources.Get("Vehicles.DriveRwd", "RWD");
        public static string DriveAlwaysAwd => StringResources.Get("Vehicles.DriveAlwaysAwd", "Always AWD");
        public static string DriveSelectableAwd => StringResources.Get("Vehicles.DriveSelectableAwd", "Selectable AWD");
        public static string DiffLockHintNative => StringResources.Get("Vehicles.DiffLockHintNative", "Switchable and Upgradeable use the truck's built-in diff-lock upgrade slot.");
        public static string DiffLockHintSimple => StringResources.Get("Vehicles.DiffLockHintSimple", "This truck has no diff-lock upgrade in the game. Only None or Always on can be set.");
        public static string DriveHint => StringResources.Get("Vehicles.DriveHint", "RWD matches the garage \"AWD: No\". Selectable AWD enables the in-cab switch (Torque full). Upgradeable AWD in-game also needs a transfer-case addon socket; connectable alone is not enough.");
        public static string SaveChanges => StringResources.Get("Vehicles.SaveChanges", "Save changes");
        public static string RestoreThisVehicle => StringResources.Get("Vehicles.RestoreThisVehicle", "Restore this vehicle to baseline");
        public static string RestoreAllVehicles => StringResources.Get("Vehicles.RestoreAllVehicles", "Restore all vehicles to baseline");
        public static string RestoreAllVehiclesConfirmTitle => StringResources.Get("Vehicles.RestoreAllVehiclesConfirmTitle", "Restore all vehicles?");
        public static string RestoreAllVehiclesConfirmMessage => StringResources.Get("Vehicles.RestoreAllVehiclesConfirmMessage", "This restores every truck XML from your baseline pak (fuel, steer, price, unlocks, drive, and other vehicle edits). Continue?");
        public static string RestoreAllVehiclesSuccessTitle => StringResources.Get("Vehicles.RestoreAllVehiclesSuccessTitle", "Vehicles restored");
        public static string RestoreAllVehiclesSavedMessage(int changedTrucks, int updatedFiles) =>
            $"Restored {changedTrucks} vehicle(s) from baseline ({updatedFiles} file(s) updated).";
        public static string LoadPakHint => StringResources.Get("Vehicles.LoadPakHint", "Load an initial.pak on the Home page first.");
        public static string TruckNotFound => StringResources.Get("Vehicles.TruckNotFound", "This vehicle could not be matched to a truck XML in the loaded pak.");
        public static string InvalidFuel => StringResources.Get("Vehicles.InvalidFuel", "Fuel tank must be a whole number of liters (1–10000).");
        public static string InvalidResponsiveness => StringResources.Get("Vehicles.InvalidResponsiveness", "Responsiveness must be between 0 and 1.");
        public static string InvalidFrontSteer => StringResources.Get("Vehicles.InvalidFrontSteer", "Front steer must be between 0 and 90 degrees.");
        public static string InvalidRearSteer => StringResources.Get("Vehicles.InvalidRearSteer", "Rear steer must be between -90 and 0 degrees.");
        public static string InvalidPrice => StringResources.Get("Vehicles.InvalidPrice", "Store price must be a whole number from 0 to 9,999,999.");
        public static string InvalidUnlockRank => StringResources.Get("Vehicles.InvalidUnlockRank", "Unlock rank must be a whole number from 0 to 30.");
        public static string SaveSuccessTitle => StringResources.Get("Vehicles.SaveSuccessTitle", "Saved successfully");
        public static string SaveErrorTitle => StringResources.Get("Vehicles.SaveErrorTitle", "Save error");
        public static string RestoreSuccessTitle => StringResources.Get("Vehicles.RestoreSuccessTitle", "Vehicle restored");
        public static string NoChangesToSave => StringResources.Get("Vehicles.NoChangesToSave", "No vehicle changes were detected to save.");
        public static string CountLabel(int count) => $"{count} vehicles";
        public static string SavedMessage() => "Vehicle tuning saved.";
        public static string RestoredMessage() => "This vehicle was restored from the baseline.";
    }

    public static class SafeRange
    {
        public static string InvalidNumber => StringResources.Get("SafeRange.InvalidNumber", "Enter a valid number.");

        public static string BaselineLabel(string formattedValue) =>
            $"Baseline: {formattedValue}";

        public static string AllowedLabel(string minFormatted, string maxFormatted) =>
            $"Allowed: {minFormatted}–{maxFormatted}";

        public static string ZoneMessage(SafeRangeZone zone) =>
            zone switch
            {
                SafeRangeZone.Normal => "Within normal range",
                SafeRangeZone.Warning => "Outside typical range — check before saving",
                SafeRangeZone.Extreme => "Extreme value — may cause unexpected behavior",
                _ => "Outside allowed range",
            };
    }

    public static class Settings
    {
        public static string Title => StringResources.Get("Settings.Title", "Settings");
        public static string AppearanceTitle => StringResources.Get("Settings.AppearanceTitle", "Appearance");
        public static string AppearanceHint => StringResources.Get("Settings.AppearanceHint", "Choose the app color theme. System follows Windows light/dark mode.");
        public static string ThemeLabel => StringResources.Get("Settings.ThemeLabel", "Theme");
        public static string ThemeSystem => StringResources.Get("Settings.ThemeSystem", "System");
        public static string ThemeDark => StringResources.Get("Settings.ThemeDark", "Dark");
        public static string ThemeLight => StringResources.Get("Settings.ThemeLight", "Light");
        public static string LanguageTitle => StringResources.Get("Settings.LanguageTitle", "Language");
        public static string LanguageHint => StringResources.Get("Settings.LanguageHint", "Choose the app language. Restart the app to apply a change.");
        public static string LanguageLabel => StringResources.Get("Settings.LanguageLabel", "Language");
        public static string LanguageRestartTitle => StringResources.Get("Settings.LanguageRestartTitle", "Restart required");
        public static string LanguageRestartMessage => StringResources.Get("Settings.LanguageRestartMessage", "Restart SnowRunner Tuning Shop to apply the new language.");
        public static string WorkspaceTitle => StringResources.Get("Settings.WorkspaceTitle", "Workspace");
        public static string WorkspaceHint => StringResources.Get("Settings.WorkspaceHint", "Restore the working pak from the baseline, refresh the baseline after a game update, or reapply your saved tuning profile.");
        public static string AboutTitle => StringResources.Get("Settings.AboutTitle", "About & support");
        public static string AboutHint => StringResources.Get("Settings.AboutHint", "Project website, releases, and optional support via PayPal.");
        public static string InstalledVersion => StringResources.Format("Settings.InstalledVersion", "Installed version: {0}", AppInfo.Version);
        public static string CheckForUpdates => StringResources.Get("Settings.CheckForUpdates", "Check for updates");
        public static string DownloadUpdate => StringResources.Get("Settings.DownloadUpdate", "Download update");
        public static string SkipThisVersion => StringResources.Get("Settings.SkipThisVersion", "Skip this version");
        public static string UpdateAvailableTitle => StringResources.Get("Settings.UpdateAvailableTitle", "App update available");
        public static string CheckingForUpdates => StringResources.Get("Settings.CheckingForUpdates", "Checking for updates…");
        public static string UpToDate => StringResources.Get("Settings.UpToDate", "You are running the latest version.");
        public static string UpdateCheckFailed => StringResources.Get("Settings.UpdateCheckFailed", "Could not check for updates. Try again later.");
        public static string UpdateAvailableMessage(string latest) =>
            $"Version {latest} is available (you have {AppInfo.Version}). Download and install it from inside the app.";
        public static string UpdateAvailableStatus(string latest) =>
            $"Update available: {latest}.";
        public static string OpenWebsite => StringResources.Get("Settings.OpenWebsite", "Open website");
        public static string DonatePayPal => StringResources.Get("Settings.DonatePayPal", "Donate with PayPal");
        public static string DonateWith => StringResources.Get("Settings.DonateWith", "Donate with");
        public static string FeedbackTitle => StringResources.Get("Settings.FeedbackTitle", "Feedback");
        public static string FeedbackHint => StringResources.Get("Settings.FeedbackHint", "Found a bug or have an idea? Open an issue on the GitHub tracker.");
        public static string OpenIssueTracker => StringResources.Get("Settings.OpenIssueTracker", "Open issue tracker");

        public static string DebugCrashTitle => StringResources.Get("Settings.DebugCrashTitle", "Debug — crash report test");
        public static string DebugCrashHint => StringResources.Get("Settings.DebugCrashHint", "Debug builds only. Triggers a handled test exception and opens the crash report dialog.");
        public static string DebugCrashUiButton => StringResources.Get("Settings.DebugCrashUiButton", "Test crash (Settings)");
        public static string DebugCrashVehicleButton => StringResources.Get("Settings.DebugCrashVehicleButton", "Test crash (vehicle page)");
    }

    public static class UpdateDownload
    {
        public static string Title => StringResources.Get("UpdateDownload.Title", "Download update");
        public static string DownloadingTitle => StringResources.Get("UpdateDownload.DownloadingTitle", "Downloading update…");
        public static string DownloadingDetail => StringResources.Get("UpdateDownload.DownloadingDetail", "Downloading the installer. Please wait.");
        public static string DownloadingDetailVersion(string version) =>
            $"Downloading version {version}. Please wait.";
        public static string Cancelling => StringResources.Get("UpdateDownload.Cancelling", "Cancelling download…");
        public static string CompleteTitle => StringResources.Get("UpdateDownload.CompleteTitle", "Download complete");
        public static string CompleteDetail => StringResources.Get("UpdateDownload.CompleteDetail", "The installer is ready. Choose Update and restart to close this app and run the setup.");
        public static string FailedTitle => StringResources.Get("UpdateDownload.FailedTitle", "Download failed");
        public static string UpdateAndRestart => StringResources.Get("UpdateDownload.UpdateAndRestart", "Update and restart");
        public static string Cancel => StringResources.Get("UpdateDownload.Cancel", "Cancel");
        public static string Close => StringResources.Get("UpdateDownload.Close", "Close");

        public static string ProgressLabel(double percent, string received, string total) =>
            $"{percent:0}% — {received} / {total}";

        public static string ProgressIndeterminate(string received) =>
            $"{received} downloaded…";
    }

    public static class Workspace
    {
        public static string RefreshBaseline => StringResources.Get("Workspace.RefreshBaseline", "Refresh baseline from game");
        public static string ReapplySavedChanges => StringResources.Get("Workspace.ReapplySavedChanges", "Reapply saved changes");
        public static string RefreshBaselineTitle => StringResources.Get("Workspace.RefreshBaselineTitle", "Refresh baseline");
        public static string RefreshBaselineConfirmTitle => StringResources.Get("Workspace.RefreshBaselineConfirmTitle", "Refresh baseline from the working pak?");
        public static string RefreshBaselineGameUpdateConfirm => StringResources.Get("Workspace.RefreshBaselineGameUpdateConfirm", "The game appears to have replaced initial.pak. This saves the current file as the new read-only baseline. Use this only when the pak is the new unmodified vanilla file.\n\nYour saved tuning profile is kept so you can reapply afterwards. Continue?");
        public static string RefreshBaselineUnknownConfirm => StringResources.Get("Workspace.RefreshBaselineUnknownConfirm", "The working pak differs from the baseline. Replace the baseline with the current file?\n\nOnly continue if this is an unmodified vanilla initial.pak (for example after a game update).");
        public static string RefreshBaselineSuccessMessage => StringResources.Get("Workspace.RefreshBaselineSuccessMessage", "The baseline was updated from the current working pak. You can reapply saved changes next.");
        public static string ReapplyTitle => StringResources.Get("Workspace.ReapplyTitle", "Reapply saved changes");
        public static string ReapplyConfirmTitle => StringResources.Get("Workspace.ReapplyConfirmTitle", "Reapply saved changes?");
        public static string ReapplyConfirmMessage => StringResources.Get("Workspace.ReapplyConfirmMessage", "This writes your saved tuning profile back into the working initial.pak. Files that no longer exist after a game update will be skipped and listed in a report. Continue?");
        public static string ReapplySuccessTitle => StringResources.Get("Workspace.ReapplySuccessTitle", "Saved changes reapplied");
        public static string NoSavedProfile => StringResources.Get("Workspace.NoSavedProfile", "No saved tuning profile.");
        public static string GameUpdateTitle => StringResources.Get("Workspace.GameUpdateTitle", "Game update detected");
        public static string GameUpdateMessage => StringResources.Get("Workspace.GameUpdateMessage", "SnowRunner appears to have replaced initial.pak with a new vanilla file. Refresh the baseline from this file, then reapply your saved tuning changes. Avoid saving new edits until you reapply — a new save would replace the saved profile.");
        public static string UnknownChangeTitle => StringResources.Get("Workspace.UnknownChangeTitle", "Working pak changed");
        public static string UnknownChangeMessage => StringResources.Get("Workspace.UnknownChangeMessage", "The working initial.pak differs from the baseline and has no Tuning Shop marker. If the game updated, refresh the baseline. If you edited the file elsewhere, set a new baseline from an original pak instead.");
        public static string ReadyToReapplyTitle => StringResources.Get("Workspace.ReadyToReapplyTitle", "Saved changes ready to reapply");
        public static string ReadyToReapplyMessage => StringResources.Get("Workspace.ReadyToReapplyMessage", "The working pak matches the baseline. You can reapply your saved tuning profile. Avoid saving new edits first — that would replace the saved profile.");
        public static string InconsistentMarkerTitle => StringResources.Get("Workspace.InconsistentMarkerTitle", "Marker mismatch");
        public static string InconsistentMarkerMessage => StringResources.Get("Workspace.InconsistentMarkerMessage", "The working pak matches the baseline but still contains a Tuning Shop marker. Restore the full baseline to clean it, or reapply saved changes if a profile exists.");

        public static string ProfileStatus(int entryCount) =>
            $"Saved profile: {entryCount} file(s).";

        public static string StatusLine(WorkspaceHealthKind kind, int profileEntryCount) =>
            kind switch
            {
                WorkspaceHealthKind.GameUpdateDetected =>
                    "Game update detected. Refresh the baseline, then reapply saved changes.",
                WorkspaceHealthKind.UnknownExternalChange =>
                    "Working pak differs from the baseline (no Tuning Shop marker).",
                WorkspaceHealthKind.ReadyToReapply =>
                    $"Saved profile ready to reapply ({profileEntryCount} file(s)).",
                WorkspaceHealthKind.HealthyTuned =>
                    $"Workspace healthy — tuned ({profileEntryCount} saved file(s)).",
                WorkspaceHealthKind.HealthyVanilla =>
                    "Workspace healthy — working pak matches the baseline.",
                WorkspaceHealthKind.InconsistentMarker =>
                    "Pak matches the baseline but still has a Tuning Shop marker.",
                _ => "Workspace is not ready.",
            };

        public static string ReapplyReport(TuningProfileReapplyResult result)
        {
            var lines = new List<string>
            {
                $"Applied {result.AppliedCount} file(s).",
            };

            AppendPathList(lines, "Skipped (no longer in the pak)", result.MissingEntryPaths);
            AppendPathList(lines, "Failed", result.FailedEntryPaths);
            return string.Join(Environment.NewLine, lines);
        }

        private static void AppendPathList(List<string> lines, string heading, IReadOnlyList<string> paths)
        {
            if (paths.Count == 0)
            {
                return;
            }

            lines.Add(string.Empty);
            lines.Add($"{heading}: {paths.Count}");
            const int limit = 12;
            foreach (var path in paths.Take(limit))
            {
                lines.Add($"  {path}");
            }

            if (paths.Count > limit)
            {
                lines.Add($"  … and {paths.Count - limit} more.");
            }
        }
    }

    public static class Engine
    {
        public static string GlobalMultipliersTitle => StringResources.Get("Engine.GlobalMultipliersTitle", "Global multipliers (relative to the baseline values)");
        public static string TorqueMultiplierDefault => StringResources.Get("Engine.TorqueMultiplierDefault", "Torque: 1 (baseline)");
        public static string FuelMultiplierDefault => StringResources.Get("Engine.FuelMultiplierDefault", "Fuel consumption: 1 (baseline)");
        public static string DamageMultiplierDefault => StringResources.Get("Engine.DamageMultiplierDefault", "Damage capacity: 1 (baseline)");
        public static string ResponsivenessMultiplierDefault => StringResources.Get("Engine.ResponsivenessMultiplierDefault", "Responsiveness: 1 (baseline)");
        public static string Apply => StringResources.Get("Engine.Apply", "Apply");
        public static string SaveIndividualChanges => StringResources.Get("Engine.SaveIndividualChanges", "Save individual changes");
        public static string RestoreEnginesToBaseline => StringResources.Get("Engine.RestoreEnginesToBaseline", "Restore engines to baseline");
        public static string RefreshList => StringResources.Get("Engine.RefreshList", "Refresh list");
        public static string FilterPlaceholder => StringResources.Get("Engine.FilterPlaceholder", "Filter category, name, set, used by…");
        public static string CategoryColumn => StringResources.Get("Engine.CategoryColumn", "Category");
        public static string NameColumn => StringResources.Get("Engine.NameColumn", "Name");
        public static string UsedByColumn => StringResources.Get("Engine.UsedByColumn", "Used by");
        public static string PriceColumn => StringResources.Get("Engine.PriceColumn", "Price");
        public static string TorqueColumn => StringResources.Get("Engine.TorqueColumn", "Torque");
        public static string FuelColumn => StringResources.Get("Engine.FuelColumn", "Fuel");
        public static string DamageColumn => StringResources.Get("Engine.DamageColumn", "Damage");
        public static string ResponsivenessColumn => StringResources.Get("Engine.ResponsivenessColumn", "Responsiveness");
        public static string NoData => StringResources.Get("Engine.NoData", "No engine data loaded.");
        public static string LoadPakFirst => StringResources.Get("Engine.LoadPakFirst", "Load an initial.pak file first.");
        public static string SaveSuccessTitle => StringResources.Get("Engine.SaveSuccessTitle", "Saved successfully");
        public static string SaveErrorTitle => StringResources.Get("Engine.SaveErrorTitle", "Save error");
        public static string LoadErrorTitle => StringResources.Get("Engine.LoadErrorTitle", "Load error");
        public static string RestoreEnginesSuccessTitle => StringResources.Get("Engine.RestoreEnginesSuccessTitle", "Engines restored");

        public static string LoadedCount(int count) => $"{count} engines loaded from pak.";

        public static string LoadedStatus(int count) => $"{count} engines loaded.";

        public static string LoadErrorStatus(string message) => $"Engine load error: {message}";

        public static string MultipliersAppliedStatus(int changedEngines, int updatedFiles) =>
            $"Multipliers applied. Updated engines: {changedEngines}, files: {updatedFiles}.";

        public static string MultipliersSavedMessage(int changedEngines, int updatedFiles) =>
            $"Engine settings saved.{Environment.NewLine}{Environment.NewLine}" +
            $"Updated engines: {changedEngines}{Environment.NewLine}" +
            $"Updated files: {updatedFiles}";

        public static string IndividualSavedStatus(int changedEngines, int updatedFiles) =>
            $"Individual changes saved. Engines: {changedEngines}, files: {updatedFiles}.";

        public static string IndividualSavedMessage(int changedEngines) =>
            $"Individual engine changes saved.{Environment.NewLine}{Environment.NewLine}" +
            $"Updated engines: {changedEngines}";

        public static string RestoreEnginesMessage(int changedEngines, int updatedFiles) =>
            $"Engine values were restored from the baseline.{Environment.NewLine}{Environment.NewLine}" +
            $"Updated engines: {changedEngines}{Environment.NewLine}" +
            $"Updated files: {updatedFiles}";
    }

    public static class Gearbox
    {
        public static string GlobalMultipliersTitle => StringResources.Get("Gearbox.GlobalMultipliersTitle", "Global multipliers (relative to the baseline values)");
        public static string FuelMultiplierDefault => StringResources.Get("Gearbox.FuelMultiplierDefault", "Fuel consumption: 1 (baseline)");
        public static string IdleMultiplierDefault => StringResources.Get("Gearbox.IdleMultiplierDefault", "Idle fuel modifier: 1 (baseline)");
        public static string AwdMultiplierDefault => StringResources.Get("Gearbox.AwdMultiplierDefault", "AWD fuel penalty: 1 (baseline)");
        public static string Apply => StringResources.Get("Gearbox.Apply", "Apply");
        public static string SaveIndividualChanges => StringResources.Get("Gearbox.SaveIndividualChanges", "Save individual changes");
        public static string RestoreGearboxesToBaseline => StringResources.Get("Gearbox.RestoreGearboxesToBaseline", "Restore gearboxes to baseline");
        public static string RefreshList => StringResources.Get("Gearbox.RefreshList", "Refresh list");
        public static string FilterPlaceholder => StringResources.Get("Gearbox.FilterPlaceholder", "Filter category, name, set, used by…");
        public static string CategoryColumn => StringResources.Get("Gearbox.CategoryColumn", "Category");
        public static string NameColumn => StringResources.Get("Gearbox.NameColumn", "Name");
        public static string UsedByColumn => StringResources.Get("Gearbox.UsedByColumn", "Used by");
        public static string PriceColumn => StringResources.Get("Gearbox.PriceColumn", "Price");
        public static string FuelColumn => StringResources.Get("Gearbox.FuelColumn", "Fuel");
        public static string IdleColumn => StringResources.Get("Gearbox.IdleColumn", "Idle");
        public static string AwdColumn => StringResources.Get("Gearbox.AwdColumn", "AWD");
        public static string NoData => StringResources.Get("Gearbox.NoData", "No gearbox data loaded.");
        public static string LoadPakFirst => StringResources.Get("Gearbox.LoadPakFirst", "Load an initial.pak file first.");
        public static string SaveSuccessTitle => StringResources.Get("Gearbox.SaveSuccessTitle", "Saved successfully");
        public static string SaveErrorTitle => StringResources.Get("Gearbox.SaveErrorTitle", "Save error");
        public static string LoadErrorTitle => StringResources.Get("Gearbox.LoadErrorTitle", "Load error");
        public static string RestoreGearboxesSuccessTitle => StringResources.Get("Gearbox.RestoreGearboxesSuccessTitle", "Gearboxes restored");

        public static string LoadedCount(int count) => $"{count} gearboxes loaded from pak.";

        public static string MultipliersSavedMessage(int changedGearboxes, int updatedFiles) =>
            $"Gearbox settings saved.{Environment.NewLine}{Environment.NewLine}" +
            $"Updated gearboxes: {changedGearboxes}{Environment.NewLine}" +
            $"Updated files: {updatedFiles}";

        public static string IndividualSavedMessage(int changedGearboxes) =>
            $"Individual gearbox changes saved.{Environment.NewLine}{Environment.NewLine}" +
            $"Updated gearboxes: {changedGearboxes}";

        public static string RestoreGearboxesMessage(int changedGearboxes, int updatedFiles) =>
            $"Gearbox values were restored from the baseline.{Environment.NewLine}{Environment.NewLine}" +
            $"Updated gearboxes: {changedGearboxes}{Environment.NewLine}" +
            $"Updated files: {updatedFiles}";
    }

    public static class Suspension
    {
        public static string GlobalMultipliersTitle => StringResources.Get("Suspension.GlobalMultipliersTitle", "Global multipliers (relative to the baseline values)");
        public static string HeightMultiplierDefault => StringResources.Get("Suspension.HeightMultiplierDefault", "Height: 1 (baseline)");
        public static string StrengthMultiplierDefault => StringResources.Get("Suspension.StrengthMultiplierDefault", "Strength: 1 (baseline)");
        public static string DampingMultiplierDefault => StringResources.Get("Suspension.DampingMultiplierDefault", "Damping: 1 (baseline)");
        public static string DamageMultiplierDefault => StringResources.Get("Suspension.DamageMultiplierDefault", "Damage capacity: 1 (baseline)");
        public static string Apply => StringResources.Get("Suspension.Apply", "Apply");
        public static string SaveIndividualChanges => StringResources.Get("Suspension.SaveIndividualChanges", "Save individual changes");
        public static string RestoreSuspensionsToBaseline => StringResources.Get("Suspension.RestoreSuspensionsToBaseline", "Restore suspensions to baseline");
        public static string RefreshList => StringResources.Get("Suspension.RefreshList", "Refresh list");
        public static string FilterPlaceholder => StringResources.Get("Suspension.FilterPlaceholder", "Filter category, name, set, used by…");
        public static string CategoryColumn => StringResources.Get("Suspension.CategoryColumn", "Category");
        public static string NameColumn => StringResources.Get("Suspension.NameColumn", "Name");
        public static string UsedByColumn => StringResources.Get("Suspension.UsedByColumn", "Used by");
        public static string PriceColumn => StringResources.Get("Suspension.PriceColumn", "Price");
        public static string DamageColumn => StringResources.Get("Suspension.DamageColumn", "Damage");
        public static string FrontHeightColumn => StringResources.Get("Suspension.FrontHeightColumn", "F Height");
        public static string FrontStrengthColumn => StringResources.Get("Suspension.FrontStrengthColumn", "F Strength");
        public static string FrontDampingColumn => StringResources.Get("Suspension.FrontDampingColumn", "F Damping");
        public static string RearHeightColumn => StringResources.Get("Suspension.RearHeightColumn", "R Height");
        public static string RearStrengthColumn => StringResources.Get("Suspension.RearStrengthColumn", "R Strength");
        public static string RearDampingColumn => StringResources.Get("Suspension.RearDampingColumn", "R Damping");
        /// <summary>Shown when an optional numeric attribute is absent from XML.</summary>
        public static string MissingValuePlaceholder => StringResources.Get("Suspension.MissingValuePlaceholder", "n/a");
        public static string LoadPakFirst => StringResources.Get("Suspension.LoadPakFirst", "Load an initial.pak file first.");
        public static string SaveSuccessTitle => StringResources.Get("Suspension.SaveSuccessTitle", "Saved successfully");
        public static string SaveErrorTitle => StringResources.Get("Suspension.SaveErrorTitle", "Save error");
        public static string LoadErrorTitle => StringResources.Get("Suspension.LoadErrorTitle", "Load error");
        public static string RestoreSuspensionsSuccessTitle => StringResources.Get("Suspension.RestoreSuspensionsSuccessTitle", "Suspensions restored");

        public static string MultipliersSavedMessage(int changedSuspensions, int updatedFiles) =>
            $"Suspension settings saved.{Environment.NewLine}{Environment.NewLine}" +
            $"Updated suspensions: {changedSuspensions}{Environment.NewLine}" +
            $"Updated files: {updatedFiles}";

        public static string IndividualSavedMessage(int changedSuspensions) =>
            $"Individual suspension changes saved.{Environment.NewLine}{Environment.NewLine}" +
            $"Updated suspensions: {changedSuspensions}";

        public static string RestoreSuspensionsMessage(int changedSuspensions, int updatedFiles) =>
            $"Suspension values were restored from the baseline.{Environment.NewLine}{Environment.NewLine}" +
            $"Updated suspensions: {changedSuspensions}{Environment.NewLine}" +
            $"Updated files: {updatedFiles}";
    }

    public static class Tires
    {
        public static string GlobalMultipliersTitle => StringResources.Get("Tires.GlobalMultipliersTitle", "Global multipliers (relative to the baseline values)");
        public static string OnRoadFrictionMultiplierDefault => StringResources.Get("Tires.OnRoadFrictionMultiplierDefault", "On-road: 1 (baseline)");
        public static string OffRoadFrictionMultiplierDefault => StringResources.Get("Tires.OffRoadFrictionMultiplierDefault", "Off-road: 1 (baseline)");
        public static string MudFrictionMultiplierDefault => StringResources.Get("Tires.MudFrictionMultiplierDefault", "Mud: 1 (baseline)");
        public static string IgnoreIceAll => StringResources.Get("Tires.IgnoreIceAll", "Ignore ice on all tires");
        public static string Apply => StringResources.Get("Tires.Apply", "Apply");
        public static string SaveIndividualChanges => StringResources.Get("Tires.SaveIndividualChanges", "Save individual changes");
        public static string RestoreTiresToBaseline => StringResources.Get("Tires.RestoreTiresToBaseline", "Restore tires to baseline");
        public static string RefreshList => StringResources.Get("Tires.RefreshList", "Refresh list");
        public static string FilterPlaceholder => StringResources.Get("Tires.FilterPlaceholder", "Filter category, name, set, used by…");
        public static string CategoryColumn => StringResources.Get("Tires.CategoryColumn", "Category");
        public static string NameColumn => StringResources.Get("Tires.NameColumn", "Name");
        public static string UsedByColumn => StringResources.Get("Tires.UsedByColumn", "Used by");
        public static string PriceColumn => StringResources.Get("Tires.PriceColumn", "Price");
        public static string OnRoadFrictionColumn => StringResources.Get("Tires.OnRoadFrictionColumn", "On-road");
        public static string OffRoadFrictionColumn => StringResources.Get("Tires.OffRoadFrictionColumn", "Off-road");
        public static string MudFrictionColumn => StringResources.Get("Tires.MudFrictionColumn", "Mud");
        public static string IgnoreIceColumn => StringResources.Get("Tires.IgnoreIceColumn", "Ignore ice");
        public static string LoadPakFirst => StringResources.Get("Tires.LoadPakFirst", "Load an initial.pak file first.");
        public static string SaveSuccessTitle => StringResources.Get("Tires.SaveSuccessTitle", "Saved successfully");
        public static string SaveErrorTitle => StringResources.Get("Tires.SaveErrorTitle", "Save error");
        public static string LoadErrorTitle => StringResources.Get("Tires.LoadErrorTitle", "Load error");
        public static string RestoreTiresSuccessTitle => StringResources.Get("Tires.RestoreTiresSuccessTitle", "Tires restored");

        public static string MultipliersSavedMessage(int changedTires, int updatedFiles) =>
            $"Tire settings saved.{Environment.NewLine}{Environment.NewLine}" +
            $"Updated tires: {changedTires}{Environment.NewLine}" +
            $"Updated files: {updatedFiles}";

        public static string IndividualSavedMessage(int changedTires) =>
            $"Individual tire changes saved.{Environment.NewLine}{Environment.NewLine}" +
            $"Updated tires: {changedTires}";

        public static string RestoreTiresMessage(int changedTires, int updatedFiles) =>
            $"Tire values were restored from the baseline.{Environment.NewLine}{Environment.NewLine}" +
            $"Updated tires: {changedTires}{Environment.NewLine}" +
            $"Updated files: {updatedFiles}";
    }

    public static class Winch
    {
        public static string GlobalMultipliersTitle => StringResources.Get("Winch.GlobalMultipliersTitle", "Global multipliers (relative to the baseline values)");
        public static string LengthMultiplierDefault => StringResources.Get("Winch.LengthMultiplierDefault", "Length multiplier: 1 (baseline)");
        public static string StrengthMultiplierDefault => StringResources.Get("Winch.StrengthMultiplierDefault", "Strength multiplier: 1 (baseline)");
        public static string AutonomousAll => StringResources.Get("Winch.AutonomousAll", "Autonomous all");
        public static string Apply => StringResources.Get("Winch.Apply", "Apply");
        public static string SaveIndividualChanges => StringResources.Get("Winch.SaveIndividualChanges", "Save individual changes");
        public static string RestoreWinchesToBaseline => StringResources.Get("Winch.RestoreWinchesToBaseline", "Restore winches to baseline");
        public static string RefreshList => StringResources.Get("Winch.RefreshList", "Refresh list");
        public static string FilterPlaceholder => StringResources.Get("Winch.FilterPlaceholder", "Filter category, name…");
        public static string CategoryColumn => StringResources.Get("Winch.CategoryColumn", "Category");
        public static string NameColumn => StringResources.Get("Winch.NameColumn", "Name");
        public static string PriceColumn => StringResources.Get("Winch.PriceColumn", "Price");
        public static string LengthColumn => StringResources.Get("Winch.LengthColumn", "Length (m)");
        public static string StrengthColumn => StringResources.Get("Winch.StrengthColumn", "Strength");
        public static string AutonomousColumn => StringResources.Get("Winch.AutonomousColumn", "Autonomous");
        public static string NoData => StringResources.Get("Winch.NoData", "No winch data loaded.");
        public static string LoadPakFirst => StringResources.Get("Winch.LoadPakFirst", "Load an initial.pak file first.");
        public static string SaveSuccessTitle => StringResources.Get("Winch.SaveSuccessTitle", "Saved successfully");
        public static string SaveErrorTitle => StringResources.Get("Winch.SaveErrorTitle", "Save error");
        public static string LoadErrorTitle => StringResources.Get("Winch.LoadErrorTitle", "Load error");
        public static string RestoreWinchesSuccessTitle => StringResources.Get("Winch.RestoreWinchesSuccessTitle", "Winches restored");

        public static string LoadedCount(int count) => $"{count} winches loaded from pak.";

        public static string LoadedStatus(int count) => $"{count} winches loaded.";

        public static string LoadErrorStatus(string message) => $"Winch load error: {message}";

        public static string MultipliersAppliedStatus(int changedWinches, int updatedFiles) =>
            $"Multipliers applied. Updated winches: {changedWinches}, files: {updatedFiles}.";

        public static string MultipliersSavedMessage(int changedWinches, int updatedFiles) =>
            $"Winch settings saved.{Environment.NewLine}{Environment.NewLine}" +
            $"Updated winches: {changedWinches}{Environment.NewLine}" +
            $"Updated files: {updatedFiles}";

        public static string IndividualSavedStatus(int changedWinches, int updatedFiles) =>
            $"Individual changes saved. Winches: {changedWinches}, files: {updatedFiles}.";

        public static string IndividualSavedMessage(int changedWinches) =>
            changedWinches <= 0
                ? "No winch changes were detected to save."
                : $"Individual winch changes saved.{Environment.NewLine}{Environment.NewLine}" +
                  $"Updated winches: {changedWinches}";

        public static string RestoreWinchesMessage(int changedWinches, int updatedFiles) =>
            $"Winch values were restored from the baseline.{Environment.NewLine}{Environment.NewLine}" +
            $"Updated winches: {changedWinches}{Environment.NewLine}" +
            $"Updated files: {updatedFiles}";
    }

    public static class CrashReport
    {
        public static string Title => StringResources.Get("CrashReport.Title", "Unexpected error");
        public static string Heading => StringResources.Get("CrashReport.Heading", "Something went wrong");
        public static string CopyReport => StringResources.Get("CrashReport.CopyReport", "Copy report");
        public static string OpenGitHubIssue => StringResources.Get("CrashReport.OpenGitHubIssue", "Open GitHub issue");
        public static string EmailReport => StringResources.Get("CrashReport.EmailReport", "Email report");
        public static string Continue => StringResources.Get("CrashReport.Continue", "Continue");
        public static string CloseApp => StringResources.Get("CrashReport.CloseApp", "Close app");
        public static string Copied => StringResources.Get("CrashReport.Copied", "Crash report copied to the clipboard.");
        public static string PreparingGitHub => StringResources.Get("CrashReport.PreparingGitHub", "Checking GitHub…");

        public static string Summary(string exceptionType, string message)
        {
            var shortType = exceptionType;
            var dot = shortType.LastIndexOf('.');
            if (dot >= 0 && dot + 1 < shortType.Length)
            {
                shortType = shortType[(dot + 1)..];
            }

            return string.IsNullOrWhiteSpace(message)
                ? shortType
                : $"{shortType}: {message}";
        }

        public static string LogSaved(string path) =>
            $"Saved locally:{Environment.NewLine}{path}";

        public static string ViewExistingIssue(int number) =>
            $"View existing issue #{number}";
    }
}
