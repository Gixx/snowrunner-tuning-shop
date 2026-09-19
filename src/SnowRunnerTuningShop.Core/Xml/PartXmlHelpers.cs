using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace SnowRunnerTuningShop.Core.Xml;

public static class PartXmlHelpers
{
    private static readonly Regex PriceRegex = new(
        @"\bPrice\s*=\s*""(?<value>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static int ExtractPrice(string block)
    {
        var match = PriceRegex.Match(block);
        if (!match.Success)
        {
            return 0;
        }

        return int.TryParse(match.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var price)
            ? price
            : 0;
    }

    /// <summary>
    /// Updates the first <c>Price="…"</c> in <paramref name="text"/>, or adds Price on the first
    /// <c>GameData</c> open tag when missing. Returns false when nothing changed.
    /// </summary>
    public static bool TrySetPrice(ref string text, int price)
    {
        var value = price.ToString(CultureInfo.InvariantCulture);
        var match = PriceRegex.Match(text);
        if (match.Success)
        {
            if (string.Equals(match.Groups["value"].Value, value, StringComparison.Ordinal))
            {
                return false;
            }

            var valueGroup = match.Groups["value"];
            text = string.Concat(
                text.AsSpan(0, valueGroup.Index),
                value,
                text.AsSpan(valueGroup.Index + valueGroup.Length));
            return true;
        }

        var updated = VehicleGameDataXml.SetGameDataAttribute(text, "Price", value);
        if (string.Equals(updated, text, StringComparison.Ordinal))
        {
            return false;
        }

        text = updated;
        return true;
    }

    public static string FormatUsedBy(IReadOnlyList<string> vehicleNames)
    {
        if (vehicleNames.Count == 0)
        {
            return "—";
        }

        if (vehicleNames.Count <= 3)
        {
            return string.Join(", ", vehicleNames);
        }

        return $"{string.Join(", ", vehicleNames.Take(3))} (+{vehicleNames.Count - 3})";
    }

    public static string FormatUsedByTooltip(IReadOnlyList<string> vehicleNames, string emptyMessage) =>
        vehicleNames.Count == 0
            ? emptyMessage
            : string.Join(", ", vehicleNames);

    public static string ReadEntryUtf8(ZipArchiveEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public static byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
