using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CCRankViewer.Lodestone;

internal static class CharacterSearchChecks
{
    private static string Entry(ulong id, string name, string world) => $"""
        <a class='entry__link' href='/lodestone/character/{id}/'>
        <p class="entry__name">{name}</p>
        <p class='entry__world'><i data-tooltip="Home World"></i>{world} [Chaos]</p></a>
        """;

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Console.WriteLine($"PASS: {description}");
    }

    internal static async Task RunAsync()
    {
        var correct = Entry(123, "O&#39;name Example", "Spriggan");
        var result = CharacterSearch.Parse(correct, "O'name Example", "Spriggan");
        Check(result.Status == SearchStatus.Found && result.CharacterId == 123, "Exact name/world resolves HTML entities");
        Check(CharacterSearch.Parse(Entry(456, "Other Name", "Spriggan") + correct, "O'name Example", "Spriggan").CharacterId == 123,
            "Partial name result is not selected");
        Check(CharacterSearch.Parse(correct, "O'name Example", "Ragnarok").Status == SearchStatus.NotFound,
            "Same name on another home world is not selected");
        Check(CharacterSearch.Parse(correct + Entry(789, "O&#39;name Example", "Spriggan"), "O'name Example", "Spriggan").Status == SearchStatus.NeedsReview,
            "Ambiguous exact matches are not guessed");
        Check(CharacterSearch.Parse(correct + "Page 1 of 2", "O'name Example", "Spriggan").Status == SearchStatus.NeedsReview,
            "Incomplete search pages are not accepted");
        Check(CharacterSearch.Parse("Your search yielded no results.", "Example", "Spriggan").Status == SearchStatus.NotFound,
            "Explicit zero results handled");
        Check(CharacterSearch.Parse("<html>Maintenance</html>", "Example", "Spriggan").Status == SearchStatus.Unavailable,
            "Unexpected pages do not become missing characters");
        Check(CharacterSearch.Parse(correct.Replace("entry__link", "entry__ranking__face"), "O'name Example", "Spriggan").Status == SearchStatus.Unavailable,
            "Sidebar character links ignored");
        Check(CharacterSearch.Parse(correct + correct, "O'name Example", "Spriggan").CharacterId == 123,
            "Duplicate links to the same identity deduplicated");
        Check(CharacterSearch.SearchUri("O'name & Example", "Spriggan").Query.Contains("%26"), "Search query encodes reserved characters");

        using var handler = new StubHandler(HttpStatusCode.OK, correct);
        using var http = new HttpClient(handler);
        var client = new CharacterSearchClient(http);
        Check((await client.FindAsync("O'name Example", "Spriggan", CancellationToken.None)).CharacterId == 123,
            "HTTP result reaches the identity parser");
        handler.Code = HttpStatusCode.TooManyRequests;
        Check((await client.FindAsync("Example", "Spriggan", CancellationToken.None)).Status == SearchStatus.Unavailable,
            "Rate limiting produces an unavailable result");
        var count = handler.Calls;
        await client.FindAsync("Example", "Spriggan", CancellationToken.None);
        Check(handler.Calls == count, "Rate-limit cooldown suppresses subsequent requests");

        using var forbiddenHandler = new StubHandler(HttpStatusCode.Forbidden, "Access restricted");
        using var forbiddenHttp = new HttpClient(forbiddenHandler);
        Check((await new CharacterSearchClient(forbiddenHttp).FindAsync("Example", "Spriggan", CancellationToken.None)).Status == SearchStatus.Unavailable,
            "Forbidden responses are not interpreted as private achievements or an unranked player");
    }

    private sealed class StubHandler(HttpStatusCode code, string html) : HttpMessageHandler
    {
        internal HttpStatusCode Code = code;
        internal int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(new HttpResponseMessage(Code) { Content = new StringContent(html) });
        }
    }
}
