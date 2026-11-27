using System;
using System.Threading;
using System.Threading.Tasks;
using CCRankViewer.Lodestone;

internal static class StandingsChecks
{
    private static string Row(ulong id, string name = "Example Name", string world = "Spriggan", string tier = "Omega") => $"""
        <div class="ranking_set" data-href="/lodestone/character/{id}/">
        <div class="name"><div><h3>{name}</h3><span class="world"><i></i>{world} [Chaos]</span></div></div>
        <div class="tier"><img alt="{tier}" data-tooltip="{tier}"></div></div>
        """;

    private static string Page(string rows, string season = "22") => $"""
        <h2><img alt="Season {season}"></h2><p class="summarytime na"><span>11/25/2026</span> 10:00 p.m. (PST)</p>
        <div class="cc-ranking__table js--wolvesden__ranking">{rows}</div>
        """;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine($"PASS: {message}");
    }

    internal static async Task RunAsync()
    {
        var parsed = SeasonStandings.Parse(Page(Row(123, "O&#39;name Example")), true);
        Check(parsed.Entries.Count == 1 && parsed.Entries[0].Name == "O'name Example"
            && parsed.Entries[0].Tier == "Omega" && parsed.Season == "Season 22" && parsed.Updated!.Contains("11/25/2026"),
            "Standings parser reads identity, tier, season, and published snapshot");
        Check(SeasonStandings.Parse(Row(123), false).Entries[0].CharacterId == 123,
            "Standings continuation rows parse without full-page metadata");
        Check(SeasonStandings.RegionGroup("Light") == "Chaos" && SeasonStandings.RegionGroup("Dynamis") == "Aether"
            && SeasonStandings.RegionGroup("Meteor") == "Elemental" && SeasonStandings.RegionGroup("Materia") == "Materia"
            && SeasonStandings.RegionGroup("Unknown") == null, "Home data centers map to supported regional standings");

        var restricted = new RankResult(RankStatus.Restricted, Detail: "Achievements restricted.");
        var calls = 0;
        var lookup = new StandingsLookup((uri, token) =>
        {
            calls++;
            return Task.FromResult(new LodestonePage(200, Page(Row(123) + Row(456, "Second Person"))));
        }, TimeSpan.Zero);
        var rank = await lookup.FindAsync(123, "Example Name", "Spriggan", "Chaos", restricted, CancellationToken.None);
        Check(rank.Status == RankStatus.Standings && rank.Tier == "Omega" && rank.StatusLabel == "Season standings"
            && rank.Detail!.Contains("Season 22") && rank.Detail.Contains("not a best season finish"),
            "Restricted achievements can fall back to clearly labeled seasonal standings");
        await lookup.FindAsync(456, "Second Person", "Spriggan", "Chaos", restricted, CancellationToken.None);
        Check(calls == 1, "Downloaded standings page reused for another match participant");

        var confirmed = new RankResult(RankStatus.Confirmed, "Crystal", 3051);
        Check(await lookup.FindAsync(123, "Example Name", "Spriggan", "Chaos", confirmed, CancellationToken.None) == confirmed
            && calls == 1, "Confirmed achievement rank is not replaced by seasonal standings");
        Check((await lookup.FindAsync(123, "Wrong Name", "Spriggan", "Chaos", restricted, CancellationToken.None)).Status == RankStatus.Restricted,
            "Matching ID with a different name is not assigned a rank");
        Check((await lookup.FindAsync(123, "Example Name", "Ragnarok", "Chaos", restricted, CancellationToken.None)).Status == RankStatus.Restricted,
            "Matching ID with a different home world is not assigned a rank");

        var pageCalls = 0;
        var paginated = new StandingsLookup((uri, token) =>
        {
            pageCalls++;
            return Task.FromResult(uri.Query.Contains("page=1")
                ? new LodestonePage(200, Page(Row(999)), HasMore: true)
                : new LodestonePage(200, Row(123)));
        }, TimeSpan.Zero);
        Check((await paginated.FindAsync(123, "Example Name", "Spriggan", "Chaos", restricted, CancellationToken.None)).Tier == "Omega"
            && pageCalls == 2, "Fallback follows the regional ladder to a later page");

        var lower = new StandingsLookup((uri, token) => Task.FromResult(new LodestonePage(200,
            uri.Query.Contains("rank_type=1&") ? Page(Row(123, tier: "Bronze")) : Page(""))), TimeSpan.Zero);
        Check((await lower.FindAsync(123, "Example Name", "Spriggan", "Chaos", restricted, CancellationToken.None)).Tier == "Bronze",
            "Fallback checks limited individual tier lists after the overall ladder");

        var absent = new StandingsLookup((uri, token) => Task.FromResult(new LodestonePage(200, Page(Row(999)))), TimeSpan.Zero);
        Check((await absent.FindAsync(123, "Example Name", "Spriggan", "Chaos", restricted, CancellationToken.None)).RankLabel == "?",
            "Missing standings entry does not turn private achievements into Unranked");
        var unranked = new RankResult(RankStatus.NoneFound);
        Check((await absent.FindAsync(123, "Example Name", "Spriggan", "Chaos", unranked, CancellationToken.None)).RankLabel == "Unranked",
            "Missing standings entry preserves a public no-achievement Unranked result");

        var failedCalls = 0;
        var failed = new StandingsLookup((uri, token) =>
        {
            failedCalls++;
            return Task.FromResult(new LodestonePage(429, "", "Rate limited"));
        }, TimeSpan.Zero);
        Check((await failed.FindAsync(123, "Example Name", "Spriggan", "Chaos", restricted, CancellationToken.None)).Status == RankStatus.Restricted,
            "Failed standings request preserves the achievement outcome");
        await failed.FindAsync(456, "Second Person", "Spriggan", "Chaos", restricted, CancellationToken.None);
        Check(failedCalls == 1, "Failed standings page not repeatedly requested for every player");

        var invalid = new StandingsLookup((uri, token) => Task.FromResult(new LodestonePage(200, "Maintenance")), TimeSpan.Zero);
        Check((await invalid.FindAsync(123, "Example Name", "Spriggan", "Chaos", restricted, CancellationToken.None)).Status == RankStatus.Restricted,
            "Unexpected standings HTML cannot produce a rank");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var canceledCorrectly = false;
        try { await lookup.FindAsync(123, "Example Name", "Spriggan", "Chaos", restricted, canceled.Token); }
        catch (OperationCanceledException) { canceledCorrectly = true; }
        Check(canceledCorrectly && calls == 1, "Leaving a match cancels even a cached standings lookup");
    }
}
