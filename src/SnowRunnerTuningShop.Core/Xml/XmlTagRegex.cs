namespace SnowRunnerTuningShop.Core.Xml;

/// <summary>
/// Shared notes for SnowRunner XML tag regexes.
/// </summary>
/// <remarks>
/// Attribute runs must not accept '&lt;' (use <c>[^&lt;&gt;]</c> / <c>[^&lt;&gt;/]</c>), otherwise a
/// truncated opening tag can backtrack into the next element (e.g. mash friction attrs onto
/// <c>GameData</c> — issue #6). Prefer requiring <c>/&gt;</c> for known empty elements
/// (see <c>WheelFriction</c>).
/// </remarks>
public static class XmlTagRegex
{
    /// <summary>Attrs for empty-or-open tags that may self-close (<c>/&gt;</c>). No '/' or '&lt;' in attrs.</summary>
    public const string AttrsMaybeSelfClose = @"[^<>/]*?";

    /// <summary>Attrs for opening tags only (children follow). No '&lt;'.</summary>
    public const string AttrsOpenOnly = @"[^<>]*";
}
