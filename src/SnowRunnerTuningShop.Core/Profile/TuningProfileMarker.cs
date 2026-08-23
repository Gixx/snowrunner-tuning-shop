using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using SnowRunnerTuningShop.Core.Pak;

namespace SnowRunnerTuningShop.Core.Profile;

public static class TuningProfileMarker
{
    public const string EntryPath = "[media]/_tuning_shop/marker.xml";

    private static readonly Regex BaselineSha256Regex = new(
        @"\bBaselineSha256\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsMarkerEntry(string entryPath) =>
        PakEntryLocator.EntryPathsEqual(entryPath, EntryPath);

    public static bool TryReadBaselineSha256(string pakPath, out string baselineSha256)
    {
        baselineSha256 = "";
        if (!File.Exists(pakPath))
        {
            return false;
        }

        using var archive = ZipFile.OpenRead(pakPath);
        var entry = PakEntryLocator.FindEntry(archive, EntryPath);
        if (entry is null)
        {
            return false;
        }

        using var reader = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        var match = BaselineSha256Regex.Match(text);
        if (!match.Success)
        {
            return false;
        }

        baselineSha256 = match.Groups["value"].Value.Trim();
        return baselineSha256.Length > 0;
    }

    public static bool HasMarker(string pakPath)
    {
        if (!File.Exists(pakPath))
        {
            return false;
        }

        using var archive = ZipFile.OpenRead(pakPath);
        return PakEntryLocator.FindEntry(archive, EntryPath) is not null;
    }

    public static byte[] BuildMarkerXml(string editionId, string baselineSha256, DateTime appliedUtc)
    {
        var applied = appliedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        var xml =
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <TuningShopMarker AppVersion="{AppInfo.Version}" EditionId="{EscapeXml(editionId)}" BaselineSha256="{EscapeXml(baselineSha256)}" AppliedUtc="{applied}" />
             """;

        return Encoding.UTF8.GetBytes(xml);
    }

    private static string EscapeXml(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
}
