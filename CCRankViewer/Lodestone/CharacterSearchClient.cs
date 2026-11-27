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
        if (DateTimeOffset.UtcNow < cooldownUntil)
            return new(SearchStatus.Unavailable, Detail: "Lodestone is busy or rate limiting requests. Reload the plugin later to retry.");

        using var response = await client.GetAsync(CharacterSearch.SearchUri(name, homeWorld), cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
        {
            var retry = response.Headers.RetryAfter;
            var wait = retry?.Delta ?? (retry?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromMinutes(1);
            cooldownUntil = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(Math.Clamp(wait.TotalSeconds, 60, 300));
            return new(SearchStatus.Unavailable, Detail: "Lodestone is busy or rate limiting requests. Reload the plugin later to retry.");
        }
        if (!response.IsSuccessStatusCode)
            return new(SearchStatus.Unavailable, Detail: $"Lodestone returned HTTP {(int)response.StatusCode}.");

        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return CharacterSearch.Parse(html, name, homeWorld);
    }
}
