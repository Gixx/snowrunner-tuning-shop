using System.Text.RegularExpressions;

namespace SnowRunnerTuningShop.Vehicles;

public static class VehicleBasedOnFormatter
{
    private static readonly Regex ExternalLinkRegex = new(
        @"^\[(?<url>https?://[^\s\]]+)(?:\s+(?<label>[^\]]+))?\]$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex WikipediaLinkRegex = new(
        @"^\[\[:(?:wikipedia:)?(?<article>[^\|\]]+)(?:\|(?<label>[^\]]+))?\]\]?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex IncompleteWikipediaRegex = new(
        @"^\[\[:(?:wikipedia:)?(?<article>[^\|\]]+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public sealed record ParsedBasedOn(string DisplayText, string? Url);

    public static ParsedBasedOn? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        var external = ExternalLinkRegex.Match(trimmed);
        if (external.Success)
        {
            var url = external.Groups["url"].Value;
            var label = external.Groups["label"].Value.Trim();
            return new ParsedBasedOn(string.IsNullOrWhiteSpace(label) ? url : label, url);
        }

        var wikipedia = WikipediaLinkRegex.Match(trimmed);
        if (wikipedia.Success)
        {
            var article = wikipedia.Groups["article"].Value.Trim();
            var label = wikipedia.Groups["label"].Value.Trim();
            return new ParsedBasedOn(
                string.IsNullOrWhiteSpace(label) ? FormatWikiArticleTitle(article) : label,
                ToWikipediaUrl(article));
        }

        var incompleteWikipedia = IncompleteWikipediaRegex.Match(trimmed);
        if (incompleteWikipedia.Success)
        {
            var article = incompleteWikipedia.Groups["article"].Value.Trim();
            return new ParsedBasedOn(FormatWikiArticleTitle(article), ToWikipediaUrl(article));
        }

        return new ParsedBasedOn(trimmed, null);
    }

    private static string FormatWikiArticleTitle(string article) =>
        article.TrimEnd(']', '|').Replace('_', ' ').Trim();

    private static string ToWikipediaUrl(string article)
    {
        var normalized = article.TrimEnd(']', '|').Replace(' ', '_');
        return $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(normalized)}";
    }
}
