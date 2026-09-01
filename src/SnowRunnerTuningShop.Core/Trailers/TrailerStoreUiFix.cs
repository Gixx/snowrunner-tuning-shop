using System.IO.Compression;
using System.Text.RegularExpressions;
using SnowRunnerTuningShop.Core.Strings;

namespace SnowRunnerTuningShop.Core.Trailers;

internal static class TrailerStoreUiFix
{
    internal const string TrainDlc17NameKey = "UI_TRAIN_DLC_17_NAME";
    internal const string WindBladeNameKey = "UI_SEMITRAILER_WIND_BLADE_NAME";
    internal const string WindBladeDescKey = "UI_SEMITRAILER_WIND_BLADE_DESC";

    private static readonly Dictionary<string, StoreUiOverride> Overrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["train_dlc_17"] = new(TrainDlc17NameKey, null),
        ["semitrailer_wind_blade"] = new(WindBladeNameKey, WindBladeDescKey),
    };

    public static bool AppliesTo(string trailerId) => Overrides.ContainsKey(trailerId);

    public static string ApplyXml(string trailerId, string text)
    {
        if (!Overrides.TryGetValue(trailerId, out var overlay))
        {
            return text;
        }

        var updated = SetAttributeValues(text, "UiName", overlay.UiNameKey);
        if (!string.IsNullOrEmpty(overlay.UiDescKey))
        {
            updated = SetAttributeValues(updated, "UiDesc", overlay.UiDescKey);
        }

        return updated;
    }

    public static void AddStringTableReplacements(ZipArchive archive, IDictionary<string, byte[]> replacements)
    {
        foreach (var entry in archive.Entries)
        {
            var path = entry.FullName.Replace('\\', '/');
            if (!IsStringTable(path))
            {
                continue;
            }

            using var stream = entry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var original = memory.ToArray();
            var language = GetLanguageId(path);
            var values = BuildStringValues(language, original);
            if (!GameStringsWriter.TryUpsert(original, values, out var updated))
            {
                continue;
            }

            replacements[path] = updated;
        }
    }

    private static Dictionary<string, string> BuildStringValues(string language, byte[] fileBytes)
    {
        var existing = GameStringsWriter.Parse(GameStringsWriter.Decode(fileBytes));
        var trainName = existing.TryGetValue("UI_TRAIN_NAME", out var vanillaTrain) && !string.IsNullOrWhiteSpace(vanillaTrain)
            ? vanillaTrain
            : "Diesel Locomotive";

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [TrainDlc17NameKey] = $"{trainName} {SeasonSuffix(language)}",
            [WindBladeNameKey] = WindBladeName(language),
            [WindBladeDescKey] = WindBladeDesc(language),
        };
    }

    private static string SetAttributeValues(string text, string attributeName, string value)
    {
        var regex = new Regex(
            $@"(?<prefix>\b{Regex.Escape(attributeName)}\s*=\s*"")(?<value>[^""]*)(?<suffix>"")",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return regex.Replace(text, match =>
        {
            if (string.Equals(match.Groups["value"].Value, value, StringComparison.Ordinal))
            {
                return match.Value;
            }

            return match.Groups["prefix"].Value + value + match.Groups["suffix"].Value;
        });
    }

    private static bool IsStringTable(string path)
    {
        var name = Path.GetFileName(path);
        return name.StartsWith("strings_", StringComparison.OrdinalIgnoreCase)
            && name.EndsWith(".str", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLanguageId(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        const string prefix = "strings_";
        return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? name[prefix.Length..]
            : "english";
    }

    private static string SeasonSuffix(string language) => language.ToLowerInvariant() switch
    {
        "french" => "(Saison 17)",
        "spanish" => "(Temporada 17)",
        "brazilian_portuguese" => "(Temporada 17)",
        "italian" => "(Stagione 17)",
        "polish" => "(Sezon 17)",
        "russian" => "(Сезон 17)",
        "czech" => "(Sezóna 17)",
        "japanese" => "(シーズン17)",
        "korean" => "(시즌 17)",
        "chinese_simplified" => "（第17季）",
        "chinese_traditional" => "（第17季）",
        _ => "(Season 17)",
    };

    private static string WindBladeName(string language) => language.ToLowerInvariant() switch
    {
        "german" => "Windkraftanlagenflügel",
        "french" => "Pale d'éolienne",
        "spanish" => "Pala de aerogenerador",
        "brazilian_portuguese" => "Pá de turbina eólica",
        "italian" => "Pala di turbina eolica",
        "polish" => "Łopata turbiny wiatrowej",
        "russian" => "Лопасть ветряной турбины",
        "czech" => "List větrné elektrárny",
        "japanese" => "風力タービンブレード",
        "korean" => "풍력 터빈 블레이드",
        "chinese_simplified" => "风力涡轮机叶片",
        "chinese_traditional" => "風力渦輪機葉片",
        _ => "Wind Turbine Blade",
    };

    private static string WindBladeDesc(string language) => language.ToLowerInvariant() switch
    {
        "german" => "Ein Sattelauflieger mit einem Rotorblatt einer Windkraftanlage.",
        "french" => "Une semi-remorque transportant une pale d'éolienne.",
        "spanish" => "Un semirremolque que transporta una pala de aerogenerador.",
        "brazilian_portuguese" => "Uma semirreboque transportando uma pá de turbina eólica.",
        "italian" => "Un semirimorchio che trasporta una pala di turbina eolica.",
        "polish" => "Naczepa z łopatą turbiny wiatrowej.",
        "russian" => "Полуприцеп с лопастью ветряной турбины.",
        "czech" => "Návěs s listem větrné elektrárny.",
        "japanese" => "風力タービンのブレードを積んだセミトレーラー。",
        "korean" => "풍력 터빈 블레이드를 실은 세미 트레일러.",
        "chinese_simplified" => "装载风力涡轮机叶片的半挂车。",
        "chinese_traditional" => "載有風力渦輪機葉片的半拖車。",
        _ => "A semi-trailer carrying a wind turbine blade.",
    };

    private sealed record StoreUiOverride(string UiNameKey, string? UiDescKey);
}
