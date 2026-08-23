namespace SnowRunnerTuningShop.Localization;

using SnowRunnerTuningShop.Core;

public static class UiText
{
    public static class Nav
    {
        public const string Home = "Home";
        public const string General = "General";
        public const string Parts = "Parts";
        public const string Vehicles = "Vehicles";
        public const string Settings = "Settings";
        public const string OpenMenu = "Menu";
        public const string PinMenu = "Keep menu open";
        public static string VersionLabel => $"Version {AppInfo.Version}";
    }

    public static class Main
    {
        public const string Subtitle = "Fine-tune SnowRunner initial.pak";
        public const string OverviewTitle = "Overview";
        public const string OverviewPlaceholder =
            "Set a baseline from your original initial.pak to load the workspace.";
        public const string CategoriesTitle = "Tuning categories";
        public const string CategoryColumn = "Category";
        public const string ItemsColumn = "Items";
        public const string FilesColumn = "Files";
        public const string SampleFileColumn = "Sample file";
        public const string BrowseDialogFilter = "SnowRunner pak (*.pak)|*.pak|All files (*.*)|*.*";
        public const string LoadingPakStatus = "Loading pak...";
        public const string LoadErrorTitle = "Load error";

        public const string BaselineTitle = "Baseline required";
        public const string BaselineWarning =
            "Choose your unmodified original initial.pak (Steam, GOG, Epic, Xbox, etc.). " +
            "The app saves a read-only baseline for that edition and remembers this file as the one you will edit.";
        public const string BaselineReadyTitle = "Baseline ready";
        public const string BaselineReadyNote =
            "Baseline is healthy and ready. Keep the baseline file read-only.";
        public const string BaselineMissingShort =
            "Baseline is not set. On Home, use Set baseline from original.";
        public const string SetBaselineFromOriginal = "Set baseline from original...";
        public const string ChangeLocation = "Change location...";
        public const string RestoreFullBaseline = "Restore full baseline";
        public const string SelectOriginalPakDialogTitle = "Select unmodified original initial.pak";
        public const string ChangeLocationDialogTitle = "Select initial.pak for another store/location";
        public const string BaselineUpdatedTitle = "Baseline set";
        public const string LocationChangedTitle = "Location changed";
        public const string BaselineErrorTitle = "Baseline error";
        public const string RestoreFullBaselineConfirmTitle = "Restore full baseline?";
        public const string RestoreFullBaselineConfirmMessage =
            "This replaces the entire working initial.pak with the read-only baseline copy. " +
            "All tuning changes in the pak will be lost. This cannot be undone from inside the tuner.";
        public const string RestoreFullBaselineSuccessTitle = "Pak restored";

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

        public const string RestoreFullBaselineMessage =
            "The entire initial.pak was restored from the baseline.";

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
        public const string Winch = "Winch";
        public const string Engine = "Engine";
        public const string Gearbox = "Gearbox";
        public const string Suspension = "Suspension";
        public const string Tires = "Tires";
        public const string ComingSoon = "Coming soon.";
        public const string LoadPakHint = "Load an initial.pak on the Home page first.";
        public const string Loading = "Loading…";
    }

    public static class General
    {
        public const string Title = "General tuning";
        public const string LoadPakHint = "Load an initial.pak on the Home page first.";
        public const string AssetsMissing = "General mod assets were not found.";
        public const string CameraTitle = "Camera collisions";
        public const string CameraHint =
            "Sets ClipCamera on map object models. Pass through stops the chase camera from clipping against buildings, bridges, and similar objects.";
        public const string CameraModeLabel = "Mode";
        public const string CameraCollisionsOff = "Pass through objects";
        public const string CameraCollisionsOn = "Game default (collisions)";
        public const string ApplyCamera = "Apply camera setting";
        public const string RestoreCameraBaseline = "Restore camera baseline";
        public const string RockTitle = "Trail rock size";
        public const string RockHint =
            "Adjusts trail pebbles (SmallRock plants: small_rock, small_forest_rock, burnt_small_rock — base game and DLC). Left = no collision; right = vanilla baseline.";
        public const string RockSizeDefault = "Rock physics: Vanilla (baseline)";
        public const string ApplyRockSize = "Apply rock size";
        public const string RestoreRockBaseline = "Restore rock baseline";
        public const string NoChangesToSave = "No general changes were detected to save.";
        public const string SaveSuccessTitle = "Saved successfully";
        public const string SaveErrorTitle = "Save error";
        public static string CameraSaved(int files) =>
            $"Camera collisions updated in {files} model file(s). Reload the game to test.";
        public static string RockSaved(int files) =>
            $"Trail rock settings updated in {files} pak file(s). Reload the game to test.";
        public static string LoadedStatus(int cameraModels, double rockScale) =>
            $"Detected {cameraModels} camera-eligible models; reference rock scale {rockScale:0%}.";
    }

    public static class Vehicles
    {
        public const string All = "All";
        public const string Highway = "Highway";
        public const string HeavyDuty = "Heavy Duty";
        public const string Heavy = "Heavy";
        public const string Offroad = "Offroad";
        public const string Scout = "Scout";
        public const string BackToList = "← Back to list";
        public const string ManufacturerLabel = "Manufacturer";
        public const string BasedOnLabel = "Based on";
        public const string RoleLabel = "Role";
        public const string YearsLabel = "Years";
        public const string CountryLabel = "Country";
        public const string CountryHint = "Brand origin of the real-world basis.";
        public const string CatalogMissing = "Vehicle catalog assets were not found.";
        public const string SearchPlaceholder = "Search by name…";
        public const string GlobalMultipliersTitle = "Global multipliers (relative to the baseline values)";
        public const string FuelMultiplierDefault = "Fuel tank: 1 (baseline)";
        public const string FrontSteerGlobalDefault = "Front steer: Default (baseline)";
        public const string FrontSteerGlobalMin = "Front steer: Min (10°)";
        public const string FrontSteerGlobalMax = "Front steer: Max (60°)";
        public const string ResponsivenessMultiplierDefault = "Responsiveness: 1 (baseline)";
        public const string ApplyGlobalMultipliers = "Apply to all vehicles";
        public const string GlobalMultipliersHint =
            "Fuel tank and responsiveness scale from baseline. Front steer uses three presets: Min (10°), Default (baseline per truck), Max (60°). Independent of the category filter below.";
        public const string LoadPakForGlobalHint = "Load an initial.pak on the Home page to enable global vehicle multipliers.";
        public static string GlobalMultipliersAppliedStatus(int changedTrucks, int updatedFiles) =>
            $"Applied global vehicle multipliers to {changedTrucks} truck(s) across {updatedFiles} file(s).";
        public static string GlobalMultipliersSavedMessage(int changedTrucks, int updatedFiles) =>
            $"Global vehicle multipliers applied to {changedTrucks} truck(s) ({updatedFiles} file(s) updated).";
        public const string TuningTitle = "Vehicle tuning";
        public const string FuelTankLabel = "Fuel tank";
        public const string FuelUnit = "L";
        public const string FrontSteerLabel = "Front steer";
        public const string RearSteerLabel = "Rear steer";
        public const string SteerAngleUnit = "°";
        public const string ResponsivenessLabel = "Responsiveness";
        public const string FrontSteerHint =
            "Maximum turn for front steering wheels. Applies to all front steer axles. Range: 0° to 90°.";
        public const string RearSteerHint =
            "Rear-axle counter-steer (turns opposite to the front). Applies to all rear steer axles. Range: −90° to 0°.";
        public const string ResponsivenessHint =
            "How quickly the steering wheel returns to center (TruckData Responsiveness). Range: 0–1; higher = snappier.";
        public const string DiffLockLabel = "Diff lock";
        public const string DriveLabel = "Drive";
        public const string DiffLockAlwaysOn = "Always on";
        public const string DiffLockSwitchable = "Switchable";
        public const string DiffLockUpgradeable = "Upgradeable";
        public const string DiffLockNone = "None";
        public const string DriveRwd = "RWD";
        public const string DriveAlwaysAwd = "Always AWD";
        public const string DriveSelectableAwd = "Selectable AWD";
        public const string DiffLockHintNative =
            "Switchable and Upgradeable use the truck's built-in diff-lock upgrade slot.";
        public const string DiffLockHintSimple =
            "This truck has no diff-lock upgrade in the game. Only None or Always on can be set.";
        public const string DriveHint =
            "RWD matches the garage \"AWD: No\". Selectable AWD enables the in-cab switch (Torque full). " +
            "Upgradeable AWD in-game also needs a transfer-case addon socket; connectable alone is not enough.";
        public const string SaveChanges = "Save changes";
        public const string RestoreThisVehicle = "Restore this vehicle to baseline";
        public const string LoadPakHint = "Load an initial.pak on the Home page first.";
        public const string TruckNotFound =
            "This vehicle could not be matched to a truck XML in the loaded pak.";
        public const string InvalidFuel =
            "Fuel tank must be a whole number of liters (1–10000).";
        public const string InvalidResponsiveness =
            "Responsiveness must be between 0 and 1.";
        public const string InvalidFrontSteer =
            "Front steer must be between 0 and 90 degrees.";
        public const string InvalidRearSteer =
            "Rear steer must be between -90 and 0 degrees.";
        public const string SaveSuccessTitle = "Saved successfully";
        public const string SaveErrorTitle = "Save error";
        public const string RestoreSuccessTitle = "Vehicle restored";
        public const string NoChangesToSave = "No vehicle changes were detected to save.";
        public static string CountLabel(int count) => $"{count} vehicles";
        public static string SavedMessage() => "Vehicle tuning saved.";
        public static string RestoredMessage() => "This vehicle was restored from the baseline.";
    }

    public static class Settings
    {
        public const string Title = "Settings";
        public const string AppearanceTitle = "Appearance";
        public const string AppearanceHint = "Choose the app color theme. System follows Windows light/dark mode.";
        public const string ThemeLabel = "Theme";
        public const string ThemeSystem = "System";
        public const string ThemeDark = "Dark";
        public const string ThemeLight = "Light";
        public const string WorkspaceTitle = "Workspace";
        public const string WorkspaceHint =
            "Replace the entire working initial.pak with the read-only baseline copy for this edition.";
        public const string AboutTitle = "About & support";
        public const string AboutHint =
            "Project website, releases, and optional support via PayPal.";
        public const string OpenWebsite = "Open website";
        public const string DonatePayPal = "Donate with PayPal";
        public const string FeedbackTitle = "Feedback";
        public const string FeedbackHint =
            "Found a bug or have an idea? Open an issue on the GitHub tracker.";
        public const string OpenIssueTracker = "Open issue tracker";
    }

    public static class Engine
    {
        public const string GlobalMultipliersTitle = "Global multipliers (relative to the baseline values)";
        public const string TorqueMultiplierDefault = "Torque: 1 (baseline)";
        public const string FuelMultiplierDefault = "Fuel consumption: 1 (baseline)";
        public const string DamageMultiplierDefault = "Damage capacity: 1 (baseline)";
        public const string ResponsivenessMultiplierDefault = "Responsiveness: 1 (baseline)";
        public const string Apply = "Apply";
        public const string SaveIndividualChanges = "Save individual changes";
        public const string RestoreEnginesToBaseline = "Restore engines to baseline";
        public const string RefreshList = "Refresh list";
        public const string FilterPlaceholder = "Filter category, name, set, used by…";
        public const string CategoryColumn = "Category";
        public const string NameColumn = "Name";
        public const string UsedByColumn = "Used by";
        public const string PriceColumn = "Price";
        public const string TorqueColumn = "Torque";
        public const string FuelColumn = "Fuel";
        public const string DamageColumn = "Damage";
        public const string ResponsivenessColumn = "Responsiveness";
        public const string NoData = "No engine data loaded.";
        public const string LoadPakFirst = "Load an initial.pak file first.";
        public const string SaveSuccessTitle = "Saved successfully";
        public const string SaveErrorTitle = "Save error";
        public const string LoadErrorTitle = "Load error";
        public const string RestoreEnginesSuccessTitle = "Engines restored";

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
        public const string GlobalMultipliersTitle = "Global multipliers (relative to the baseline values)";
        public const string FuelMultiplierDefault = "Fuel consumption: 1 (baseline)";
        public const string IdleMultiplierDefault = "Idle fuel modifier: 1 (baseline)";
        public const string AwdMultiplierDefault = "AWD fuel penalty: 1 (baseline)";
        public const string Apply = "Apply";
        public const string SaveIndividualChanges = "Save individual changes";
        public const string RestoreGearboxesToBaseline = "Restore gearboxes to baseline";
        public const string RefreshList = "Refresh list";
        public const string FilterPlaceholder = "Filter category, name, set, used by…";
        public const string CategoryColumn = "Category";
        public const string NameColumn = "Name";
        public const string UsedByColumn = "Used by";
        public const string PriceColumn = "Price";
        public const string FuelColumn = "Fuel";
        public const string IdleColumn = "Idle";
        public const string AwdColumn = "AWD";
        public const string NoData = "No gearbox data loaded.";
        public const string LoadPakFirst = "Load an initial.pak file first.";
        public const string SaveSuccessTitle = "Saved successfully";
        public const string SaveErrorTitle = "Save error";
        public const string LoadErrorTitle = "Load error";
        public const string RestoreGearboxesSuccessTitle = "Gearboxes restored";

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
        public const string GlobalMultipliersTitle = "Global multipliers (relative to the baseline values)";
        public const string HeightMultiplierDefault = "Height: 1 (baseline)";
        public const string StrengthMultiplierDefault = "Strength: 1 (baseline)";
        public const string DampingMultiplierDefault = "Damping: 1 (baseline)";
        public const string DamageMultiplierDefault = "Damage capacity: 1 (baseline)";
        public const string Apply = "Apply";
        public const string SaveIndividualChanges = "Save individual changes";
        public const string RestoreSuspensionsToBaseline = "Restore suspensions to baseline";
        public const string RefreshList = "Refresh list";
        public const string FilterPlaceholder = "Filter category, name, set, used by…";
        public const string CategoryColumn = "Category";
        public const string NameColumn = "Name";
        public const string UsedByColumn = "Used by";
        public const string PriceColumn = "Price";
        public const string DamageColumn = "Damage";
        public const string FrontHeightColumn = "F Height";
        public const string FrontStrengthColumn = "F Strength";
        public const string FrontDampingColumn = "F Damping";
        public const string RearHeightColumn = "R Height";
        public const string RearStrengthColumn = "R Strength";
        public const string RearDampingColumn = "R Damping";
        /// <summary>Shown when an optional numeric attribute is absent from XML.</summary>
        public const string MissingValuePlaceholder = "n/a";
        public const string LoadPakFirst = "Load an initial.pak file first.";
        public const string SaveSuccessTitle = "Saved successfully";
        public const string SaveErrorTitle = "Save error";
        public const string LoadErrorTitle = "Load error";
        public const string RestoreSuspensionsSuccessTitle = "Suspensions restored";

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
        public const string GlobalMultipliersTitle = "Global multipliers (relative to the baseline values)";
        public const string OnRoadFrictionMultiplierDefault = "On-road: 1 (baseline)";
        public const string OffRoadFrictionMultiplierDefault = "Off-road: 1 (baseline)";
        public const string MudFrictionMultiplierDefault = "Mud: 1 (baseline)";
        public const string IgnoreIceAll = "Ignore ice on all tires";
        public const string Apply = "Apply";
        public const string SaveIndividualChanges = "Save individual changes";
        public const string RestoreTiresToBaseline = "Restore tires to baseline";
        public const string RefreshList = "Refresh list";
        public const string FilterPlaceholder = "Filter category, name, set, used by…";
        public const string CategoryColumn = "Category";
        public const string NameColumn = "Name";
        public const string UsedByColumn = "Used by";
        public const string PriceColumn = "Price";
        public const string OnRoadFrictionColumn = "On-road";
        public const string OffRoadFrictionColumn = "Off-road";
        public const string MudFrictionColumn = "Mud";
        public const string IgnoreIceColumn = "Ignore ice";
        public const string LoadPakFirst = "Load an initial.pak file first.";
        public const string SaveSuccessTitle = "Saved successfully";
        public const string SaveErrorTitle = "Save error";
        public const string LoadErrorTitle = "Load error";
        public const string RestoreTiresSuccessTitle = "Tires restored";

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
        public const string GlobalMultipliersTitle = "Global multipliers (relative to the baseline values)";
        public const string LengthMultiplierDefault = "Length multiplier: 1 (baseline)";
        public const string StrengthMultiplierDefault = "Strength multiplier: 1 (baseline)";
        public const string AutonomousAll = "Autonomous all";
        public const string Apply = "Apply";
        public const string SaveIndividualChanges = "Save individual changes";
        public const string RestoreWinchesToBaseline = "Restore winches to baseline";
        public const string RefreshList = "Refresh list";
        public const string FilterPlaceholder = "Filter category, name…";
        public const string CategoryColumn = "Category";
        public const string NameColumn = "Name";
        public const string PriceColumn = "Price";
        public const string LengthColumn = "Length (m)";
        public const string StrengthColumn = "Strength";
        public const string AutonomousColumn = "Autonomous";
        public const string NoData = "No winch data loaded.";
        public const string LoadPakFirst = "Load an initial.pak file first.";
        public const string SaveSuccessTitle = "Saved successfully";
        public const string SaveErrorTitle = "Save error";
        public const string LoadErrorTitle = "Load error";
        public const string RestoreWinchesSuccessTitle = "Winches restored";

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
}
