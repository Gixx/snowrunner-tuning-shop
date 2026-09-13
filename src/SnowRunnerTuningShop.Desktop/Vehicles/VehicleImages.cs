using System.IO;
using Avalonia.Media.Imaging;

namespace SnowRunnerTuningShop.Desktop.Vehicles;

public static class VehicleImages
{
    public static Bitmap? TryLoadBitmap(string? imagePath, int decodeWidth = 0)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(imagePath);
            var bitmap = new Bitmap(stream);
            if (decodeWidth <= 0 || bitmap.PixelSize.Width <= decodeWidth)
            {
                return bitmap;
            }

            var height = Math.Max(
                1,
                (int)Math.Round(bitmap.PixelSize.Height * (decodeWidth / (double)bitmap.PixelSize.Width)));
            var scaled = bitmap.CreateScaledBitmap(new Avalonia.PixelSize(decodeWidth, height));
            bitmap.Dispose();
            return scaled;
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException or IOException)
        {
            return null;
        }
    }
}
