using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using SnowRunnerTuningShop.Core.Tuning;

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
    /// Scales every existing <c>Price="…"</c> attribute from baseline by <paramref name="priceMultiplier"/>.
    /// Skips missing/zero prices; does not invent Price attributes.
    /// </summary>
    public static string ApplyPriceMultiplier(string text, double priceMultiplier)
    {
        if (TuningMultiplierPresets.IsBaselineMultiplier(priceMultiplier))
        {
            return text;
        }

        return PriceRegex.Replace(text, match =>
        {
            if (!int.TryParse(
                    match.Groups["value"].Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var price)
                || price <= 0)
            {
                return match.Value;
            }

            var scaled = (int)Math.Clamp(
                Math.Round(price * priceMultiplier, MidpointRounding.AwayFromZero),
                0,
                9_999_999);
            return string.Concat(
                match.Value.AsSpan(0, match.Groups["value"].Index - match.Index),
                scaled.ToString(CultureInfo.InvariantCulture),
                match.Value.AsSpan(match.Groups["value"].Index - match.Index + match.Groups["value"].Length));
        });
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
