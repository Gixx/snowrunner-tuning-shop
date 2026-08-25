using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace SnowRunnerTuningShop.Vehicles;

public sealed record VehicleManufacturerInfo(string Id, string Name, string? LogoPath);

public sealed record VehicleCountryInfo(string Code, string Name, string? FlagPath, string OvalCode);

public sealed record VehicleMetaInfo(
    string Id,
    string? BasedOn,
    int? Year,
    VehicleManufacturerInfo? Manufacturer,
    VehicleCountryInfo? Country)
{
    public string? YearDisplay => Year?.ToString();
}

public static class VehicleMetadata
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyDictionary<string, VehicleMetaInfo> Load()
    {
        var assetsDir = AppPaths.TryFindVehiclesAssetsDirectory();
        if (assetsDir is null)
        {
            return new Dictionary<string, VehicleMetaInfo>(StringComparer.OrdinalIgnoreCase);
        }

        var path = Path.Combine(assetsDir, "metadata.json");
        if (!File.Exists(path))
        {
            return new Dictionary<string, VehicleMetaInfo>(StringComparer.OrdinalIgnoreCase);
        }

        var json = File.ReadAllText(path);
        var root = JsonSerializer.Deserialize<MetadataRoot>(json, JsonOptions);
        if (root?.Vehicles is null)
        {
            return new Dictionary<string, VehicleMetaInfo>(StringComparer.OrdinalIgnoreCase);
        }

        var manufacturers = (root.Manufacturers ?? [])
            .Where(m => !string.IsNullOrWhiteSpace(m.Id))
            .ToDictionary(
                m => m.Id,
                m => new VehicleManufacturerInfo(
                    m.Id,
                    string.IsNullOrWhiteSpace(m.Name) ? m.Id : m.Name,
                    ResolveAssetPath(assetsDir, m.LogoFile)),
                StringComparer.OrdinalIgnoreCase);

        var countries = (root.Countries ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c.Code))
            .ToDictionary(
                c => c.Code,
                c => new VehicleCountryInfo(
                    c.Code,
                    string.IsNullOrWhiteSpace(c.Name) ? c.Code : c.Name,
                    ResolveAssetPath(assetsDir, c.FlagFile),
                    CountryMarks.OvalCode(c.Code, c.OvalCode)),
                StringComparer.OrdinalIgnoreCase);

        // Ensure USSR entry exists even if omitted from JSON countries list.
        if (!countries.ContainsKey("SU"))
        {
            countries["SU"] = new VehicleCountryInfo(
                "SU",
                "USSR",
                ResolveAssetPath(assetsDir, "flags/su.png"),
                CountryMarks.OvalCode("SU"));
        }

        var map = new Dictionary<string, VehicleMetaInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var vehicle in root.Vehicles)
        {
            if (string.IsNullOrWhiteSpace(vehicle.Id))
            {
                continue;
            }

            VehicleManufacturerInfo? manufacturer = null;
            if (!string.IsNullOrWhiteSpace(vehicle.ManufacturerId)
                && manufacturers.TryGetValue(vehicle.ManufacturerId, out var mfg))
            {
                manufacturer = mfg;
            }

            var year = vehicle.Year ?? vehicle.YearFrom;
            var countryCode = vehicle.CountryCode;
            var countryName = vehicle.CountryName;
            // Runtime safety net: Soviet-era plants before 1991 → USSR
            if (year is int y
                && y < 1991
                && countryCode is not null
                && (countryCode.Equals("RU", StringComparison.OrdinalIgnoreCase)
                    || countryCode.Equals("UA", StringComparison.OrdinalIgnoreCase)
                    || countryCode.Equals("BY", StringComparison.OrdinalIgnoreCase)))
            {
                countryCode = "SU";
                countryName = "USSR";
            }

            VehicleCountryInfo? country = null;
            if (!string.IsNullOrWhiteSpace(countryCode)
                && countries.TryGetValue(countryCode, out var c))
            {
                country = string.IsNullOrWhiteSpace(countryName) || countryName == c.Name
                    ? c
                    : c with { Name = countryName! };
            }
            else if (!string.IsNullOrWhiteSpace(countryCode))
            {
                country = new VehicleCountryInfo(
                    countryCode,
                    string.IsNullOrWhiteSpace(countryName) ? countryCode : countryName!,
                    ResolveAssetPath(assetsDir, $"flags/{countryCode.ToLowerInvariant()}.png"),
                    CountryMarks.OvalCode(countryCode));
            }

            map[vehicle.Id] = new VehicleMetaInfo(
                vehicle.Id,
                string.IsNullOrWhiteSpace(vehicle.BasedOn) ? null : vehicle.BasedOn,
                year,
                manufacturer,
                country);
        }

        return map;
    }

    public static BitmapImage? TryLoadImage(string? imagePath, int decodePixelWidth = 160)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return null;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(imagePath, UriKind.Absolute);
        image.DecodePixelWidth = decodePixelWidth;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static string? ResolveAssetPath(string assetsDir, string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
        {
            return null;
        }

        var full = Path.Combine(assetsDir, relative.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(full) ? full : null;
    }

    private sealed class MetadataRoot
    {
        public List<ManufacturerRow>? Manufacturers { get; set; }
        public List<CountryRow>? Countries { get; set; }
        public List<VehicleRow>? Vehicles { get; set; }
    }

    private sealed class ManufacturerRow
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? LogoFile { get; set; }
    }

    private sealed class CountryRow
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? FlagFile { get; set; }
        public string? OvalCode { get; set; }
    }

    private sealed class VehicleRow
    {
        public string Id { get; set; } = "";
        public string? ManufacturerId { get; set; }
        public string? BasedOn { get; set; }
        public int? Year { get; set; }
        /// <summary>Legacy field; prefer <see cref="Year"/>.</summary>
        public int? YearFrom { get; set; }
        public string? CountryCode { get; set; }
        public string? CountryName { get; set; }
    }
}
