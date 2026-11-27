using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace CCRankViewer.Lodestone;

internal enum RankStatus { Confirmed, Standings, NoneFound, Restricted, Unavailable }

internal sealed record RankResult(RankStatus Status, string? Tier = null, uint? AchievementId = null, string? Detail = null)
{
    internal string RankLabel => Status is RankStatus.Confirmed or RankStatus.Standings ? Tier! : Status == RankStatus.NoneFound ? "Unranked" : "?";
    internal string StatusLabel => Status switch
    {
        RankStatus.Confirmed => "Achievement confirmed",
        RankStatus.Standings => "Season standings",
        RankStatus.NoneFound => "No rank achievement",
        RankStatus.Restricted => "Private / restricted",
        _ => "Achievements unavailable",
    };
}

internal static class RankingAchievements
{
    private const RegexOptions Options = RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
    private static readonly IReadOnlyDictionary<uint, (string Tier, string Achievement, int Order)> Tiers = new Dictionary<uint, (string, string, int)>
    {
        [3046] = ("Bronze", "Solid Bronze", 1), [3047] = ("Silver", "Solid Silver", 2),
        [3048] = ("Gold", "Solid Gold", 3), [3049] = ("Platinum", "Solid Platinum", 4),
        [3050] = ("Diamond", "Solid Diamond", 5), [3051] = ("Crystal", "Solid Crystal", 6),
        [3750] = ("Omega", "Omega Material", 7), [3751] = ("Ultima", "Ultima Material", 8),
    };

    internal static Uri PageUri(ulong characterId) => new($"https://na.finalfantasyxiv.com/lodestone/character/{characterId}/achievement/category/8/");

    internal static RankResult Parse(string html, ulong characterId, int httpStatus = 200)
    {
        // A generic 403 or bot challenge does not establish that achievements are private.
        if ((httpStatus == 200 || httpStatus == 403)
            && Regex.IsMatch(html, "class=['\"]error__heading['\"][^>]*>\\s*Access Restricted\\s*</h1>", Options, Timeout)
            && html.Contains("You do not have permission to view this page.", StringComparison.Ordinal))
            return new(RankStatus.Restricted, Detail: "Lodestone restricts access to these achievements. No rank can be confirmed.");
        if (httpStatus != 200)
            return new(RankStatus.Unavailable, Detail: $"Lodestone returned HTTP {httpStatus}; no rank inferred.");

        var expectedPath = $"/lodestone/character/{characterId}/achievement/category/8/";
        if (!html.Contains(expectedPath, StringComparison.Ordinal)
            || !Regex.IsMatch(html, "id=['\"]anchor_achievement['\"]", Options, Timeout))
            return Unexpected();

        var pages = Regex.Match(html, "Page\\s+[0-9]+\\s+of\\s+(?<total>[0-9]+)", Options, Timeout);
        if (pages.Success && int.TryParse(pages.Groups["total"].Value, out var total) && total > 1)
            return new(RankStatus.Unavailable, Detail: "Ranking achievements span multiple pages; no highest tier inferred.");

        var list = Regex.Matches(html, "<ul\\b(?<attributes>[^>]*)>(?<body>.*?)</ul\\s*>", Options, Timeout)
            .Cast<Match>().FirstOrDefault(m => HasClass(m.Groups["attributes"].Value, "js--achievements"));
        if (list == null)
            return Unexpected();

        uint highest = 0;
        var entries = Regex.Matches(list.Groups["body"].Value, "<li\\b(?<attributes>[^>]*)>(?<body>.*?)</li\\s*>", Options, Timeout);
        if (entries.Count == 0 && !string.IsNullOrWhiteSpace(list.Groups["body"].Value))
            return Unexpected();
        foreach (Match entry in entries)
        {
            var anchor = Regex.Match(entry.Groups["body"].Value, "<a\\b(?<attributes>[^>]*)>", Options, Timeout);
            if (!anchor.Success || !HasClass(anchor.Groups["attributes"].Value, "entry__achievement"))
                return Unexpected();
            var path = Attribute(anchor.Groups["attributes"].Value, "href");
            var idMatch = Regex.Match(path, $"^/lodestone/character/{characterId}/achievement/detail/(?<id>[0-9]+)/$", Options, Timeout);
            if (!idMatch.Success || !uint.TryParse(idMatch.Groups["id"].Value, out var id))
                return Unexpected();

            var achieved = Attribute(entry.Groups["attributes"].Value, "data-achieved");
            var complete = HasClass(anchor.Groups["attributes"].Value, "entry__achievement--complete");
            if (achieved is not ("0" or "1") || complete != (achieved == "1"))
                return Unexpected();
            if (achieved == "1" && Tiers.TryGetValue(id, out var candidate)
                && (highest == 0 || candidate.Order > Tiers[highest].Order))
                highest = id;
        }

        if (highest == 0)
            return new(RankStatus.NoneFound, Detail: "Public achievements contain no earned Bronze through Ultima season-finish achievement, so this viewer displays Unranked. Current in-game rank is not checked.");

        var rank = Tiers[highest];
        return new(RankStatus.Confirmed, rank.Tier, highest,
            $"{rank.Achievement} (#{highest}) earned. Highest tier confirmed by public achievements; this is a season finish, not current rank.");
    }

    private static RankResult Unexpected() => new(RankStatus.Unavailable,
        Detail: "Lodestone returned an unexpected achievement page. No rank inferred.");

    private static bool HasClass(string attributes, string value) => Attribute(attributes, "class")
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Contains(value);

    private static string Attribute(string attributes, string name)
    {
        var match = Regex.Match(attributes, $"(?:^|\\s){name}\\s*=\\s*(['\"])(?<value>.*?)\\1", Options, Timeout);
        return WebUtility.HtmlDecode(match.Groups["value"].Value);
    }
}
