using System.IO.Compression;
using System.Text.RegularExpressions;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Core.Trucks;

/// <summary>
/// Diff-lock mode resolution and truck XML / addon socket mutations.
/// </summary>
public static class TruckDiffLockXml
{
    private static readonly Regex AddonSocketsBlockRegex = new(
        @"<AddonSockets\b(?<attrs>[^<>]*)>(?<body>.*?)</AddonSockets>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex DiffLockInstalledRegex = new(
        @"DiffLockInstalled\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string ApplyAlwaysOnDiffLock(ZipArchive archive, string truckXml, string truckId)
    {
        if (!HasNativeDiffLockInfrastructure(archive, truckXml, truckId))
        {
            return VehicleGameDataXml.SetTruckDataAttribute(truckXml, "DiffLockType", "Always");
        }

        ResolveDiffLockAddonNames(archive, truckId, truckXml, out _, out var defaultAddon);
        truckXml = VehicleGameDataXml.SetTruckDataAttribute(truckXml, "DiffLockType", "Always");
        return SetDiffLockDefaultAddon(truckXml, defaultAddon);
    }

    public static TruckDiffLockMode ResolveDiffLockMode(
        ZipArchive archive,
        string truckXml,
        string? diffRaw,
        bool hasNativeDiffLockOptions)
    {
        if (!string.IsNullOrWhiteSpace(diffRaw)
            && diffRaw.Equals("Always", StringComparison.OrdinalIgnoreCase))
        {
            return TruckDiffLockMode.AlwaysOn;
        }

        if (!hasNativeDiffLockOptions)
        {
            return TruckDiffLockMode.None;
        }

        if (TryGetDiffLockDefaultAddonName(truckXml, out var addonName)
            && TryReadAddonText(archive, addonName, out var addonText))
        {
            var installed = DiffLockInstalledRegex.Match(addonText);
            if (installed.Success)
            {
                return installed.Groups["value"].Value.Equals("true", StringComparison.OrdinalIgnoreCase)
                    ? TruckDiffLockMode.Switchable
                    : TruckDiffLockMode.Upgradeable;
            }
        }

        return ParseDiffLockMode(diffRaw);
    }

    public static string ApplyDiffLock(
        ZipArchive archive,
        string truckXml,
        TruckTuningDefinition truck)
    {
        if (!truck.HasNativeDiffLockOptions)
        {
            var simpleType = truck.DiffLock switch
            {
                TruckDiffLockMode.AlwaysOn => "Always",
                _ => "None",
            };
            return VehicleGameDataXml.SetTruckDataAttribute(truckXml, "DiffLockType", simpleType);
        }

        ResolveDiffLockAddonNames(
            archive,
            truck.TruckId,
            truckXml,
            out var installedAddon,
            out var defaultAddon);

        var diffType = truck.DiffLock switch
        {
            TruckDiffLockMode.AlwaysOn => "Always",
            TruckDiffLockMode.None => "None",
            TruckDiffLockMode.Upgradeable => "Uninstalled",
            TruckDiffLockMode.Switchable when IsInstalledStyle(truck.DiffLockTypeRaw) => truck.DiffLockTypeRaw,
            _ => "Installed",
        };
        truckXml = VehicleGameDataXml.SetTruckDataAttribute(truckXml, "DiffLockType", diffType);

        var defaultAddonName = truck.DiffLock switch
        {
            TruckDiffLockMode.Switchable => installedAddon,
            TruckDiffLockMode.Upgradeable => defaultAddon,
            TruckDiffLockMode.AlwaysOn => defaultAddon,
            TruckDiffLockMode.None => defaultAddon,
            _ => defaultAddon,
        };

        return SetDiffLockDefaultAddon(truckXml, defaultAddonName);
    }

    public static bool HasNativeDiffLockInfrastructure(ZipArchive archive, string truckXml, string truckId)
    {
        foreach (Match match in AddonSocketsBlockRegex.Matches(truckXml))
        {
            if (IsDiffLockSocketBlock(match.Groups["attrs"].Value, match.Groups["body"].Value))
            {
                return true;
            }
        }

        return FindAddonEntry(archive, truckId + "_diff_lock") is not null
            || FindAddonEntry(archive, truckId + "_diff_lock_default") is not null;
    }

    public static void ResolveDiffLockAddonNames(
        ZipArchive archive,
        string truckId,
        string truckXml,
        out string installedAddonName,
        out string defaultAddonName)
    {
        installedAddonName = truckId + "_diff_lock";
        defaultAddonName = truckId + "_diff_lock_default";

        if (TryGetDiffLockDefaultAddonName(truckXml, out var currentDefault))
        {
            if (currentDefault.EndsWith("_default", StringComparison.OrdinalIgnoreCase))
            {
                defaultAddonName = currentDefault;
                installedAddonName = currentDefault[..^"_default".Length];
            }
            else
            {
                installedAddonName = currentDefault;
                defaultAddonName = currentDefault + "_default";
            }
        }

        if (TryReadAddonText(archive, installedAddonName, out _))
        {
            return;
        }

        if (TryReadAddonText(archive, defaultAddonName, out _))
        {
            if (!defaultAddonName.EndsWith("_default", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            installedAddonName = defaultAddonName[..^"_default".Length];
        }
    }

    public static string SetDiffLockDefaultAddon(string truckXml, string defaultAddonName)
    {
        foreach (Match match in AddonSocketsBlockRegex.Matches(truckXml))
        {
            var attrs = match.Groups["attrs"].Value;
            var body = match.Groups["body"].Value;
            if (!IsDiffLockSocketBlock(attrs, body))
            {
                continue;
            }

            var updatedAttrs = attrs;
            if (!VehicleGameDataXml.SetOrReplaceAttribute(ref updatedAttrs, "DefaultAddon", defaultAddonName))
            {
                return truckXml;
            }

            var replacement = $"<AddonSockets{updatedAttrs}>{body}</AddonSockets>";
            return string.Concat(
                truckXml.AsSpan(0, match.Index),
                replacement,
                truckXml.AsSpan(match.Index + match.Length));
        }

        return truckXml;
    }

    public static bool TryGetDiffLockDefaultAddonName(string truckXml, out string addonName)
    {
        foreach (Match match in AddonSocketsBlockRegex.Matches(truckXml))
        {
            var attrs = match.Groups["attrs"].Value;
            var body = match.Groups["body"].Value;
            if (!IsDiffLockSocketBlock(attrs, body))
            {
                continue;
            }

            var parsed = VehicleGameDataXml.ParseAttributes(attrs);
            addonName = parsed.TryGetValue("DefaultAddon", out var value) ? value : "";
            if (!string.IsNullOrWhiteSpace(addonName))
            {
                return true;
            }
        }

        addonName = "";
        return false;
    }

    public static TruckDiffLockMode ParseDiffLockMode(string? raw) =>
        (raw ?? "").Trim() switch
        {
            var value when value.Equals("Always", StringComparison.OrdinalIgnoreCase) => TruckDiffLockMode.AlwaysOn,
            var value when value.Equals("None", StringComparison.OrdinalIgnoreCase) => TruckDiffLockMode.None,
            var value when value.Equals("Uninstalled", StringComparison.OrdinalIgnoreCase) => TruckDiffLockMode.Upgradeable,
            _ => TruckDiffLockMode.Switchable,
        };

    public static bool IsInstalledStyle(string raw) =>
        raw.Equals("Installed", StringComparison.OrdinalIgnoreCase)
        || raw.Equals("Switchable", StringComparison.OrdinalIgnoreCase)
        || raw.Equals("Connected", StringComparison.OrdinalIgnoreCase);

    private static bool IsDiffLockSocketBlock(string attrs, string body) =>
        attrs.Contains("diff_lock", StringComparison.OrdinalIgnoreCase)
        || body.Contains("DiffLock", StringComparison.OrdinalIgnoreCase)
        || body.Contains("diff_lock", StringComparison.OrdinalIgnoreCase);

    private static bool TryReadAddonText(ZipArchive archive, string addonName, out string text)
    {
        var entry = FindAddonEntry(archive, addonName);
        if (entry is null)
        {
            text = "";
            return false;
        }

        text = PartXmlHelpers.ReadEntryUtf8(entry);
        return true;
    }

    private static ZipArchiveEntry? FindAddonEntry(ZipArchive archive, string addonName)
    {
        var suffix = "/" + addonName + ".xml";
        foreach (var entry in archive.Entries)
        {
            var path = PartPakPipeline.NormalizeEntryPath(entry.FullName);
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }
}
