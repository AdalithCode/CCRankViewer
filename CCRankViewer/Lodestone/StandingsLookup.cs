using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CCRankViewer.Lodestone;

internal sealed record LodestonePage(int Status, string Html, string? Error = null, bool HasMore = false);

// Used only by the sequential lookup worker. The cache belongs to one match.
internal sealed class StandingsLookup(
    Func<Uri, CancellationToken, Task<LodestonePage>> fetch, TimeSpan? spacing = null)
{
    private readonly Dictionary<Uri, LodestonePage> pages = new();
    private readonly TimeSpan requestSpacing = spacing ?? TimeSpan.FromSeconds(1);

    internal async Task<RankResult> FindAsync(ulong id, string name, string world, string? group,
        RankResult original, CancellationToken token)
    {
        if (original.Status == RankStatus.Confirmed || group == null)
            return original;

        try
        {
            string? expectedSeason = null;
            // Overall ladder (up to 300), then each tier's limited list.
            foreach (var tier in new[] { "all", "1", "2", "3", "4", "5", "6", "7", "8" })
            {
                string? season = null;
                string? updated = null;
                for (var pageNumber = 1; pageNumber <= 6; pageNumber++)
                {
                    token.ThrowIfCancellationRequested();
                    var uri = SeasonStandings.PageUri(group, tier, pageNumber);
                    if (!pages.TryGetValue(uri, out var response))
                    {
                        await Task.Delay(requestSpacing, token).ConfigureAwait(false);
                        try
                        {
                            response = await fetch(uri, token).ConfigureAwait(false);
                        }
                        catch (Exception exception) when ((exception is System.Net.Http.HttpRequestException or OperationCanceledException)
                            && !token.IsCancellationRequested)
                        {
                            response = new(0, "", "Standings request failed.");
                        }
                        // Never publish or cache an in-flight result after leaving the match.
                        token.ThrowIfCancellationRequested();
                        pages[uri] = response;
                    }
                    if (response.Status != 200 || response.Error != null)
                        return Unavailable(original);
                    var parsed = SeasonStandings.Parse(response.Html, pageNumber == 1);
                    if (pageNumber == 1)
                    {
                        season = parsed.Season;
                        updated = parsed.Updated;
                        if (expectedSeason != null && expectedSeason != season)
                            return Unavailable(original);
                        expectedSeason = season;
                    }
                    var matches = parsed.Entries.Where(p => p.CharacterId == id).ToArray();
                    if (matches.Length > 1)
                        return Unavailable(original);
                    if (matches.Length == 1)
                    {
                        var found = matches[0];
                        if (!string.Equals(found.Name, name, StringComparison.OrdinalIgnoreCase)
                            || !string.Equals(found.HomeWorld, world, StringComparison.OrdinalIgnoreCase))
                            return Unavailable(original);
                        return new(RankStatus.Standings, found.Tier, Detail:
                            $"{season} public standings: {found.Tier}. Published snapshot: {updated}. This is a published seasonal tier, not a best season finish or live rank. Achievement result: {original.StatusLabel}.");
                    }
                    if (!response.HasMore)
                        break;
                }
            }
            return original with { Detail = original.Detail + " No matching entry in the checked public standings. These lists have limited coverage; absence does not establish rank." };
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is FormatException or System.Net.Http.HttpRequestException
            or OperationCanceledException or System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            return Unavailable(original);
        }
    }

    private static RankResult Unavailable(RankResult original) => original with
    { Detail = original.Detail + " Standings fallback unavailable; the achievement result is unchanged." };
}
