using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CCRankViewer.Lodestone;

internal static class RankingAchievementChecks
{
    private static string Entry(uint id, bool earned, ulong character = 123) => $"""
        <li class="entry" data-achieved="{(earned ? 1 : 0)}">
        <a href="/lodestone/character/{character}/achievement/detail/{id}/" class="entry__achievement{(earned ? " entry__achievement--complete" : "")}">
        <p class="entry__activity__txt">Example achievement</p></a></li>
        """;

    private static string Page(string entries) => $"""
        <a href="/lodestone/character/123/achievement/category/8/">Ranking</a>
        <div id="anchor_achievement"><ul class="js--achievements">{entries}</ul></div>
        """;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine($"PASS: {message}");
    }

    internal static async Task RunAsync()
    {
        var tiers = new[] { "Bronze", "Silver", "Gold", "Platinum", "Diamond", "Crystal", "Omega", "Ultima" };
        var ids = new uint[] { 3046, 3047, 3048, 3049, 3050, 3051, 3750, 3751 };
        for (var i = 0; i < tiers.Length; i++)
        {
            var result = RankingAchievements.Parse(Page(Entry(ids[i], true)), 123);
            Check(result.Status == RankStatus.Confirmed && result.Tier == tiers[i] && result.AchievementId == ids[i],
                $"Earned ranking achievement maps to {tiers[i]}");
        }
        var mixed = RankingAchievements.Parse(Page(Entry(3051, false) + Entry(3046, true) + Entry(3050, true)), 123);
        Check(mixed.Tier == "Diamond", "Highest earned tier chosen; unearned Crystal ignored");
        Check(RankingAchievements.Parse(Page(Entry(3051, true) + Entry(3751, false) + Entry(3750, true)), 123).Tier == "Omega",
            "Earned Omega outranks Crystal; unearned Ultima ignored");
        var ultima = RankingAchievements.Parse(Page(Entry(3751, true) + Entry(3750, true) + Entry(3051, true)), 123);
        Check(ultima.Tier == "Ultima" && ultima.Detail!.Contains("Ultima Material"),
            "Ultima outranks Omega regardless of display order and uses its actual achievement name");
        Check(RankingAchievements.Parse(Page(Entry(3051, false)), 123).Status == RankStatus.NoneFound,
            "Listed but unearned rank does not confirm a tier");
        Check(RankingAchievements.Parse(Page(Entry(3934, true)), 123).Status == RankStatus.NoneFound,
            "Other PvP ranking achievements do not confirm a Solid rank");
        var emptyPublic = RankingAchievements.Parse(Page(""), 123);
        Check(emptyPublic.Status == RankStatus.NoneFound && emptyPublic.RankLabel == "Unranked",
            "Public achievements without an earned tier display Unranked");
        Check(RankingAchievements.Parse(Page(Entry(3051, false)), 123).RankLabel == "Unranked",
            "Only unearned tier achievements also display Unranked");
        Check(RankingAchievements.Parse(Page(Entry(3051, true, 456)), 123).Status == RankStatus.Unavailable,
            "Achievement for a different character rejected");
        Check(RankingAchievements.Parse(Page(Entry(3051, true)).Replace("data-achieved=\"1\"", "data-achieved=\"0\""), 123).Status == RankStatus.Unavailable,
            "Conflicting earned markers rejected");
        Check(RankingAchievements.Parse(Page(Entry(3051, true)) + "Page 1 of 2", 123).Status == RankStatus.Unavailable,
            "Incomplete achievement pages cannot establish highest tier");
        Check(RankingAchievements.Parse("<html>Maintenance</html>", 123).Status == RankStatus.Unavailable
            && RankingAchievements.Parse("<html>Maintenance</html>", 123).RankLabel == "Unknown",
            "Maintenance page never becomes a rank");
        Check(RankingAchievements.Parse(Page("<div>Changed entry format</div>"), 123).Status == RankStatus.Unavailable,
            "Changed list markup does not become an empty achievement result");
        Check(RankingAchievements.Parse(Page(Entry(3051, true)), 123, 403).Status == RankStatus.Unavailable,
            "Generic HTTP 403 is not labeled private");
        const string restricted = "<h1 class=\"error__heading\">Access Restricted</h1><p>You do not have permission to view this page.</p>";
        Check(RankingAchievements.Parse(restricted, 123, 403).Status == RankStatus.Restricted
            && RankingAchievements.Parse(restricted, 123, 403).RankLabel == "Unknown",
            "Explicit Lodestone access restriction is reported separately");

        using var handler = new RankHandler(Page(Entry(3051, true)));
        using var http = new HttpClient(handler);
        var client = new CharacterSearchClient(http);
        Check((await client.ReadRankAsync(123, CancellationToken.None)).Tier == "Crystal"
            && handler.LastUri == RankingAchievements.PageUri(123), "Rank client requests the correct ranking category");
        handler.Status = HttpStatusCode.TooManyRequests;
        Check((await client.ReadRankAsync(123, CancellationToken.None)).Status == RankStatus.Unavailable,
            "Rate-limited achievement request does not infer a rank");
        var count = handler.Calls;
        await client.FindAsync("Example Name", "Spriggan", CancellationToken.None);
        Check(handler.Calls == count, "Achievement rate limit also pauses subsequent character searches");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var canceledCorrectly = false;
        try { await client.ReadRankAsync(123, canceled.Token); }
        catch (OperationCanceledException) { canceledCorrectly = true; }
        Check(canceledCorrectly && handler.Calls == count, "Canceled match does not issue an achievement request");
    }

    private sealed class RankHandler(string html) : HttpMessageHandler
    {
        internal HttpStatusCode Status = HttpStatusCode.OK;
        internal Uri? LastUri;
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(html) });
        }
    }
}
