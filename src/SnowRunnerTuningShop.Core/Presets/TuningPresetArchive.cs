using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SnowRunnerTuningShop.Core.Updates;

namespace SnowRunnerTuningShop.Core.Presets;

public static class TuningPresetArchive
{
    public const string Extension = ".tsa";
    public const string ManifestFileName = "manifest.json";
    public const string SettingsFileName = "settings.json";
    public const string FileFilter = "Tuning Shop archive (*.tsa)|*.tsa|All files (*.*)|*.*";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static TuningPresetPackage Read(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            throw new FileNotFoundException("Preset archive was not found.", archivePath);
        }

        using var stream = File.OpenRead(archivePath);
        return Read(stream, archivePath);
    }

    public static TuningPresetPackage Read(Stream stream, string? displayPath = null)
    {
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var manifestEntry = FindEntry(archive, ManifestFileName)
                ?? throw new InvalidDataException("Preset archive is missing manifest.json.");
            var settingsEntry = FindEntry(archive, SettingsFileName)
                ?? throw new InvalidDataException("Preset archive is missing settings.json.");

            var manifest = JsonSerializer.Deserialize<TuningPresetManifest>(
                               ReadUtf8(manifestEntry), JsonOptions)
                           ?? throw new InvalidDataException("Preset manifest.json is invalid.");
            ValidateManifest(manifest);

            var settings = JsonSerializer.Deserialize<TuningPresetSettings>(
                               ReadUtf8(settingsEntry), JsonOptions)
                           ?? throw new InvalidDataException("Preset settings.json is invalid.");
            settings.Entries ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(manifest.MinAppVersion)
                && AppSemVersion.TryParse(AppInfo.Version, out var installed)
                && AppSemVersion.TryParse(manifest.MinAppVersion, out var required)
                && installed.CompareTo(required) < 0)
            {
                throw new InvalidOperationException(
                    $"This preset requires app version {manifest.MinAppVersion} or newer (installed: {AppInfo.Version}).");
            }

            byte[]? iconBytes = null;
            if (!string.IsNullOrWhiteSpace(manifest.Icon))
            {
                var iconEntry = FindEntry(archive, manifest.Icon);
                if (iconEntry is not null)
                {
                    using var iconStream = iconEntry.Open();
                    using var memory = new MemoryStream();
                    iconStream.CopyTo(memory);
                    iconBytes = memory.ToArray();
                }
            }

            return new TuningPresetPackage(manifest, settings, iconBytes);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            var label = string.IsNullOrWhiteSpace(displayPath) ? "archive" : displayPath;
            throw new InvalidDataException($"Preset archive is not a valid .tsa file: {label}", ex);
        }
    }

    public static void Write(string archivePath, TuningPresetPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        ValidateManifest(package.Manifest);

        var directory = Path.GetDirectoryName(Path.GetFullPath(archivePath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = archivePath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = File.Create(tempPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteJsonEntry(archive, ManifestFileName, package.Manifest);
                WriteJsonEntry(archive, SettingsFileName, package.Settings);

                if (package.IconBytes is { Length: > 0 }
                    && !string.IsNullOrWhiteSpace(package.Manifest.Icon))
                {
                    var iconEntry = archive.CreateEntry(package.Manifest.Icon.Replace('\\', '/'));
                    using var iconStream = iconEntry.Open();
                    iconStream.Write(package.IconBytes);
                }
            }

            File.Move(tempPath, archivePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public static void ValidateManifest(TuningPresetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            throw new InvalidDataException("Preset manifest is missing id.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            throw new InvalidDataException("Preset manifest is missing name.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidDataException("Preset manifest is missing version.");
        }

        if (manifest.SchemaVersion < 1)
        {
            throw new InvalidDataException("Preset manifest schemaVersion is invalid.");
        }
    }

    private static void WriteJsonEntry<T>(ZipArchive archive, string entryName, T value)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(JsonSerializer.Serialize(value, JsonOptions));
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive archive, string fileName)
    {
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (name.Equals(fileName, StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("/" + fileName, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    private static string ReadUtf8(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
