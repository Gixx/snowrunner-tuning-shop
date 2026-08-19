namespace SnowRunnerTuningShop.Localization;

public static class UiText
{
    public static class Main
    {
        public const string Subtitle = "Fine-tune initial.pak — starting with winches";
        public const string NoPakSelected = "No initial.pak selected";
        public const string Browse = "Browse...";
        public const string Load = "Load";
        public const string TabOverview = "Overview";
        public const string TabWinch = "Winch";
        public const string OverviewTitle = "Overview";
        public const string OverviewPlaceholder = "Open an initial.pak file to get started.";
        public const string CategoriesTitle = "Tuning categories";
        public const string CategoryColumn = "Category";
        public const string FilesColumn = "Files";
        public const string SampleFileColumn = "Sample file";
        public const string ReadyStatus = "Ready. Select an initial.pak file.";
        public const string ExamplePakDetected = "Example initial.pak detected in the example.data folder.";
        public const string BrowseDialogTitle = "Select initial.pak";
        public const string BrowseDialogFilter = "SnowRunner pak (*.pak)|*.pak|All files (*.*)|*.*";
        public const string FileSelectedStatus = "File selected. Click Load.";
        public const string LoadingPakStatus = "Loading pak...";
        public const string LoadFailedOverview = "Failed to load pak.";
        public const string LoadErrorTitle = "Load error";

        public static string LoadSuccessStatus(int entryCount) =>
            $"Loaded successfully: {entryCount:N0} entries. Winch data is ready.";

        public static string ErrorStatus(string message) => $"Error: {message}";

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

    public static class Winch
    {
        public const string GlobalMultipliersTitle = "Global multipliers (relative to the baseline values)";
        public const string LengthMultiplierDefault = "Length multiplier: 1 (baseline)";
        public const string StrengthMultiplierDefault = "Strength multiplier: 1 (baseline)";
        public const string AutonomousAll = "Autonomous all";
        public const string ApplyMultipliers = "Apply multipliers";
        public const string SaveIndividualChanges = "Save individual changes";
        public const string RefreshList = "Refresh list";
        public const string CategoryColumn = "Category";
        public const string NameColumn = "Name";
        public const string LengthColumn = "Length (m)";
        public const string StrengthColumn = "Strength";
        public const string AutonomousColumn = "Autonomous";
        public const string NoData = "No winch data loaded.";
        public const string LoadPakFirst = "Load an initial.pak file first.";
        public const string SaveSuccessTitle = "Saved successfully";
        public const string SaveErrorTitle = "Save error";
        public const string LoadErrorTitle = "Load error";
        public const string BaselineTitle = "Baseline reference";
        public const string SetBaselineFromFile = "Set baseline from file...";
        public const string ImportPythonBaseline = "Import Python editor backup";
        public const string ClearBaseline = "Clear baseline";
        public const string RestoreWinchesToBaseline = "Restore winches to baseline";
        public const string RestorePakToBaseline = "Restore entire pak...";
        public const string RestorePakConfirmTitle = "Restore entire pak?";
        public const string RestorePakConfirmMessage =
            "This replaces the entire initial.pak with the baseline copy. " +
            "All tuning changes in the pak will be lost, not just winches.";
        public const string RestoreWinchesSuccessTitle = "Winches restored";
        public const string RestorePakSuccessTitle = "Pak restored";
        public const string SelectBaselineDialogTitle = "Select unmodified initial.pak";
        public const string BaselineUpdatedTitle = "Baseline updated";

        public static string BaselineMissing(string? pythonHint) =>
            "Baseline not set. Global multipliers need a clean reference initial.pak." +
            (string.IsNullOrWhiteSpace(pythonHint) ? "" : $"{Environment.NewLine}{pythonHint}");

        public static string BaselineReady(string sourceDescription, string fileName, DateTime lastWriteUtc) =>
            $"Baseline: {sourceDescription} ({fileName}, {lastWriteUtc.ToLocalTime():yyyy-MM-dd HH:mm})";

        public static string PythonBackupsFound(int count) =>
            $"Found {count} Python editor backup(s). The oldest one is usually the stock file.";

        public const string PythonBackupsMissing =
            "No Python editor backups were found for this initial.pak.";

        public static string BaselineImportedMessage(string sourceDescription, string baselinePath) =>
            $"Baseline imported.{Environment.NewLine}{Environment.NewLine}" +
            $"Source: {sourceDescription}{Environment.NewLine}" +
            $"Saved as: {baselinePath}";

        public static string LoadedCount(int count) => $"{count} winches loaded from pak.";

        public static string LoadedStatus(int count) => $"{count} winches loaded.";

        public static string LoadErrorStatus(string message) => $"Winch load error: {message}";

        public static string MultipliersAppliedStatus(int changedWinches, int updatedFiles, string backupFileName) =>
            $"Multipliers applied. Updated winches: {changedWinches}, files: {updatedFiles}. Backup: {backupFileName}";

        public static string MultipliersSavedMessage(int changedWinches, int updatedFiles, string backupPath) =>
            $"Winch settings saved.{Environment.NewLine}{Environment.NewLine}" +
            $"Updated winches: {changedWinches}{Environment.NewLine}" +
            $"Updated files: {updatedFiles}{Environment.NewLine}" +
            $"Backup: {backupPath}";

        public static string IndividualSavedStatus(int changedWinches, int updatedFiles) =>
            $"Individual changes saved. Winches: {changedWinches}, files: {updatedFiles}.";

        public static string IndividualSavedMessage(int changedWinches, string backupPath) =>
            $"Individual winch changes saved.{Environment.NewLine}{Environment.NewLine}" +
            $"Updated winches: {changedWinches}{Environment.NewLine}" +
            $"Backup: {backupPath}";

        public static string RestoreWinchesMessage(int changedWinches, int updatedFiles, string backupPath) =>
            $"Winch values were restored from the baseline.{Environment.NewLine}{Environment.NewLine}" +
            $"Updated winches: {changedWinches}{Environment.NewLine}" +
            $"Updated files: {updatedFiles}{Environment.NewLine}" +
            $"Backup: {backupPath}";

        public static string RestorePakMessage(string backupPath) =>
            $"The entire initial.pak was restored from the baseline.{Environment.NewLine}{Environment.NewLine}" +
            $"Backup of the previous file: {backupPath}";
    }
}
