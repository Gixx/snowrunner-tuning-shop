namespace SnowRunnerTuningShop.Core.Pak;

/// <summary>
/// Matches catalog cards to pak XML files by filename ids only — never by localized display names.
/// </summary>
public static class PakFileId
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    public static T? Find<T>(
        IReadOnlyList<T> items,
        Func<T, string> fileIdSelector,
        Func<T, string>? entryPathSelector,
        params string?[] candidates)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(fileIdSelector);
        if (items.Count == 0)
        {
            return default;
        }

        foreach (var candidate in candidates)
        {
            var found = FindOne(items, fileIdSelector, entryPathSelector, candidate);
            if (found is not null)
            {
                return found;
            }
        }

        return default;
    }

    public static T? Find<T>(IReadOnlyList<T> items, Func<T, string> fileIdSelector, params string?[] candidates) =>
        Find(items, fileIdSelector, entryPathSelector: null, candidates);

    private static T? FindOne<T>(
        IReadOnlyList<T> items,
        Func<T, string> fileIdSelector,
        Func<T, string>? entryPathSelector,
        string? candidate)
    {
        var key = Normalize(candidate);
        if (key.Length == 0)
        {
            return default;
        }

        var exact = PickUnique(items.Where(item => Normalize(fileIdSelector(item)) == key), entryPathSelector);
        if (exact is not null)
        {
            return exact;
        }

        if (key.Length >= 3)
        {
            var suffix = items
                .Where(item =>
                {
                    var fileKey = Normalize(fileIdSelector(item));
                    return fileKey.Length > key.Length && fileKey.EndsWith(key, StringComparison.Ordinal);
                })
                .ToArray();
            var uniqueSuffix = PickUnique(suffix, entryPathSelector);
            if (uniqueSuffix is not null)
            {
                return uniqueSuffix;
            }
        }

        var prefixes = items
            .Select(item => (Item: item, FileKey: Normalize(fileIdSelector(item))))
            .Where(pair => pair.FileKey.Length >= 5 && key.StartsWith(pair.FileKey, StringComparison.Ordinal))
            .OrderByDescending(pair => pair.FileKey.Length)
            .ToArray();
        if (prefixes.Length > 0)
        {
            var bestLen = prefixes[0].FileKey.Length;
            var best = prefixes.Where(pair => pair.FileKey.Length == bestLen).Select(pair => pair.Item);
            return PickUnique(best, entryPathSelector);
        }

        return default;
    }

    private static T? PickUnique<T>(IEnumerable<T> matches, Func<T, string>? entryPathSelector)
    {
        var list = matches as IList<T> ?? matches.ToArray();
        if (list.Count == 0)
        {
            return default;
        }

        if (list.Count == 1)
        {
            return list[0];
        }

        if (entryPathSelector is null)
        {
            return list[0];
        }

        return list.FirstOrDefault(item =>
                   !entryPathSelector(item).Contains("/_dlc/", StringComparison.OrdinalIgnoreCase))
               ?? list[0];
    }
}
