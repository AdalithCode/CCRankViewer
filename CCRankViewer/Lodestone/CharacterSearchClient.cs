using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CCRankViewer.Lodestone;

internal sealed class CharacterSearchClient
{
    private readonly HttpClient client;
    private DateTimeOffset cooldownUntil;

    internal CharacterSearchClient(HttpClient client) => this.client = client;

    // Called sequentially by the lookup worker. Never accesses game objects.
    internal async Task<CharacterSearchResult> FindAsync(string name, string homeWorld, CancellationToken cancellationToken)
    {
        var page = await ReadPageAsync(CharacterSearch.SearchUri(name, homeWorld), cancellationToken).ConfigureAwait(false);
        if (page.Error != null)
            return new(SearchStatus.Unavailable, Detail: page.Error);
        if (page.Status != 200)
            return new(SearchStatus.Unavailable, Detail: $"Lodestone returned HTTP {page.Status}.");
        return CharacterSearch.Parse(page.Html, name, homeWorld);
    }

    internal async Task<RankResult> ReadRankAsync(ulong characterId, CancellationToken cancellationToken)
    {
        var page = await ReadPageAsync(RankingAchievements.PageUri(characterId), cancellationToken).ConfigureAwait(false);
        return page.Error != null
            ? new(RankStatus.Unavailable, Detail: page.Error)
            : RankingAchievements.Parse(page.Html, characterId, page.Status);
    }

    private async Task<(int Status, string Html, string? Error)> ReadPageAsync(Uri uri, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (DateTimeOffset.UtcNow < cooldownUntil)
            return (0, "", "Lodestone is busy or rate limiting requests. Reload the plugin later to retry.");

        using var response = await client.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
        {
            var retry = response.Headers.RetryAfter;
            var wait = retry?.Delta ?? (retry?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromMinutes(1);
            cooldownUntil = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(Math.Clamp(wait.TotalSeconds, 60, 300));
            return ((int)response.StatusCode, "", "Lodestone is busy or rate limiting requests. Reload the plugin later to retry.");
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ((int)response.StatusCode, html, null);
    }
}
