namespace SnowRunnerTuningShop.Core.Updates;

/// <summary>
/// Minimal SemVer (MAJOR.MINOR.PATCH[-pre.N]) for app release tags and update checks.
/// </summary>
public readonly struct AppSemVersion : IComparable<AppSemVersion>, IEquatable<AppSemVersion>
{
    public AppSemVersion(int major, int minor, int patch, string? preRelease = null)
    {
        if (major < 0 || minor < 0 || patch < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(major), "Version components must be non-negative.");
        }

        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = NormalizePreRelease(preRelease);
    }

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }

    /// <summary>Prerelease label without leading hyphen (e.g. <c>beta.1</c>), or null for stable.</summary>
    public string? PreRelease { get; }

    public bool IsPrerelease => PreRelease is not null;

    public static bool TryParse(string? value, out AppSemVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        string? pre = null;
        var dash = text.IndexOf('-');
        if (dash >= 0)
        {
            pre = text[(dash + 1)..];
            text = text[..dash];
            if (string.IsNullOrWhiteSpace(pre))
            {
                return false;
            }
        }

        var parts = text.Split('.');
        if (parts.Length is < 2 or > 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var major)
            || !int.TryParse(parts[1], out var minor))
        {
            return false;
        }

        var patch = 0;
        if (parts.Length == 3 && !int.TryParse(parts[2], out patch))
        {
            return false;
        }

        if (major < 0 || minor < 0 || patch < 0)
        {
            return false;
        }

        version = new AppSemVersion(major, minor, patch, pre);
        return true;
    }

    public int CompareTo(AppSemVersion other)
    {
        var core = Major.CompareTo(other.Major);
        if (core != 0)
        {
            return core;
        }

        core = Minor.CompareTo(other.Minor);
        if (core != 0)
        {
            return core;
        }

        core = Patch.CompareTo(other.Patch);
        if (core != 0)
        {
            return core;
        }

        // Stable (no prerelease) has higher precedence than any prerelease.
        if (PreRelease is null && other.PreRelease is null)
        {
            return 0;
        }

        if (PreRelease is null)
        {
            return 1;
        }

        if (other.PreRelease is null)
        {
            return -1;
        }

        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    public bool Equals(AppSemVersion other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is AppSemVersion other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Major, Minor, Patch, PreRelease, StringComparer.OrdinalIgnoreCase);

    public override string ToString()
    {
        var core = $"{Major}.{Minor}.{Patch}";
        return PreRelease is null ? core : $"{core}-{PreRelease}";
    }

    public static bool operator <(AppSemVersion left, AppSemVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(AppSemVersion left, AppSemVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(AppSemVersion left, AppSemVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(AppSemVersion left, AppSemVersion right) => left.CompareTo(right) >= 0;
    public static bool operator ==(AppSemVersion left, AppSemVersion right) => left.Equals(right);
    public static bool operator !=(AppSemVersion left, AppSemVersion right) => !left.Equals(right);

    private static string? NormalizePreRelease(string? preRelease)
    {
        if (string.IsNullOrWhiteSpace(preRelease))
        {
            return null;
        }

        var text = preRelease.Trim();
        if (text.StartsWith('-'))
        {
            text = text[1..];
        }

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static int ComparePreRelease(string left, string right)
    {
        var a = left.Split('.');
        var b = right.Split('.');
        var n = Math.Max(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            if (i >= a.Length)
            {
                return -1;
            }

            if (i >= b.Length)
            {
                return 1;
            }

            var leftNumeric = int.TryParse(a[i], out var leftNumber);
            var rightNumeric = int.TryParse(b[i], out var rightNumber);
            if (leftNumeric && rightNumeric)
            {
                var cmp = leftNumber.CompareTo(rightNumber);
                if (cmp != 0)
                {
                    return cmp;
                }

                continue;
            }

            if (leftNumeric)
            {
                return -1;
            }

            if (rightNumeric)
            {
                return 1;
            }

            var textCmp = string.Compare(a[i], b[i], StringComparison.OrdinalIgnoreCase);
            if (textCmp != 0)
            {
                return textCmp;
            }
        }

        return 0;
    }
}
