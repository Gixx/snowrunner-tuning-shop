using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using SnowRunnerTuningShop.Core.AddonCapacity;
using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.Crane;
using SnowRunnerTuningShop.Core.Engine;
using SnowRunnerTuningShop.Core.Gearbox;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Suspension;
using SnowRunnerTuningShop.Core.Tires;
using SnowRunnerTuningShop.Core.Trucks;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Core.Winch;
using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.PakSmoke;

/// <summary>
/// Level B: local-only smoke against a real baseline/working pak.
/// Not run by CI — invoke manually:
///   dotnet run --project tests/SnowRunnerTuningShop.PakSmoke -- [pakPath]
/// </summary>
internal static class Program
{
    private static readonly double[] Multipliers =
    [
        TuningMultiplierPresets.Values[2], // 1/3
        TuningMultiplierPresets.Values[TuningMultiplierPresets.TwoTimesIndex],
    ];

    private static int Main(string[] args)
    {
        try
        {
            var pakPath = ResolvePakPath(args);
            if (pakPath is null)
            {
                Console.Error.WriteLine(
                    "No pak path. Pass one as argument, or configure an edition in the app (uses baseline if present).");
                return 2;
            }

            if (!File.Exists(pakPath))
            {
                Console.Error.WriteLine($"Pak not found: {pakPath}");
                return 2;
            }

            Console.WriteLine($"Pak smoke (Level B) — {pakPath}");
            Console.WriteLine("Structural rewrite checks only (no game launch).");
            Console.WriteLine();

            var sw = Stopwatch.StartNew();
            var failures = new List<string>();
            var filesOk = 0;
            var filesTotal = 0;

            using var archive = ZipFile.OpenRead(pakPath);

            RunGroup(
                "trucks",
                archive.Entries.Where(e => IsTruckXml(e.FullName)),
                (text, name) => SmokeTruck(text, name, failures),
                ref filesOk,
                ref filesTotal,
                failures);

            RunGroup(
                "engines",
                archive.Entries.Where(e => IsClassXml(e.FullName, "engines")),
                (text, name) => SmokeScaled(
                    name,
                    failures,
                    m => EngineService.ApplyMultipliersToTextForTests(text, m, m, m, m),
                    "engine"),
                ref filesOk,
                ref filesTotal,
                failures);

            RunGroup(
                "gearboxes",
                archive.Entries.Where(e => IsClassXml(e.FullName, "gearboxes")),
                (text, name) => SmokeScaled(
                    name,
                    failures,
                    m => GearboxService.ApplyMultipliersToTextForTests(text, m, m, m),
                    "gearbox"),
                ref filesOk,
                ref filesTotal,
                failures);

            RunGroup(
                "suspensions",
                archive.Entries.Where(e => IsClassXml(e.FullName, "suspensions")),
                (text, name) => SmokeScaled(
                    name,
                    failures,
                    m => SuspensionService.ApplyMultipliersToTextForTests(text, m, m, m, m),
                    "suspension"),
                ref filesOk,
                ref filesTotal,
                failures);

            RunGroup(
                "wheels",
                archive.Entries.Where(e => IsClassXml(e.FullName, "wheels")),
                (text, name) => SmokeScaled(
                    name,
                    failures,
                    m => TireService.ApplyMultipliersToTextForTests(text, m, m, m),
                    "tires"),
                ref filesOk,
                ref filesTotal,
                failures);

            RunGroup(
                "winches",
                archive.Entries.Where(e => IsClassXml(e.FullName, "winches")),
                (text, name) => SmokeScaled(
                    name,
                    failures,
                    m => WinchService.ApplyMultipliersToTextForTests(text, m, m),
                    "winch"),
                ref filesOk,
                ref filesTotal,
                failures);

            RunGroup(
                "addons",
                archive.Entries.Where(e => AddonCapacityService.IsAddonXmlEntry(e.FullName)),
                (text, name) => SmokeAddon(text, name, failures),
                ref filesOk,
                ref filesTotal,
                failures);

            sw.Stop();
            Console.WriteLine();
            Console.WriteLine(new string('─', 60));
            Console.WriteLine(
                $"Done in {sw.Elapsed.TotalSeconds:0.0}s — files {filesOk}/{filesTotal} OK, failures={failures.Count}");

            if (failures.Count == 0)
            {
                Console.WriteLine("All structural smoke checks passed.");
                return 0;
            }

            Console.WriteLine();
            Console.WriteLine("Failures:");
            foreach (var failure in failures.Take(40))
            {
                Console.WriteLine($"  • {failure}");
            }

            if (failures.Count > 40)
            {
                Console.WriteLine($"  … and {failures.Count - 40} more");
            }

            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static string? ResolvePakPath(string[] args)
    {
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            return Path.GetFullPath(args[0]);
        }

        var workspace = WorkspaceConfigStore.TryGetActiveWorkspace();
        if (workspace is null)
        {
            return null;
        }

        if (workspace.BaselineExists)
        {
            Console.WriteLine($"Using baseline: {workspace.BaselinePath}");
            return workspace.BaselinePath;
        }

        Console.WriteLine($"Using working pak (no baseline found): {workspace.WorkingPakPath}");
        return workspace.WorkingPakPath;
    }

    private static void RunGroup(
        string label,
        IEnumerable<ZipArchiveEntry> entries,
        Func<string, string, bool> smoke,
        ref int filesOk,
        ref int filesTotal,
        List<string> failures)
    {
        var list = entries
            .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var groupOk = 0;
        for (var i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            var name = ShortName(entry.FullName);
            ReportProgress(label, i + 1, list.Count, name);
            filesTotal++;
            var before = failures.Count;
            if (smoke(ReadEntry(entry), name) && failures.Count == before)
            {
                groupOk++;
                filesOk++;
            }
        }

        if (list.Count > 0)
        {
            Console.WriteLine($"  {label}: {groupOk}/{list.Count} files OK");
        }
        else
        {
            Console.WriteLine($"  {label}: (none)");
        }
    }

    private static bool SmokeTruck(string text, string name, List<string> failures)
    {
        try
        {
            foreach (var m in Multipliers)
            {
                var scaled = TruckTuningService.ApplyGlobalMultipliersToTextForTests(
                    text,
                    fuelMultiplier: m,
                    frontSteerMode: TruckFrontSteerGlobalMode.Maximum,
                    responsivenessMultiplier: m,
                    priceMultiplier: m,
                    massMultiplier: Math.Min(m, 2),
                    rearSteerMode: TruckRearSteerGlobalMode.Minimum);
                XmlRewriteSafety.EnsureSafe(scaled, $"{name} globals m={m:0.###}");
            }

            var axles = TruckSteerXml.ParseSteerAxles(text, text);
            var injectable = axles.FirstOrDefault(a => !a.HadSteerInBaseline);
            if (injectable is not null)
            {
                injectable.Angle = -40;
                var steered = TruckSteerXml.ApplySteerAxles(text, axles);
                XmlRewriteSafety.EnsureSafe(steered, $"{name} rear inject -40");
                if (steered.Contains("/ SteeringAngle", StringComparison.Ordinal))
                {
                    failures.Add($"{name}: still contains '/ SteeringAngle' after inject");
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            failures.Add($"{name}: {ex.Message}");
            return false;
        }
    }

    private static bool SmokeScaled(
        string name,
        List<string> failures,
        Func<double, string> apply,
        string label)
    {
        try
        {
            foreach (var m in Multipliers)
            {
                var updated = apply(m);
                XmlRewriteSafety.EnsureSafe(updated, $"{name} {label} m={m:0.###}");
            }

            return true;
        }
        catch (Exception ex)
        {
            failures.Add($"{name}: {ex.Message}");
            return false;
        }
    }

    private static bool SmokeAddon(string text, string name, List<string> failures)
    {
        try
        {
            if (AddonCapacityService.HasAnyCapacityAttribute(text))
            {
                foreach (var m in Multipliers)
                {
                    var updated = AddonCapacityService.ApplyGlobalMultipliersToTextForTests(
                        text, m, m, m, m, m);
                    XmlRewriteSafety.EnsureSafe(updated, $"{name} addon m={m:0.###}");
                }
            }

            if (text.Contains("ControlledIK", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var m in Multipliers)
                {
                    var crane = CraneService.ApplyMultipliersToTextForTests(text, m, m);
                    XmlRewriteSafety.EnsureSafe(crane, $"{name} crane m={m:0.###}");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            failures.Add($"{name}: {ex.Message}");
            return false;
        }
    }

    private static void ReportProgress(string group, int current, int total, string item)
    {
        var pct = total == 0 ? 100 : (int)Math.Round(100.0 * current / total);
        var filled = Math.Clamp(pct / 5, 0, 20);
        var bar = new string('█', filled) + new string('░', 20 - filled);
        var line = $"\r[{bar}] {group} {current}/{total} ({pct,3}%)  {Truncate(item, 42),-42}";
        Console.Write(line);
        if (current >= total)
        {
            Console.WriteLine();
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";

    private static string ShortName(string entryPath)
    {
        var n = PakEntryLocator.NormalizeEntryPath(entryPath);
        var i = n.LastIndexOf('/');
        return i >= 0 ? n[(i + 1)..] : n;
    }

    private static bool IsTruckXml(string entryPath)
    {
        // Same rule as truck services: .../classes/trucks/name.xml (not nested folders).
        var n = PakEntryLocator.NormalizeEntryPath(entryPath);
        if (!n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        const string marker = "/classes/trucks/";
        var idx = n.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return false;
        }

        var rest = n[(idx + marker.Length)..];
        return rest.Length > 0 && !rest.Contains('/');
    }

    private static bool IsClassXml(string entryPath, string folder)
    {
        var n = PakEntryLocator.NormalizeEntryPath(entryPath);
        return n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            && n.Contains($"/classes/{folder}/", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadEntry(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
