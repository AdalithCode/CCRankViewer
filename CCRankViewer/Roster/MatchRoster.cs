using System;
using System.Collections.Generic;
using System.Linq;

namespace CCRankViewer.Roster;

internal enum MatchTeam { Unknown, Yours, Opposing }

internal sealed record ObservedPlayer(uint EntityId, string Name, uint HomeWorldId, string HomeWorld, byte Battalion);
internal sealed record RosterPlayer(string Name, uint HomeWorldId, string HomeWorld, MatchTeam Team);

// Keeps managed copies for this match, including players temporarily outside object range.
internal sealed class MatchRoster
{
    private readonly Dictionary<(string Name, uint World), ObservedPlayer> observed = new();
    private byte localBattalion;

    internal IReadOnlyList<RosterPlayer> Players { get; private set; } = Array.Empty<RosterPlayer>();

    internal void Observe(uint localEntityId, byte battalion, IEnumerable<ObservedPlayer> players)
    {
        localBattalion = battalion;
        foreach (var player in players)
        {
            if (player.EntityId == localEntityId || player.EntityId is 0 or 0xE0000000
                || string.IsNullOrWhiteSpace(player.Name) || player.HomeWorldId == 0)
                continue;

            observed[(player.Name, player.HomeWorldId)] = player;
        }

        // Do not guess team membership while battalion assignments are still loading.
        // Validate the expected four teammates and five opponents, without assuming
        // particular numeric battalion IDs (including whether zero is a valid ID).
        var yours = observed.Values.Count(p => p.Battalion == localBattalion);
        var opponents = observed.Values.Where(p => p.Battalion != localBattalion).ToArray();
        var teamsReady = yours == 4 && opponents.Length == 5
            && opponents.Select(p => p.Battalion).Distinct().Count() == 1;

        Players = observed.Values.Select(p => new RosterPlayer(p.Name, p.HomeWorldId, p.HomeWorld,
                !teamsReady ? MatchTeam.Unknown : p.Battalion == localBattalion ? MatchTeam.Yours : MatchTeam.Opposing))
            .OrderBy(p => p.Team).ThenBy(p => p.Name, StringComparer.Ordinal)
            .ThenBy(p => p.HomeWorldId).ToArray();
    }

    internal void Clear()
    {
        observed.Clear();
        Players = Array.Empty<RosterPlayer>();
    }
}
