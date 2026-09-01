using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace SnowRunnerTuningShop.Trailers;

public sealed record TrailerCatalogEntry(
    string Id,
    string DisplayName,
    string Hitch,
    string Function,
    bool IsQuest,
    string ImageFile,
    string ImagePath);

public static class TrailerCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyList<TrailerCatalogEntry> Load()
    {
        var assetsDir = AppPaths.TryFindTrailersAssetsDirectory();
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
                var hitch = (row.Hitch ?? "").Trim().ToLowerInvariant();
                var function = (row.Function ?? "").Trim().ToLowerInvariant();
                var imageFile = ResolveImageFile(assetsDir, row.ImageFile, hitch, function, row.IsQuest);
                var imagePath = Path.Combine(assetsDir, imageFile.Replace('/', Path.DirectorySeparatorChar));
                return new TrailerCatalogEntry(
                    row.Id,
                    row.DisplayName,
                    hitch,
                    function,
                    row.IsQuest,
                    imageFile,
                    File.Exists(imagePath) ? imagePath : string.Empty);
            })
            .OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static BitmapImage? TryLoadImage(string imagePath, int decodePixelWidth = 220)
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
            image.DecodePixelWidth = decodePixelWidth;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or System.Runtime.InteropServices.COMException or IOException)
        {
            return null;
        }
    }

    public static string ResolveDefaultImageFile(string hitch, string function, bool isQuest)
    {
        if (isQuest || function.Equals("mission", StringComparison.OrdinalIgnoreCase) || hitch == "other")
        {
            return "default/trailer-mission.jpg";
        }

        return hitch switch
        {
            "scout" => "default/trailer-scout.jpg",
            "saddle-low" => "default/trailer-saddle-low.jpg",
            "saddle-high" => "default/trailer-saddle-high.jpg",
            _ => "default/trailer-standard.jpg",
        };
    }

    private static string ResolveImageFile(
        string assetsDir,
        string? imageFile,
        string hitch,
        string function,
        bool isQuest)
    {
        var requested = (imageFile ?? "").Trim().Replace('/', Path.DirectorySeparatorChar);
        if (requested.Length > 0)
        {
            var requestedPath = Path.Combine(assetsDir, requested);
            if (File.Exists(requestedPath))
            {
                return imageFile!.Trim();
            }
        }

        return ResolveDefaultImageFile(hitch, function, isQuest);
    }

    private sealed class CatalogRow
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Hitch { get; set; } = "";
        public string Function { get; set; } = "";
        public bool IsQuest { get; set; }
        public string ImageFile { get; set; } = "";
    }
}
