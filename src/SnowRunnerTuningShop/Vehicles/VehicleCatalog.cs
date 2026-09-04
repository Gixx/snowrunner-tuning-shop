using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace SnowRunnerTuningShop.Vehicles;

public sealed record VehicleCatalogEntry(
    string Id,
    string PakId,
    string DisplayName,
    string Category,
    string ImageFile,
    string ImagePath);

public static class VehicleCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyList<VehicleCatalogEntry> Load()
    {
        var assetsDir = AppPaths.TryFindVehiclesAssetsDirectory();
        if (assetsDir is null)
        {
            return [];
        }

        var catalogPath = Path.Combine(assetsDir, "catalog.json");
        if (!File.Exists(catalogPath))
        {
            return [];
        }

        var json = File.ReadAllText(catalogPath);
        var rows = JsonSerializer.Deserialize<List<CatalogRow>>(json, JsonOptions) ?? [];

        return rows
            .Select(row =>
            {
                var imagePath = Path.Combine(assetsDir, row.ImageFile);
                return new VehicleCatalogEntry(
                    row.Id,
                    string.IsNullOrWhiteSpace(row.PakId) ? row.Id : row.PakId.Trim(),
                    row.DisplayName,
                    row.Category,
                    row.ImageFile,
                    File.Exists(imagePath) ? imagePath : string.Empty);
            })
            .OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static BitmapImage? TryLoadImage(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(imagePath, UriKind.Absolute);
            image.DecodePixelWidth = 220;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or System.Runtime.InteropServices.COMException or IOException)
        {
            return null;
        }
    }

    private sealed class CatalogRow
    {
        public string Id { get; set; } = "";
        public string PakId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Category { get; set; } = "";
        public string ImageFile { get; set; } = "";
    }
}
