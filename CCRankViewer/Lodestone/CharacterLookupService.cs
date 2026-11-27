using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CCRankViewer.Roster;
using Dalamud.Plugin.Services;

namespace CCRankViewer.Lodestone;

internal sealed class CharacterLookupService : IDisposable
{
    private readonly IFramework framework;
    private readonly MatchRosterService roster;
    private readonly IPluginLog log;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
    private readonly CancellationTokenSource lifetime = new();
    private readonly ConcurrentQueue<LookupWork> queue = new();
    private readonly CharacterSearchClient search;
    private readonly Task worker;
    private CancellationTokenSource session = new();
    private ConcurrentDictionary<(string, uint), CharacterSearchResult> results = new();
    private int generation = -1;

    internal CharacterLookupService(IFramework framework, MatchRosterService roster, IPluginLog log)
    {
        this.framework = framework;
        this.roster = roster;
        this.log = log;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("CCRankViewer/0.1 (+https://github.com/AdalithCode/CCRankViewer)");
        search = new CharacterSearchClient(http);
        worker = Task.Run(WorkAsync);
        framework.Update += OnUpdate;
    }

    internal CharacterSearchResult Get(RosterPlayer player) => results.TryGetValue((player.Name, player.HomeWorldId), out var result)
        ? result : new(SearchStatus.Queued);

    private void OnUpdate(IFramework _)
    {
        if (generation != roster.Generation)
        {
            session.Cancel();
            session.Dispose();
            session = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            results = new();
            generation = roster.Generation;
        }
        if (!roster.IsCasualMatch)
            return;

        foreach (var player in roster.Players)
            if (results.TryAdd((player.Name, player.HomeWorldId), new(SearchStatus.Queued)))
                queue.Enqueue(new(player, results, session.Token));
    }

    private async Task WorkAsync()
    {
        var token = lifetime.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (!queue.TryDequeue(out var work))
                {
                    await Task.Delay(200, token).ConfigureAwait(false);
                    continue;
                }
                if (work.Token.IsCancellationRequested)
                    continue;
                var key = (work.Player.Name, work.Player.HomeWorldId);
                CharacterSearchResult? character = null;
                try
                {
                    work.Results[key] = new(SearchStatus.Searching);
                    character = await search.FindAsync(work.Player.Name, work.Player.HomeWorld, work.Token).ConfigureAwait(false);
                    work.Results[key] = character;
                    if (character.Status == SearchStatus.Found && character.CharacterId is { } id)
                    {
                        work.Results[key] = character with { Status = SearchStatus.CheckingAchievements };
                        await Task.Delay(1000, work.Token).ConfigureAwait(false);
                        var rank = await search.ReadRankAsync(id, work.Token).ConfigureAwait(false);
                        work.Results[key] = character with { Rank = rank };
                    }
                }
                catch (OperationCanceledException) when (work.Token.IsCancellationRequested) { }
                catch (Exception exception)
                {
                    const string detail = "Lookup failed or timed out. See /xllog; reload the plugin later to retry.";
                    work.Results[key] = character?.CharacterId != null
                        ? character with { Rank = new(RankStatus.Unavailable, Detail: detail) }
                        : new(SearchStatus.Unavailable, Detail: detail);
                    log.Warning(exception, "Lodestone character or achievement lookup failed.");
                }
                // One request at a time, at least a second apart, independent of frame rate.
                await Task.Delay(1000, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
        session.Cancel();
        lifetime.Cancel();
        // Release HTTP resources after the worker has observed cancellation.
        _ = worker.ContinueWith(_ =>
        {
            http.Dispose();
            session.Dispose();
            lifetime.Dispose();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private sealed record LookupWork(RosterPlayer Player,
        ConcurrentDictionary<(string, uint), CharacterSearchResult> Results, CancellationToken Token);
}
