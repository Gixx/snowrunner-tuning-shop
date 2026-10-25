using System.Text;
using SnowRunnerTuningShop.Core.Strings;

namespace SnowRunnerTuningShop.Core.Cargo;

/// <summary>
/// Maps InstallSlot <c>CargoType</c> (e.g. <c>CargoLogsMedium</c>) to game string keys
/// (<c>UI_CARGO_LOGS_MEDIUM_NAME</c>) used in the truck store / cargo UI.
/// </summary>
public static class CargoTypeDisplayNames
{
    /// <summary>
    /// Known CargoType bodies (without the <c>Cargo</c> prefix) that do not follow the
    /// default CamelCase → SCREAMING_SNAKE rule, or that omit the <c>_NAME</c> suffix.
    /// </summary>
    private static readonly Dictionary<string, string[]> BodyKeyBodies =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["LogsMedium_Burnt"] = ["BURNT_LOGS"],
            ["MetalJunk"] = ["JUNK_CLOSED"],
            ["MetalJunkOpen"] = ["JUNK_OPEN"],
            ["BigDrill"] = ["BIG_DRILLS"],
            ["ReeferContainer"] = ["CONTAINER_REEFER"],
            ["Radioactive"] = ["RADIOCTIVE"], // vanilla typo in strings_*.str
            ["IronRoad"] = ["IRON_ROAD", "RAILWAY"],
            ["Boiler"] = ["BOILER"],
            ["BA20Add"] = ["BA20"],
            ["Wing1"] = ["WING", "BIG_WING"],
            ["Wing2"] = ["WING", "BIG_WING"],
            ["WoodenPlanks2"] = ["WOODEN_PLANKS"],
            ["WoodenPlanks4"] = ["WOODEN_PLANKS_LONG", "WOODEN_PLANKS"],
            ["PotatoUS"] = ["POTATO"],
            ["WaterPipe"] = ["PIPE_WATER"],
            ["Trash"] = ["JUNK_OPEN"],
            ["Forcklift"] = ["FORKLIFT_CARAVAN_CONTAINER_2"],
            ["CableCar1"] = ["CABLE_CAR"],
            ["SensoryBuoyAdd2"] = ["SENSORY_BUOY"],
            ["SensoryBuoyAdd3"] = ["SENSORY_BUOY"],
            ["SensoryBuoyAdd4"] = ["SENSORY_BUOY"],
            ["NPPPump"] = ["NPP_PUMP"],
        };

    public static string ResolveDisplayName(
        IReadOnlyDictionary<string, string> strings,
        string? cargoType,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(cargoType))
        {
            return fallback;
        }

        foreach (var key in EnumerateCandidateKeys(cargoType))
        {
            var resolved = GameStringsReader.Resolve(strings, key, "");
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        return fallback;
    }

    public static IEnumerable<string> EnumerateCandidateKeys(string cargoType)
    {
        var body = cargoType.StartsWith("Cargo", StringComparison.OrdinalIgnoreCase)
            ? cargoType["Cargo".Length..]
            : cargoType;
        if (body.Length == 0)
        {
            yield break;
        }

        if (BodyKeyBodies.TryGetValue(body, out var aliases))
        {
            foreach (var alias in aliases)
            {
                foreach (var key in KeysForBody(alias))
                {
                    yield return key;
                }
            }
        }

        foreach (var key in KeysForBody(ToScreamingSnake(body)))
        {
            yield return key;
        }

        // CargoBA20Add / CargoSensoryBuoyAdd2 → strip trailing Add / AddN
        if (body.EndsWith("Add", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var key in KeysForBody(ToScreamingSnake(body[..^3])))
            {
                yield return key;
            }
        }
        else
        {
            var addDigits = System.Text.RegularExpressions.Regex.Match(
                body,
                @"^(?<stem>.+?)Add(?<n>\d+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (addDigits.Success)
            {
                foreach (var key in KeysForBody(ToScreamingSnake(addDigits.Groups["stem"].Value)))
                {
                    yield return key;
                }
            }
        }
    }

    private static IEnumerable<string> KeysForBody(string screamingSnakeBody)
    {
        if (string.IsNullOrWhiteSpace(screamingSnakeBody))
        {
            yield break;
        }

        yield return $"UI_CARGO_{screamingSnakeBody}_NAME";
        yield return $"UI_CARGO_{screamingSnakeBody}";
    }

    /// <summary>
    /// <c>LogsMedium</c> → <c>LOGS_MEDIUM</c>; <c>Bags2</c> → <c>BAGS_2</c>;
    /// <c>BA20</c> → <c>BA20</c> (no underscore between uppercase and digits).
    /// </summary>
    internal static string ToScreamingSnake(string camelOrMixed)
    {
        var sb = new StringBuilder(camelOrMixed.Length + 8);
        for (var i = 0; i < camelOrMixed.Length; i++)
        {
            var c = camelOrMixed[i];
            if (c == '_')
            {
                if (sb.Length > 0 && sb[^1] != '_')
                {
                    sb.Append('_');
                }

                continue;
            }

            var prev = i > 0 ? camelOrMixed[i - 1] : '\0';
            if (i > 0 && char.IsUpper(c) && char.IsLower(prev))
            {
                sb.Append('_');
            }
            else if (i > 0 && char.IsDigit(c) && char.IsLower(prev))
            {
                sb.Append('_');
            }
            else if (i > 0 && char.IsUpper(c) && char.IsDigit(prev))
            {
                sb.Append('_');
            }

            sb.Append(char.ToUpperInvariant(c));
        }

        return sb.ToString();
    }
}
