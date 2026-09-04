namespace SnowRunnerTuningShop.Localization;

using SnowRunnerTuningShop.Core;
using SnowRunnerTuningShop.Core.General;
using SnowRunnerTuningShop.Core.Localization;
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
        public static string Trailers => StringResources.Get("Nav.Trailers", "Trailers");
        public static string PhotoMode => StringResources.Get("Nav.PhotoMode", "Photo Mode");
        public static string Settings => StringResources.Get("Nav.Settings", "Settings");
        public static string OpenMenu => StringResources.Get("Nav.OpenMenu", "Menu");
        public static string PinMenu => StringResources.Get("Nav.PinMenu", "Keep menu open");
        public static string VersionLabel => StringResources.Format("Nav.VersionLabel", "Version {0}", AppInfo.Version);
    }

    public static class Slider
    {
        public static string Caption(string prefix, int index) =>
            StringResources.Format("Slider.Caption", "{0}: {1}", prefix, ValueLabel(index));

        public static string ValueLabel(int index)
        {
            index = TuningMultiplierPresets.ClampIndex(index);
            return index == TuningMultiplierPresets.BaselineIndex
                ? StringResources.Get("Slider.MultiplierBaseline", "1 (baseline)")
                : TuningMultiplierPresets.GetLabel(index);
        }

        public static string FuelTank => StringResources.Get("Vehicles.FuelTankLabel", "Fuel tank");
        public static string Responsiveness => StringResources.Get("Vehicles.ResponsivenessLabel", "Responsiveness");
        public static string StorePrice => StringResources.Get("Vehicles.StorePriceLabel", "Store price");
        public static string Torque => StringResources.Get("Engine.TorqueColumn", "Torque");
        public static string FuelConsumption => StringResources.Get("Slider.FuelConsumption", "Fuel consumption");
        public static string DamageCapacity => StringResources.Get("Slider.DamageCapacity", "Damage capacity");
        public static string IdleFuelModifier => StringResources.Get("Slider.IdleFuelModifier", "Idle fuel modifier");
        public static string AwdFuelPenalty => StringResources.Get("Slider.AwdFuelPenalty", "AWD fuel penalty");
        public static string Height => StringResources.Get("Slider.Height", "Height");
        public static string Strength => StringResources.Get("Slider.Strength", "Strength");
        public static string Damping => StringResources.Get("Slider.Damping", "Damping");
        public static string OnRoad => StringResources.Get("Tires.OnRoadFrictionColumn", "On-road");
        public static string OffRoad => StringResources.Get("Tires.OffRoadFrictionColumn", "Off-road");
        public static string Mud => StringResources.Get("Tires.MudFrictionColumn", "Mud");
        public static string RepairParts => StringResources.Get("Trailers.RepairPartsLabel", "Repair parts");
        public static string SpareWheels => StringResources.Get("Trailers.SpareWheelsLabel", "Spare wheels");
        public static string LengthMultiplier => StringResources.Get("Slider.LengthMultiplier", "Length multiplier");
        public static string StrengthMultiplier => StringResources.Get("Slider.StrengthMultiplier", "Strength multiplier");
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
        public static string GameRunningTitle => StringResources.Get("Main.GameRunningTitle", "SnowRunner is running");
        public static string GameRunningMessage => StringResources.Get(
            "Main.GameRunningMessage",
            "Close SnowRunner before applying, saving, or restoring changes to initial.pak.");
        public static string GameRunningBanner => StringResources.Get(
            "Main.GameRunningBanner",
            "SnowRunner is running — Apply, Save, and Restore are disabled until you close the game.");
        public static string BaselineWarning => StringResources.Get("Main.BaselineWarning", "Choose your unmodified original initial.pak (Steam, GOG, Epic, Xbox, etc.). The app saves a read-only baseline for that edition and remembers this file as the one you will edit.");
        public static string BaselineReadyTitle => StringResources.Get("Main.BaselineReadyTitle", "Baseline ready");
        public static string BaselineReadyNote => StringResources.Get("Main.BaselineReadyNote", "Baseline is healthy and ready. Keep the baseline file read-only.");
        public static string BaselineMissingShort => StringResources.Get("Main.BaselineMissingShort", "Baseline is not set. On Home, use Set baseline from original.");
        public static string ConfigCorruptTitle => StringResources.Get("Main.ConfigCorruptTitle", "Settings reset");
        public static string ConfigCorruptMessage => StringResources.Get(
            "Main.ConfigCorruptMessage",
            "Your Tuning Shop settings file was damaged and has been reset. A backup was saved as config.json.corrupt.bak in the app data folder.");
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
            StringResources.Format("Main.LoadSuccessStatus", "Loaded successfully: {0:N0} entries.", entryCount);

        public static string ErrorStatus(string message) =>
            StringResources.Format("Main.ErrorStatus", "Error: {0}", message);

        public static string AutoLoadedStatus(string editionDisplayName) =>
            StringResources.Format("Main.AutoLoadedStatus", "Loaded saved {0} workspace.", editionDisplayName);

        public static string BaselineSetStatus(string editionDisplayName) =>
            StringResources.Format("Main.BaselineSetStatus", "Baseline set for {0}.", editionDisplayName);

        public static string LocationChangedStatus(string editionDisplayName) =>
            StringResources.Format("Main.LocationChangedStatus", "Switched to {0}.", editionDisplayName);

        public static string BaselineCreatedMessage(
            string editionDisplayName,
            string workingPakPath,
            string baselinePath) =>
            StringResources.Format(
                "Main.BaselineCreatedMessage",
                "Baseline created for {0}.\n\nWorking pak:\n{1}\n\nRead-only baseline:\n{2}",
                editionDisplayName,
                workingPakPath,
                baselinePath);

        public static string LocationChangedMessage(
            string editionDisplayName,
            string workingPakPath,
            string baselinePath,
            bool baselineCreated) =>
            StringResources.Format(
                baselineCreated ? "Main.LocationChangedMessageCreated" : "Main.LocationChangedMessageExisting",
                baselineCreated
                    ? "Now editing the {0} edition.\n\nWorking pak:\n{1}\n\nNo previous baseline existed for this edition, so a new read-only baseline was created:\n{2}"
                    : "Now editing the {0} edition.\n\nWorking pak:\n{1}\n\nUsing the existing read-only baseline for this edition:\n{2}",
                editionDisplayName,
                workingPakPath,
                baselinePath);

        public static string BaselineReadyStatus(string editionDisplayName, string fileName, DateTime lastWriteUtc) =>
            StringResources.Format(
                "Main.BaselineReadyStatus",
                "Baseline OK for {0} ({1}, {2:yyyy-MM-dd HH:mm}).",
                editionDisplayName,
                fileName,
                lastWriteUtc.ToLocalTime());

        public static string WorkingPakStatus(string editionDisplayName, string workingPakPath) =>
            StringResources.Format("Main.WorkingPakStatus", "Editing ({0}): {1}", editionDisplayName, workingPakPath);

        public static string RestoreFullBaselineMessage => StringResources.Get("Main.RestoreFullBaselineMessage", "The entire initial.pak was restored from the baseline. You can reapply saved changes from Home or Settings.");

        public static string OverviewDetails(
            string filePath,
            string fileSize,
            int totalEntries,
            int xmlEntries,
            int dlcPackages,
            string uncompressedSize,
            IEnumerable<string> topLevelFolders)
        {
            var folders = string.Join(
                Environment.NewLine,
                topLevelFolders.Select(folder => "• " + folder));
            return StringResources.Format(
                "Main.OverviewDetails",
                "File: {0}\nSize: {1}\nEntries: {2:N0}\nXML files: {3:N0}\nDLC packages: {4}\nUnpacked: {5}\n\nTop level:\n{6}",
                filePath,
                fileSize,
                totalEntries,
                xmlEntries,
                dlcPackages,
                uncompressedSize,
                folders);
        }
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
        public static string NoTrucksEngineSet => StringResources.Get("Parts.NoTrucksEngineSet", "No trucks reference this engine set.");
        public static string NoTrucksGearboxSet => StringResources.Get("Parts.NoTrucksGearboxSet", "No trucks reference this gearbox set.");
        public static string NoTrucksSuspensionSet => StringResources.Get("Parts.NoTrucksSuspensionSet", "No trucks reference this suspension set.");
        public static string NoTrucksWheelSet => StringResources.Get("Parts.NoTrucksWheelSet", "No trucks reference this wheel set.");
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
            StringResources.Format("General.CameraSaved", "Camera collisions updated in {0} model file(s). Reload the game to test.", files);
        public static string RockSaved(int files) =>
            StringResources.Format("General.RockSaved", "Trail rock settings updated in {0} pak file(s). Reload the game to test.", files);
        public static string LoadedStatus(int cameraModels, double rockScale) =>
            StringResources.Format(
                "General.LoadedStatus",
                "Detected {0} camera-eligible models; reference rock scale {1:0%}.",
                cameraModels,
                rockScale);

        public static string RockPhysicsCaption(int index)
        {
            var clamped = RockSizePresets.ClampIndex(index);
            var value = clamped switch
            {
                0 => StringResources.Get("General.RockNoCollision", "No collision"),
                4 => StringResources.Get("General.RockVanillaBaseline", "Vanilla (baseline)"),
                _ => RockSizePresets.GetLabel(clamped),
            };
            return StringResources.Format("General.RockPhysicsCaption", "Rock physics: {0}", value);
        }
    }

    public static class PhotoMode
    {
        public static string Title => StringResources.Get("PhotoMode.Title", "Photo Mode defaults");
        public static string ExperimentalWarning => StringResources.Get(
            "PhotoMode.ExperimentalWarning",
            "Experimental feature. Not included in Reapply saved changes on Home — use Reapply saved photo mode here instead. Restore baseline if you see errors.");
        public static string ReapplySaved =>
            StringResources.Get("PhotoMode.ReapplySaved", "Reapply saved photo mode");
        public static string ReappliedSaved(int entries) =>
            StringResources.Format(
                "PhotoMode.ReappliedSaved",
                "Saved photo mode settings reapplied ({0} pak file(s) updated). Reload the game to test.",
                entries);
        public static string ReappliedSavedNoChanges =>
            StringResources.Get(
                "PhotoMode.ReappliedSavedNoChanges",
                "Saved photo mode settings are already applied to the working pak.");
        public static string Subtitle => StringResources.Get("PhotoMode.Subtitle", "Change the values Photo Mode uses when you open it or press Restore default in-game. Test in the game after saving.");
        public static string EnvironmentTitle => StringResources.Get("PhotoMode.EnvironmentTitle", "Environment");
        public static string LookTitle => StringResources.Get("PhotoMode.LookTitle", "Look");
        public static string CameraTitle => StringResources.Get("PhotoMode.CameraTitle", "Camera & focus");
        public static string TimeLabel => StringResources.Get("PhotoMode.TimeLabel", "Time");
        public static string TimeNote => StringResources.Get(
            "PhotoMode.TimeNote",
            "Cannot be safely changed from the default value stored in the pak.");
        public static string WeatherLabel => StringResources.Get("PhotoMode.WeatherLabel", "Default weather");
        public static string Exposure => StringResources.Get("PhotoMode.Exposure", "Exposure");
        public static string ExposureNote => StringResources.Get(
            "PhotoMode.ExposureNote",
            "Cannot be safely changed from the default value stored in the pak.");
        public static string Contrast => StringResources.Get("PhotoMode.Contrast", "Contrast");
        public static string ContrastNote => StringResources.Get(
            "PhotoMode.ContrastNote",
            "Cannot be safely changed from the default value stored in the pak.");
        public static string Hue => StringResources.Get("PhotoMode.Hue", "Hue");
        public static string Saturation => StringResources.Get("PhotoMode.Saturation", "Saturation");
        public static string ColorGrading => StringResources.Get("PhotoMode.ColorGrading", "Color grading");
        public static string ColorGradingIntensity => StringResources.Get("PhotoMode.ColorGradingIntensity", "Color grading intensity");
        public static string Vignette => StringResources.Get("PhotoMode.Vignette", "Vignette");
        public static string FilmGrain => StringResources.Get("PhotoMode.FilmGrain", "Film grain");
        public static string FieldOfView => StringResources.Get("PhotoMode.FieldOfView", "Field of view");
        public static string FieldOfViewNote => StringResources.Get(
            "PhotoMode.FieldOfViewNote",
            "Not editable here. Photo Mode uses your gameplay camera FOV from Settings → Gameplay.");
        public static string GameDefaultsNote => StringResources.Get(
            "PhotoMode.GameDefaultsNote",
            "After saving, press Restore default (R) in Photo Mode to load these pak defaults. Sliders may show your last in-game session until then.");
        public static string Aperture => StringResources.Get("PhotoMode.Aperture", "Aperture");
        public static string FocusPoint => StringResources.Get("PhotoMode.FocusPoint", "Focus point");
        public static string FocusSpan => StringResources.Get("PhotoMode.FocusSpan", "Depth of field span");
        public static string WeatherDefault => StringResources.Get("PhotoMode.WeatherDefault", "Default");
        public static string WeatherClearSky => StringResources.Get("PhotoMode.WeatherClearSky", "Clear sky");
        public static string WeatherLightRain => StringResources.Get("PhotoMode.WeatherLightRain", "Light rain");
        public static string WeatherHeavyRain => StringResources.Get("PhotoMode.WeatherHeavyRain", "Heavy rain");
        public static string WeatherHeavySnow => StringResources.Get("PhotoMode.WeatherHeavySnow", "Heavy snow");
        public static string Apply => StringResources.Get("PhotoMode.Apply", "Apply photo mode defaults");
        public static string RestoreBaseline => StringResources.Get("PhotoMode.RestoreBaseline", "Restore photo mode baseline");
        public static string LoadPakHint => StringResources.Get("PhotoMode.LoadPakHint", "Load an initial.pak on the Home page first.");
        public static string LoadedStatus => StringResources.Get("PhotoMode.LoadedStatus", "Loaded current photo mode defaults from the working pak.");
        public static string SliderRangeLimited =>
            StringResources.Get(
                "PhotoMode.SliderRangeLimited",
                "Some sliders are limited to values that fit in the pak file. Vignette and film grain usually allow the most freedom.");
        public static string SliderFixedPakField(string label) =>
            StringResources.Format(
                "PhotoMode.SliderFixedPakField",
                "{0} cannot be changed here — the pak field is too narrow to store other values.",
                label);
        public static string SliderLimitedPakField(string label, int count, int fieldWidth) =>
            StringResources.Format(
                "PhotoMode.SliderLimitedPakField",
                "{0}: only {1} saveable values (pak field is {2} characters wide).",
                label,
                count,
                fieldWidth);
        public static string NoChangesToSave => StringResources.Get("PhotoMode.NoChangesToSave", "No photo mode changes were detected to save.");
        public static string SaveSuccessTitle => StringResources.Get("PhotoMode.SaveSuccessTitle", "Saved successfully");
        public static string SaveErrorTitle => StringResources.Get("PhotoMode.SaveErrorTitle", "Save error");
        public static string Saved(int entries) =>
            StringResources.Format("PhotoMode.Saved", "Photo mode defaults updated in {0} pak file(s). Reload the game to test.", entries);
    }

    public static class Vehicles
    {
        public static string All => StringResources.Get("Vehicles.All", "All");
        public static string Highway => StringResources.Get("Vehicles.Highway", "Highway");
        public static string HeavyDuty => StringResources.Get("Vehicles.HeavyDuty", "Heavy Duty");
        public static string Heavy => StringResources.Get("Vehicles.Heavy", "Heavy");
        public static string Offroad => StringResources.Get("Vehicles.Offroad", "Offroad");
        public static string Scout => StringResources.Get("Vehicles.Scout", "Scout");

        public static string CategoryDisplay(string? category) =>
            category?.Trim() switch
            {
                "Highway" => Highway,
                "Heavy Duty" => HeavyDuty,
                "Heavy" => Heavy,
                "Offroad" => Offroad,
                "Scout" => Scout,
                _ => category ?? "",
            };

        public static string CountryDisplay(string? countryCode, string? fallback = null)
        {
            if (string.IsNullOrWhiteSpace(countryCode))
            {
                return fallback ?? "";
            }

            var code = countryCode.Trim().ToUpperInvariant();
            var key = "Vehicles.Country." + code;
            return StringResources.Get(key, fallback ?? countryCode);
        }

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
        public static string GlobalMultipliersHint => StringResources.Get("Vehicles.GlobalMultipliersHint", "Fuel tank, store price, and responsiveness scale from baseline. Front steer uses three presets: Min (10°), Default (baseline per truck), Max (60°). Always-on diff lock and AWD, when checked, are forced on every truck. Independent of the category filter below.");
        public static string AlwaysOnDiffLock => StringResources.Get("Vehicles.AlwaysOnDiffLock", "Always on diff lock");
        public static string AlwaysOnAwd => StringResources.Get("Vehicles.AlwaysOnAwd", "Always on AWD");
        public static string StoreUnlocksTitle => StringResources.Get("Vehicles.StoreUnlocksTitle", "Store unlocks (all vehicles)");
        public static string StoreUnlocksHint => StringResources.Get("Vehicles.StoreUnlocksHint", "Apply region and rank unlocks across every truck XML. Region-free makes trucks appear in every regional truck store. Unlock all sets UnlockByRank to 0.");
        public static string ReleaseRegionLock => StringResources.Get("Vehicles.ReleaseRegionLock", "Release region lock (all stores)");
        public static string UnlockAllVehicles => StringResources.Get("Vehicles.UnlockAllVehicles", "Unlock all vehicles (rank 0)");
        public static string ApplyStoreUnlocks => StringResources.Get("Vehicles.ApplyStoreUnlocks", "Apply store unlocks");
        public static string LoadPakForGlobalHint => StringResources.Get("Vehicles.LoadPakForGlobalHint", "Load an initial.pak on the Home page to enable global vehicle multipliers.");
        public static string GlobalMultipliersAppliedStatus(int changedTrucks, int updatedFiles) =>
            StringResources.Format(
                "Vehicles.GlobalMultipliersAppliedStatus",
                "Applied global vehicle multipliers to {0} truck(s) across {1} file(s).",
                changedTrucks,
                updatedFiles);
        public static string GlobalMultipliersSavedMessage(int changedTrucks, int updatedFiles) =>
            StringResources.Format(
                "Vehicles.GlobalMultipliersSavedMessage",
                "Global vehicle multipliers applied to {0} truck(s) ({1} file(s) updated).",
                changedTrucks,
                updatedFiles);
        public static string StoreUnlocksSavedMessage(int changedTrucks, int updatedFiles) =>
            StringResources.Format(
                "Vehicles.StoreUnlocksSavedMessage",
                "Store unlocks applied to {0} truck(s) ({1} file(s) updated).",
                changedTrucks,
                updatedFiles);
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
            StringResources.Format(
                "Vehicles.RestoreAllVehiclesSavedMessage",
                "Restored {0} vehicle(s) from baseline ({1} file(s) updated).",
                changedTrucks,
                updatedFiles);
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
        public static string CountLabel(int count) =>
            StringResources.Format("Vehicles.CountLabel", "{0} vehicles", count);
        public static string SavedMessage() =>
            StringResources.Get("Vehicles.SavedMessage", "Vehicle tuning saved.");
        public static string RestoredMessage() =>
            StringResources.Get("Vehicles.RestoredMessage", "This vehicle was restored from the baseline.");
    }

    public static class Trailers
    {
        public static string All => StringResources.Get("Trailers.All", "All");
        public static string HitchScout => StringResources.Get("Trailers.HitchScout", "Scout");
        public static string HitchStandard => StringResources.Get("Trailers.HitchStandard", "Standard");
        public static string HitchSaddleLow => StringResources.Get("Trailers.HitchSaddleLow", "Saddle Low");
        public static string HitchSaddleHigh => StringResources.Get("Trailers.HitchSaddleHigh", "Saddle High");
        public static string HitchOther => StringResources.Get("Trailers.HitchOther", "Special");
        public static string Mission => StringResources.Get("Trailers.Mission", "Mission");
        public static string BackToList => StringResources.Get("Trailers.BackToList", "← Back to list");
        public static string CatalogMissing => StringResources.Get("Trailers.CatalogMissing", "Trailer catalog assets were not found.");
        public static string SearchPlaceholder => StringResources.Get("Trailers.SearchPlaceholder", "Search by name…");
        public static string HitchLabel => StringResources.Get("Trailers.HitchLabel", "Hitch");
        public static string FunctionLabel => StringResources.Get("Trailers.FunctionLabel", "Function");
        public static string MissionYes => StringResources.Get("Trailers.MissionYes", "Yes");
        public static string MissionNo => StringResources.Get("Trailers.MissionNo", "No");
        public static string LoadPakHint => StringResources.Get("Trailers.LoadPakHint", "Load an initial.pak on the Home page first.");
        public static string TrailerNotFound => StringResources.Get("Trailers.TrailerNotFound", "This trailer could not be matched to an XML file in the loaded pak.");
        public static string TuningTitle => StringResources.Get("Trailers.TuningTitle", "Trailer tuning");
        public static string FuelTankLabel => StringResources.Get("Trailers.FuelTankLabel", "Fuel tank");
        public static string FuelUnit => StringResources.Get("Trailers.FuelUnit", "L");
        public static string WaterTankLabel => StringResources.Get("Trailers.WaterTankLabel", "Water tank");
        public static string RepairPartsLabel => StringResources.Get("Trailers.RepairPartsLabel", "Repair parts");
        public static string SpareWheelsLabel => StringResources.Get("Trailers.SpareWheelsLabel", "Spare wheels");
        public static string StorePriceLabel => StringResources.Get("Trailers.StorePriceLabel", "Store price");
        public static string UnlockRankLabel => StringResources.Get("Trailers.UnlockRankLabel", "Unlock rank");
        public static string UnlockRankHint => StringResources.Get("Trailers.UnlockRankHint", "Player rank required in the trailer store (GameData UnlockByRank). Use 0 to clear the rank gate.");
        public static string SaveChanges => StringResources.Get("Trailers.SaveChanges", "Save changes");
        public static string RestoreThisTrailer => StringResources.Get("Trailers.RestoreThisTrailer", "Restore this trailer to baseline");
        public static string RestoreAllTrailers => StringResources.Get("Trailers.RestoreAllTrailers", "Restore all trailers to baseline");
        public static string RestoreAllTrailersConfirmTitle => StringResources.Get("Trailers.RestoreAllTrailersConfirmTitle", "Restore all trailers?");
        public static string RestoreAllTrailersConfirmMessage => StringResources.Get("Trailers.RestoreAllTrailersConfirmMessage", "This restores every trailer XML from your baseline pak (fuel, water, repairs, wheels, price, unlock rank, and store availability). Continue?");
        public static string RestoreAllTrailersSuccessTitle => StringResources.Get("Trailers.RestoreAllTrailersSuccessTitle", "Trailers restored");
        public static string RestoreSuccessTitle => StringResources.Get("Trailers.RestoreSuccessTitle", "Trailer restored");
        public static string SaveSuccessTitle => StringResources.Get("Trailers.SaveSuccessTitle", "Saved successfully");
        public static string SaveErrorTitle => StringResources.Get("Trailers.SaveErrorTitle", "Save error");
        public static string LoadPakForGlobalHint => StringResources.Get("Trailers.LoadPakForGlobalHint", "Load an initial.pak on the Home page to enable global trailer multipliers.");
        public static string GlobalMultipliersTitle => StringResources.Get("Trailers.GlobalMultipliersTitle", "Global multipliers (relative to the baseline values)");
        public static string GlobalMultipliersHint => StringResources.Get("Trailers.GlobalMultipliersHint", "Fuel, repair parts, spare wheels, and store price scale from baseline on trailers that already have those fields. Independent of the hitch filter below.");
        public static string ApplyGlobalMultipliers => StringResources.Get("Trailers.ApplyGlobalMultipliers", "Apply to all trailers");
        public static string StoreUnlocksTitle => StringResources.Get("Trailers.StoreUnlocksTitle", "Trailer store");
        public static string StoreUnlocksHint => StringResources.Get("Trailers.StoreUnlocksHint", "Quest trailers are hidden by GameData IsQuest (including values inherited from a parent XML). Trains and similar special hitches also need a regular trailer socket so the store can list them. Restore all trailers to undo.");
        public static string MakeMissionTrailersPurchasable => StringResources.Get("Trailers.MakeMissionTrailersPurchasable", "Make mission trailers purchasable");
        public static string AvailableInStoreLabel => StringResources.Get("Trailers.AvailableInStoreLabel", "Available in store");
        public static string AvailableInStoreHint => StringResources.Get(
            "Trailers.AvailableInStoreHint",
            "Checked lists the trailer in the store (clears IsQuest and adds a regular hitch socket for trains). Unchecked hides it. Vanilla trains look “off” here even when IsQuest is absent, because they only have a Train hitch.");
        public static string FuelMultiplierDefault => StringResources.Get("Trailers.FuelMultiplierDefault", "Fuel tank: 1 (baseline)");
        public static string RepairsMultiplierDefault => StringResources.Get("Trailers.RepairsMultiplierDefault", "Repair parts: 1 (baseline)");
        public static string WheelsMultiplierDefault => StringResources.Get("Trailers.WheelsMultiplierDefault", "Spare wheels: 1 (baseline)");
        public static string PriceMultiplierDefault => StringResources.Get("Trailers.PriceMultiplierDefault", "Store price: 1 (baseline)");
        public static string InvalidFuel => StringResources.Get("Trailers.InvalidFuel", "Fuel tank must be a whole number of liters (1–10000).");
        public static string InvalidWater => StringResources.Get("Trailers.InvalidWater", "Water tank must be a whole number of liters (1–10000).");
        public static string InvalidRepairs => StringResources.Get("Trailers.InvalidRepairs", "Repair parts must be a whole number from 0 to 10,000.");
        public static string InvalidWheels => StringResources.Get("Trailers.InvalidWheels", "Spare wheels must be a whole number from 0 to 99.");
        public static string InvalidPrice => StringResources.Get("Trailers.InvalidPrice", "Store price must be a whole number from 0 to 9,999,999.");
        public static string InvalidUnlockRank => StringResources.Get("Trailers.InvalidUnlockRank", "Unlock rank must be a whole number from 0 to 30.");
        public static string NoChangesToSave => StringResources.Get("Trailers.NoChangesToSave", "No trailer changes were detected to save.");
        public static string NoTunableFields => StringResources.Get("Trailers.NoTunableFields", "This trailer has no fuel, water, repair, wheel, or store-price fields to edit.");

        public static string GlobalMultipliersSavedMessage(int changedTrailers, int updatedFiles) =>
            StringResources.Format(
                "Trailers.GlobalMultipliersSavedMessage",
                "Global trailer multipliers applied to {0} trailer(s) ({1} file(s) updated).",
                changedTrailers,
                updatedFiles);

        public static string StoreUnlocksSavedMessage(int changedTrailers, int updatedFiles) =>
            StringResources.Format(
                "Trailers.StoreUnlocksSavedMessage",
                "Made {0} mission trailer(s) purchasable ({1} file(s) updated).",
                changedTrailers,
                updatedFiles);

        public static string RestoreAllTrailersSavedMessage(int changedTrailers, int updatedFiles) =>
            StringResources.Format(
                "Trailers.RestoreAllTrailersSavedMessage",
                "Restored {0} trailer(s) from baseline ({1} file(s) updated).",
                changedTrailers,
                updatedFiles);

        public static string SavedMessage() =>
            StringResources.Get("Trailers.SavedMessage", "Trailer tuning saved.");
        public static string RestoredMessage() =>
            StringResources.Get("Trailers.RestoredMessage", "This trailer was restored from the baseline.");

        public static string CountLabel(int count) =>
            StringResources.Format("Trailers.CountLabel", "{0} trailers", count);

        public static string HitchName(string hitch) => hitch.Trim().ToLowerInvariant() switch
        {
            "scout" => HitchScout,
            "standard" => HitchStandard,
            "saddle-low" => HitchSaddleLow,
            "saddle-high" => HitchSaddleHigh,
            "other" => HitchOther,
            _ => hitch,
        };

        public static string FunctionName(string function) => function.Trim().ToLowerInvariant() switch
        {
            "cargo" => StringResources.Get("Trailers.FunctionCargo", "Cargo"),
            "logging" => StringResources.Get("Trailers.FunctionLogging", "Logging"),
            "maintenance" => StringResources.Get("Trailers.FunctionMaintenance", "Maintenance"),
            "mission" => Mission,
            "farming" => StringResources.Get("Trailers.FunctionFarming", "Farming"),
            _ => function,
        };
    }

    public static class SafeRange
    {
        public static string InvalidNumber => StringResources.Get("SafeRange.InvalidNumber", "Enter a valid number.");

        public static string BaselineLabel(string formattedValue) =>
            StringResources.Format("SafeRange.BaselineLabel", "Baseline: {0}", formattedValue);

        public static string AllowedLabel(string minFormatted, string maxFormatted) =>
            StringResources.Format("SafeRange.AllowedLabel", "Allowed: {0}–{1}", minFormatted, maxFormatted);

        public static string ZoneMessage(SafeRangeZone zone) =>
            zone switch
            {
                SafeRangeZone.Normal => StringResources.Get("SafeRange.ZoneNormal", "Within normal range"),
                SafeRangeZone.Warning => StringResources.Get("SafeRange.ZoneWarning", "Outside typical range — check before saving"),
                SafeRangeZone.Extreme => StringResources.Get("SafeRange.ZoneExtreme", "Extreme value — may cause unexpected behavior"),
                _ => StringResources.Get("SafeRange.ZoneInvalid", "Outside allowed range"),
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
        public static string LanguageHint => StringResources.Get("Settings.LanguageHint", "Choose the app language. Restart the app to apply a change. Extra languages can be downloaded from GitHub without waiting for an app release.");
        public static string LanguageLabel => StringResources.Get("Settings.LanguageLabel", "Language");
        public static string LanguageRestartTitle => StringResources.Get("Settings.LanguageRestartTitle", "Restart required");
        public static string LanguageRestartMessage => StringResources.Get("Settings.LanguageRestartMessage", "Restart SnowRunner Tuning Shop to apply the new language.");
        public static string ManageLanguages => StringResources.Get("Settings.ManageLanguages", "Add or Update languages");
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
            StringResources.Format(
                "Settings.UpdateAvailableMessage",
                "Version {0} is available (you have {1}). Download and install it from inside the app.",
                latest,
                AppInfo.Version);
        public static string UpdateAvailableStatus(string latest) =>
            StringResources.Format("Settings.UpdateAvailableStatus", "Update available: {0}.", latest);
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

    public static class LocalePack
    {
        public static string Title => StringResources.Get("LocalePack.Title", "Add or Update languages");
        public static string Hint => StringResources.Get("LocalePack.Hint", "Languages shipped with the app stay available offline. Check Add/Update to download a newer or extra language from GitHub, or Remove to drop a downloaded copy (bundled languages revert to the shipped file).");
        public static string Checking => StringResources.Get("LocalePack.Checking", "Checking GitHub for language files…");
        public static string Refresh => StringResources.Get("LocalePack.Refresh", "Refresh");
        public static string Apply => StringResources.Get("LocalePack.Apply", "Apply selected");
        public static string Close => StringResources.Get("LocalePack.Close", "Close");
        public static string ColumnAddUpdate => StringResources.Get("LocalePack.ColumnAddUpdate", "Add / Update");
        public static string ColumnRemove => StringResources.Get("LocalePack.ColumnRemove", "Remove");
        public static string ColumnLanguage => StringResources.Get("LocalePack.ColumnLanguage", "Language");
        public static string ColumnStatus => StringResources.Get("LocalePack.ColumnStatus", "Status");
        public static string ColumnRevision => StringResources.Get("LocalePack.ColumnRevision", "Revision");
        public static string NothingSelected => StringResources.Get("LocalePack.NothingSelected", "Select at least one language to add, update, or remove.");
        public static string ApplySuccess => StringResources.Get("LocalePack.ApplySuccess", "Language files updated. You can select a new language in Settings; restart the app to apply it.");

        public static string CheckFailed(string? detail) =>
            StringResources.Format(
                "LocalePack.CheckFailed",
                "Could not check GitHub ({0}). Shipped languages are listed below.",
                string.IsNullOrWhiteSpace(detail) ? "unknown error" : detail);

        public static string Ready(IReadOnlyList<LocalePackSnapshot> packs)
        {
            var add = packs.Count(pack => pack.CanAdd);
            var update = packs.Count(pack => pack.CanUpdate);
            return StringResources.Format(
                "LocalePack.Ready",
                "GitHub catalog loaded. {0} new, {1} update(s) available.",
                add,
                update);
        }

        public static string Status(LocalePackSnapshot pack)
        {
            if (!pack.CompatibleWithApp)
            {
                return StringResources.Format(
                    "LocalePack.StatusNeedsApp",
                    "Needs app {0}+",
                    pack.MinAppVersion ?? "?");
            }

            if (pack.CanAdd)
            {
                return StringResources.Get("LocalePack.StatusAvailable", "Available to add");
            }

            if (pack.CanUpdate)
            {
                return StringResources.Get("LocalePack.StatusUpdate", "Update available");
            }

            if (pack.HasOverlay)
            {
                return StringResources.Get("LocalePack.StatusDownloaded", "Downloaded overlay");
            }

            if (pack.IsBundled)
            {
                return StringResources.Get("LocalePack.StatusBundled", "Shipped with the app");
            }

            return pack.HasLocalFile
                ? StringResources.Get("LocalePack.StatusInstalled", "Installed")
                : StringResources.Get("LocalePack.StatusMissing", "Not installed");
        }

        public static string Revision(LocalePackSnapshot pack)
        {
            if (pack.RemoteRevision is int remote)
            {
                return StringResources.Format("LocalePack.RevisionBoth", "{0} → {1}", pack.LocalRevision, remote);
            }

            return pack.LocalRevision > 0
                ? pack.LocalRevision.ToString()
                : "—";
        }
    }

    public static class UpdateDownload
    {
        public static string Title => StringResources.Get("UpdateDownload.Title", "Download update");
        public static string DownloadingTitle => StringResources.Get("UpdateDownload.DownloadingTitle", "Downloading update…");
        public static string DownloadingDetail => StringResources.Get("UpdateDownload.DownloadingDetail", "Downloading the installer. Please wait.");
        public static string DownloadingDetailVersion(string version) =>
            StringResources.Format("UpdateDownload.DownloadingDetailVersion", "Downloading version {0}. Please wait.", version);
        public static string Cancelling => StringResources.Get("UpdateDownload.Cancelling", "Cancelling download…");
        public static string CompleteTitle => StringResources.Get("UpdateDownload.CompleteTitle", "Download complete");
        public static string CompleteDetail => StringResources.Get("UpdateDownload.CompleteDetail", "The installer is ready. Choose Update and restart to close this app and run the setup.");
        public static string FailedTitle => StringResources.Get("UpdateDownload.FailedTitle", "Download failed");
        public static string UpdateAndRestart => StringResources.Get("UpdateDownload.UpdateAndRestart", "Update and restart");
        public static string Cancel => StringResources.Get("UpdateDownload.Cancel", "Cancel");
        public static string Close => StringResources.Get("UpdateDownload.Close", "Close");

        public static string ProgressLabel(double percent, string received, string total) =>
            StringResources.Format("UpdateDownload.ProgressLabel", "{0:0}% — {1} / {2}", percent, received, total);

        public static string ProgressIndeterminate(string received) =>
            StringResources.Format("UpdateDownload.ProgressIndeterminate", "{0} downloaded…", received);
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
        public static string RefreshBaselineTooltipEnabled => StringResources.Get(
            "Workspace.RefreshBaselineTooltipEnabled",
            "Save the current working pak as the new read-only baseline. Use only when it is unmodified vanilla (for example after a game update).");
        public static string RefreshBaselineTooltipHasMarker => StringResources.Get(
            "Workspace.RefreshBaselineTooltipHasMarker",
            "Unavailable: the working pak still has Tuning Shop edits. Restore the full baseline first (or wait until the game replaces the file), then refresh.");
        public static string RefreshBaselineTooltipMatchesBaseline => StringResources.Get(
            "Workspace.RefreshBaselineTooltipMatchesBaseline",
            "Unavailable: the working pak already matches the baseline.");
        public static string RefreshBaselineTooltipNotReady => StringResources.Get(
            "Workspace.RefreshBaselineTooltipNotReady",
            "Unavailable: set a baseline and load a workspace first.");

        public static string RefreshBaselineTooltip(WorkspaceHealth health)
        {
            if (health.CanRefreshBaseline)
            {
                return RefreshBaselineTooltipEnabled;
            }

            if (health.HasMarker)
            {
                return RefreshBaselineTooltipHasMarker;
            }

            if (health.WorkingMatchesBaseline)
            {
                return RefreshBaselineTooltipMatchesBaseline;
            }

            return RefreshBaselineTooltipNotReady;
        }

        public static string ReapplyTitle => StringResources.Get("Workspace.ReapplyTitle", "Reapply saved changes");
        public static string ReapplyConfirmTitle => StringResources.Get("Workspace.ReapplyConfirmTitle", "Reapply saved changes?");
        public static string ReapplyConfirmMessage => StringResources.Get("Workspace.ReapplyConfirmMessage", "This writes your saved tuning profile back into the working initial.pak. Files that no longer exist after a game update will be skipped and listed in a report. Continue?");
        public static string ReapplySuccessTitle => StringResources.Get("Workspace.ReapplySuccessTitle", "Saved changes reapplied");
        public static string ReapplyProgressTitle => StringResources.Get("Workspace.ReapplyProgressTitle", "Reapplying saved changes");
        public static string ReapplyProgressStarting => StringResources.Get("Workspace.ReapplyProgressStarting", "Starting…");
        public static string ReapplyProgressFinalizing => StringResources.Get("Workspace.ReapplyProgressFinalizing", "Finalizing…");
        public static string ReapplyProgressWritingPakStart =>
            StringResources.Get("Workspace.ReapplyProgressWritingPakStart", "Writing initial.pak…");
        public static string ReapplyProgressStagingCopy =>
            StringResources.Get("Workspace.ReapplyProgressStagingCopy", "Copying pak to a temporary file…");
        public static string ReapplyProgressStagingPrepare(string categoryKey) =>
            StringResources.Format(
                "Workspace.ReapplyProgressStagingPrepare",
                "Preparing replacements: {0}",
                ReapplyProgressCategory(categoryKey));
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
            StringResources.Format("Workspace.ProfileStatus", "Saved profile: {0} file(s).", entryCount);

        public static string StatusLine(WorkspaceHealthKind kind, int profileEntryCount) =>
            kind switch
            {
                WorkspaceHealthKind.GameUpdateDetected =>
                    StringResources.Get("Workspace.StatusGameUpdate", "Game update detected. Refresh the baseline, then reapply saved changes."),
                WorkspaceHealthKind.UnknownExternalChange =>
                    StringResources.Get("Workspace.StatusUnknownChange", "Working pak differs from the baseline (no Tuning Shop marker)."),
                WorkspaceHealthKind.ReadyToReapply =>
                    StringResources.Format("Workspace.StatusReadyToReapply", "Saved profile ready to reapply ({0} file(s)).", profileEntryCount),
                WorkspaceHealthKind.HealthyTuned =>
                    StringResources.Format("Workspace.StatusHealthyTuned", "Workspace healthy — tuned ({0} saved file(s)).", profileEntryCount),
                WorkspaceHealthKind.HealthyVanilla =>
                    StringResources.Get("Workspace.StatusHealthyVanilla", "Workspace healthy — working pak matches the baseline."),
                WorkspaceHealthKind.InconsistentMarker =>
                    StringResources.Get("Workspace.StatusInconsistentMarker", "Pak matches the baseline but still has a Tuning Shop marker."),
                _ => StringResources.Get("Workspace.StatusNotReady", "Workspace is not ready."),
            };

        public static string ReapplyReport(TuningProfileReapplyResult result)
        {
            var lines = new List<string>
            {
                StringResources.Format("Workspace.ReapplyReportApplied", "Applied {0} file(s).", result.AppliedCount),
            };

            AppendPathList(lines, StringResources.Get("Workspace.ReapplyReportSkipped", "Skipped (no longer in the pak)"), result.MissingEntryPaths);
            AppendPathList(lines, StringResources.Get("Workspace.ReapplyReportFailed", "Failed"), result.FailedEntryPaths);
            return string.Join(Environment.NewLine, lines);
        }

        public static string ReapplyProgressCounter(int current, int total) =>
            StringResources.Format("Workspace.ReapplyProgressCounter", "{0} / {1}", current, total);

        public static string ReapplyProgressProfileCounter(int current, int total) =>
            StringResources.Format("Workspace.ReapplyProgressProfileCounter", "Profile {0} / {1}", current, total);

        public static string ReapplyProgressPakCounter(int current, int total) =>
            StringResources.Format("Workspace.ReapplyProgressPakCounter", "Pak {0} / {1}", current, total);

        public static string ReapplyProgressStagingCounter(int current, int total) =>
            StringResources.Format("Workspace.ReapplyProgressStagingCounter", "Staging {0} / {1}", current, total);

        public static string ReapplyProgressElapsed(TimeSpan elapsed) =>
            StringResources.Format(
                "Workspace.ReapplyProgressElapsed",
                "{0:mm\\:ss}",
                elapsed);

        public static string ReapplyProgressPreparing(string categoryKey) =>
            StringResources.Format(
                "Workspace.ReapplyProgressPreparing",
                "Preparing saved profile: {0}",
                ReapplyProgressCategory(categoryKey));

        public static string ReapplyProgressWriting(string categoryKey) =>
            StringResources.Format(
                "Workspace.ReapplyProgressWriting",
                "Applying: {0}",
                ReapplyProgressCategory(categoryKey));

        public static string ReapplyProgressCategory(string categoryKey) =>
            categoryKey switch
            {
                TuningProfileEntryCategories.Engines => StringResources.Get("Workspace.ReapplyCategory.Engines", "engines"),
                TuningProfileEntryCategories.Gearboxes => StringResources.Get("Workspace.ReapplyCategory.Gearboxes", "gearboxes"),
                TuningProfileEntryCategories.Suspensions => StringResources.Get("Workspace.ReapplyCategory.Suspensions", "suspensions"),
                TuningProfileEntryCategories.Winches => StringResources.Get("Workspace.ReapplyCategory.Winches", "winches"),
                TuningProfileEntryCategories.Tires => StringResources.Get("Workspace.ReapplyCategory.Tires", "tires"),
                TuningProfileEntryCategories.Vehicles => StringResources.Get("Workspace.ReapplyCategory.Vehicles", "vehicles"),
                TuningProfileEntryCategories.Trailers => StringResources.Get("Workspace.ReapplyCategory.Trailers", "trailers"),
                TuningProfileEntryCategories.Rocks => StringResources.Get("Workspace.ReapplyCategory.Rocks", "rocks"),
                TuningProfileEntryCategories.General => StringResources.Get("Workspace.ReapplyCategory.General", "general"),
                TuningProfileEntryCategories.PhotoMode => StringResources.Get("Workspace.ReapplyCategory.PhotoMode", "photo mode"),
                _ => StringResources.Get("Workspace.ReapplyCategory.Pak", "pak files"),
            };

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
                lines.Add(StringResources.Format("Workspace.ReapplyReportMore", "  … and {0} more.", paths.Count - limit));
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

        public static string LoadedCount(int count) =>
            StringResources.Format("Engine.LoadedCount", "{0} engines loaded from pak.", count);

        public static string LoadedStatus(int count) =>
            StringResources.Format("Engine.LoadedStatus", "{0} engines loaded.", count);

        public static string LoadErrorStatus(string message) =>
            StringResources.Format("Engine.LoadErrorStatus", "Engine load error: {0}", message);

        public static string MultipliersAppliedStatus(int changedEngines, int updatedFiles) =>
            StringResources.Format(
                "Engine.MultipliersAppliedStatus",
                "Multipliers applied. Updated engines: {0}, files: {1}.",
                changedEngines,
                updatedFiles);

        public static string MultipliersSavedMessage(int changedEngines, int updatedFiles) =>
            StringResources.Format(
                "Engine.MultipliersSavedMessage",
                "Engine settings saved.\n\nUpdated engines: {0}\nUpdated files: {1}",
                changedEngines,
                updatedFiles);

        public static string IndividualSavedStatus(int changedEngines, int updatedFiles) =>
            StringResources.Format(
                "Engine.IndividualSavedStatus",
                "Individual changes saved. Engines: {0}, files: {1}.",
                changedEngines,
                updatedFiles);

        public static string IndividualSavedMessage(int changedEngines) =>
            StringResources.Format(
                "Engine.IndividualSavedMessage",
                "Individual engine changes saved.\n\nUpdated engines: {0}",
                changedEngines);

        public static string RestoreEnginesMessage(int changedEngines, int updatedFiles) =>
            StringResources.Format(
                "Engine.RestoreEnginesMessage",
                "Engine values were restored from the baseline.\n\nUpdated engines: {0}\nUpdated files: {1}",
                changedEngines,
                updatedFiles);
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

        public static string LoadedCount(int count) =>
            StringResources.Format("Gearbox.LoadedCount", "{0} gearboxes loaded from pak.", count);

        public static string MultipliersSavedMessage(int changedGearboxes, int updatedFiles) =>
            StringResources.Format(
                "Gearbox.MultipliersSavedMessage",
                "Gearbox settings saved.\n\nUpdated gearboxes: {0}\nUpdated files: {1}",
                changedGearboxes,
                updatedFiles);

        public static string IndividualSavedMessage(int changedGearboxes) =>
            StringResources.Format(
                "Gearbox.IndividualSavedMessage",
                "Individual gearbox changes saved.\n\nUpdated gearboxes: {0}",
                changedGearboxes);

        public static string RestoreGearboxesMessage(int changedGearboxes, int updatedFiles) =>
            StringResources.Format(
                "Gearbox.RestoreGearboxesMessage",
                "Gearbox values were restored from the baseline.\n\nUpdated gearboxes: {0}\nUpdated files: {1}",
                changedGearboxes,
                updatedFiles);
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
            StringResources.Format(
                "Suspension.MultipliersSavedMessage",
                "Suspension settings saved.\n\nUpdated suspensions: {0}\nUpdated files: {1}",
                changedSuspensions,
                updatedFiles);

        public static string IndividualSavedMessage(int changedSuspensions) =>
            StringResources.Format(
                "Suspension.IndividualSavedMessage",
                "Individual suspension changes saved.\n\nUpdated suspensions: {0}",
                changedSuspensions);

        public static string RestoreSuspensionsMessage(int changedSuspensions, int updatedFiles) =>
            StringResources.Format(
                "Suspension.RestoreSuspensionsMessage",
                "Suspension values were restored from the baseline.\n\nUpdated suspensions: {0}\nUpdated files: {1}",
                changedSuspensions,
                updatedFiles);
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
            StringResources.Format(
                "Tires.MultipliersSavedMessage",
                "Tire settings saved.\n\nUpdated tires: {0}\nUpdated files: {1}",
                changedTires,
                updatedFiles);

        public static string IndividualSavedMessage(int changedTires) =>
            StringResources.Format(
                "Tires.IndividualSavedMessage",
                "Individual tire changes saved.\n\nUpdated tires: {0}",
                changedTires);

        public static string RestoreTiresMessage(int changedTires, int updatedFiles) =>
            StringResources.Format(
                "Tires.RestoreTiresMessage",
                "Tire values were restored from the baseline.\n\nUpdated tires: {0}\nUpdated files: {1}",
                changedTires,
                updatedFiles);
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

        public static string LoadedCount(int count) =>
            StringResources.Format("Winch.LoadedCount", "{0} winches loaded from pak.", count);

        public static string LoadedStatus(int count) =>
            StringResources.Format("Winch.LoadedStatus", "{0} winches loaded.", count);

        public static string LoadErrorStatus(string message) =>
            StringResources.Format("Winch.LoadErrorStatus", "Winch load error: {0}", message);

        public static string MultipliersAppliedStatus(int changedWinches, int updatedFiles) =>
            StringResources.Format(
                "Winch.MultipliersAppliedStatus",
                "Multipliers applied. Updated winches: {0}, files: {1}.",
                changedWinches,
                updatedFiles);

        public static string MultipliersSavedMessage(int changedWinches, int updatedFiles) =>
            StringResources.Format(
                "Winch.MultipliersSavedMessage",
                "Winch settings saved.\n\nUpdated winches: {0}\nUpdated files: {1}",
                changedWinches,
                updatedFiles);

        public static string IndividualSavedStatus(int changedWinches, int updatedFiles) =>
            StringResources.Format(
                "Winch.IndividualSavedStatus",
                "Individual changes saved. Winches: {0}, files: {1}.",
                changedWinches,
                updatedFiles);

        public static string IndividualSavedMessage(int changedWinches) =>
            changedWinches <= 0
                ? StringResources.Get("Winch.NoChangesToSave", "No winch changes were detected to save.")
                : StringResources.Format(
                    "Winch.IndividualSavedMessage",
                    "Individual winch changes saved.\n\nUpdated winches: {0}",
                    changedWinches);

        public static string RestoreWinchesMessage(int changedWinches, int updatedFiles) =>
            StringResources.Format(
                "Winch.RestoreWinchesMessage",
                "Winch values were restored from the baseline.\n\nUpdated winches: {0}\nUpdated files: {1}",
                changedWinches,
                updatedFiles);
    }

    public static class CrashReport
    {
        public static string Title => StringResources.GetEnglish("CrashReport.Title", "Unexpected error");
        public static string Heading => StringResources.GetEnglish("CrashReport.Heading", "Something went wrong");
        public static string CopyReport => StringResources.GetEnglish("CrashReport.CopyReport", "Copy report");
        public static string OpenGitHubIssue => StringResources.GetEnglish("CrashReport.OpenGitHubIssue", "Open GitHub issue");
        public static string EmailReport => StringResources.GetEnglish("CrashReport.EmailReport", "Email report");
        public static string Continue => StringResources.GetEnglish("CrashReport.Continue", "Continue");
        public static string CloseApp => StringResources.GetEnglish("CrashReport.CloseApp", "Close app");
        public static string Copied => StringResources.GetEnglish("CrashReport.Copied", "Crash report copied to the clipboard.");
        public static string PreparingGitHub => StringResources.GetEnglish("CrashReport.PreparingGitHub", "Checking GitHub…");

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
            StringResources.FormatEnglish("CrashReport.LogSaved", "Saved locally:\n{0}", path);

        public static string ViewExistingIssue(int number) =>
            StringResources.FormatEnglish("CrashReport.ViewExistingIssue", "View existing issue #{0}", number);
    }
}
