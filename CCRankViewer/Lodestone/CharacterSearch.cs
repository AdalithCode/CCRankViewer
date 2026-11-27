using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace CCRankViewer.Lodestone;

internal enum SearchStatus { Queued, Searching, Found, NotFound, NeedsReview, Unavailable }

internal sealed record CharacterSearchResult(SearchStatus Status, ulong? CharacterId = null, string? Detail = null)
{
    internal string Label => Status switch
    {
        SearchStatus.Queued => "Queued",
        SearchStatus.Searching => "Finding character...",
        SearchStatus.Found => "Character found",
        SearchStatus.NotFound => "Not found",
        SearchStatus.NeedsReview => "Match needs review",
        _ => "Lookup unavailable",
    };
}

internal static class CharacterSearch
{
    private const RegexOptions Options = RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
    private static readonly Regex Anchors = new("<a\\b(?<attributes>[^>]*)>(?<body>.*?)</a\\s*>", Options, Timeout);
    private static readonly Regex Tags = new("<[^>]+>", Options, Timeout);
    private static readonly Regex Spaces = new("\\s+", Options, Timeout);
    private static readonly Regex CharacterPath = new("^/lodestone/character/(?<id>[0-9]+)/$", Options, Timeout);
    private static readonly Regex Pages = new("Page\\s+[0-9]+\\s+of\\s+(?<total>[0-9]+)", Options, Timeout);

    internal static Uri SearchUri(string name, string homeWorld) => new(
        $"https://na.finalfantasyxiv.com/lodestone/character/?q={Uri.EscapeDataString(name)}&worldname={Uri.EscapeDataString(homeWorld)}");

    internal static CharacterSearchResult Parse(string html, string name, string homeWorld)
    {
        // Only search-result anchors qualify. Sidebars contain unrelated character links.
        var ids = new HashSet<ulong>();
        var hasEntries = false;
        foreach (Match anchor in Anchors.Matches(html))
        {
            var attributes = anchor.Groups["attributes"].Value;
            if (!Attribute(attributes, "class").Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("entry__link"))
                continue;
            hasEntries = true;
            var path = CharacterPath.Match(Attribute(attributes, "href"));
            if (!path.Success)
                return new(SearchStatus.Unavailable, Detail: "Lodestone search format changed.");

            var body = anchor.Groups["body"].Value;
            var candidateName = Paragraph(body, "entry__name");
            var world = Paragraph(body, "entry__world");
            var bracket = world.IndexOf('[');
            if (bracket >= 0)
                world = world[..bracket].Trim();
            if (candidateName.Length == 0 || world.Length == 0)
                return new(SearchStatus.Unavailable, Detail: "Lodestone search format changed.");

            if (string.Equals(candidateName, Normalize(name), StringComparison.OrdinalIgnoreCase)
                && string.Equals(world, Normalize(homeWorld), StringComparison.OrdinalIgnoreCase)
                && ulong.TryParse(path.Groups["id"].Value, out var id) && id != 0)
                ids.Add(id);
        }

        // Refuse to select an identity from an incomplete or ambiguous search.
        var pages = Pages.Match(html);
        if ((pages.Success && int.TryParse(pages.Groups["total"].Value, out var total) && total > 1) || ids.Count > 1)
            return new(SearchStatus.NeedsReview, Detail: "Search is ambiguous or spans multiple pages; no character selected.");
        if (ids.Count == 1)
            return new(SearchStatus.Found, ids.Single(), "Exact character name and home world matched.");
        if (hasEntries || html.Contains("Your search yielded no results.", StringComparison.Ordinal))
            return new(SearchStatus.NotFound, Detail: "No exact name and home-world match. Lodestone may lag behind a rename or transfer.");
        return new(SearchStatus.Unavailable, Detail: "Lodestone returned an unexpected page; no character selected.");
    }

    private static string Attribute(string attributes, string name)
    {
        var match = Regex.Match(attributes, $"(?:^|\\s){name}\\s*=\\s*(['\"])(?<value>.*?)\\1", Options, Timeout);
        return WebUtility.HtmlDecode(match.Groups["value"].Value);
    }

    private static string Paragraph(string html, string className)
    {
        foreach (Match match in Regex.Matches(html, "<p\\b(?<attributes>[^>]*)>(?<body>.*?)</p\\s*>", Options, Timeout))
            if (Attribute(match.Groups["attributes"].Value, "class").Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(className))
                return Normalize(Tags.Replace(match.Groups["body"].Value, ""));
        return "";
    }

    private static string Normalize(string text) => Spaces.Replace(WebUtility.HtmlDecode(text), " ").Trim();
}
