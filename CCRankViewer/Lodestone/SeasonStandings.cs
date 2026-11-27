using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace CCRankViewer.Lodestone;

internal sealed record StandingEntry(ulong CharacterId, string Name, string HomeWorld, string Tier);
internal sealed record StandingsPage(IReadOnlyList<StandingEntry> Entries, string? Season, string? Updated);

internal static class SeasonStandings
{
    private const RegexOptions Options = RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
    private static readonly string[] Tiers = ["Bronze", "Silver", "Gold", "Platinum", "Diamond", "Crystal", "Omega", "Ultima"];

    internal static string? RegionGroup(string dataCenter) => dataCenter switch
    {
        "Aether" or "Primal" or "Crystal" or "Dynamis" => "Aether",
        "Chaos" or "Light" => "Chaos",
        "Elemental" or "Gaia" or "Mana" or "Meteor" => "Elemental",
        "Materia" => "Materia",
        _ => null,
    };

    internal static Uri PageUri(string group, string tier, int page) => new(
        $"https://na.finalfantasyxiv.com/lodestone/ranking/crystallineconflict/?dcgroup={Uri.EscapeDataString(group)}&rank_type={tier}&page={page}");

    internal static StandingsPage Parse(string html, bool firstPage)
    {
        var season = Regex.Match(html, "<img\\b[^>]*alt=['\"]Season (?<season>[0-9]+)['\"]", Options, Timeout);
        var summary = Regex.Match(html, "<p\\b[^>]*class=['\"][^'\"]*\\bsummarytime\\b[^'\"]*['\"][^>]*>(?<text>.*?)</p>", Options, Timeout);
        if (firstPage && (!season.Success || !summary.Success || !html.Contains("js--wolvesden__ranking", StringComparison.Ordinal)))
            throw new FormatException("Unexpected standings page.");

        var rows = Regex.Matches(html, "<div\\b(?<attributes>[^>]*\\bclass=['\"]ranking_set['\"][^>]*)>", Options, Timeout);
        if (!firstPage && rows.Count == 0 && !string.IsNullOrWhiteSpace(html))
            throw new FormatException("Unexpected standings continuation.");
        var entries = new List<StandingEntry>();
        for (var i = 0; i < rows.Count; i++)
        {
            var path = Attribute(rows[i].Groups["attributes"].Value, "data-href");
            var id = Regex.Match(path, "^/lodestone/character/(?<id>[0-9]+)/$", Options, Timeout);
            var end = i + 1 < rows.Count ? rows[i + 1].Index : html.Length;
            var row = html[rows[i].Index..end];
            var name = Regex.Match(row, "<h3\\b[^>]*>(?<text>.*?)</h3>", Options, Timeout);
            var world = Regex.Match(row, "<span\\b[^>]*class=['\"]world['\"][^>]*>(?<text>.*?)</span>", Options, Timeout);
            var tier = Regex.Match(row, "<div\\b[^>]*class=['\"]tier['\"][^>]*>\\s*<img\\b(?<attributes>[^>]*)>", Options, Timeout);
            var tierName = Attribute(tier.Groups["attributes"].Value, "alt");
            var worldName = Text(world.Groups["text"].Value).Split('[')[0].Trim();
            if (!id.Success || !ulong.TryParse(id.Groups["id"].Value, out var characterId) || characterId == 0
                || !name.Success || string.IsNullOrWhiteSpace(Text(name.Groups["text"].Value))
                || !world.Success || worldName.Length == 0 || !Tiers.Contains(tierName))
                throw new FormatException("Unexpected standings row.");

            entries.Add(new(characterId, Text(name.Groups["text"].Value), worldName, tierName));
        }
        return new(entries, season.Success ? $"Season {season.Groups["season"].Value}" : null,
            summary.Success ? Text(summary.Groups["text"].Value) : null);
    }

    private static string Attribute(string attributes, string name) => WebUtility.HtmlDecode(Regex.Match(attributes,
        $"(?:^|\\s){name}\\s*=\\s*(['\"])(?<value>.*?)\\1", Options, Timeout).Groups["value"].Value);

    private static string Text(string html) => Regex.Replace(WebUtility.HtmlDecode(
        Regex.Replace(html, "<[^>]+>", " ", Options, Timeout)), "\\s+", " ", Options, Timeout).Trim();
}
