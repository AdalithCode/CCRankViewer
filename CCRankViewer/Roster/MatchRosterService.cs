using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.Sheets;

namespace CCRankViewer.Roster;

internal sealed class MatchRosterService : IDisposable
{
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;
    private readonly MatchRoster roster = new();
    private long nextScan;
    private uint dutyId;
    private bool errorLogged;

    internal IReadOnlyList<RosterPlayer> Players => roster.Players;
    internal bool IsCasualMatch { get; private set; }
    internal string Status { get; private set; } = "Enter a casual Crystalline Conflict match to detect players.";

    internal MatchRosterService(IFramework framework, IClientState clientState, IObjectTable objectTable,
        IDataManager dataManager, IPluginLog log)
    {
        this.framework = framework;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.dataManager = dataManager;
        this.log = log;
        framework.Update += OnUpdate;
        clientState.TerritoryChanged += OnTerritoryChanged;
    }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
        clientState.TerritoryChanged -= OnTerritoryChanged;
        Reset();
    }

    private void OnTerritoryChanged(uint territory) => Reset();

    private void Reset()
    {
        roster.Clear();
        IsCasualMatch = false;
        dutyId = 0;
        nextScan = 0;
        errorLogged = false;
        Status = "Enter a casual Crystalline Conflict match to detect players.";
    }

    private unsafe void OnUpdate(IFramework _)
    {
        if (Environment.TickCount64 < nextScan)
            return;
        nextScan = Environment.TickCount64 + 500;

        try
        {
            if (!clientState.IsLoggedIn || !clientState.IsPvPExcludingDen)
            {
                if (IsCasualMatch || Players.Count != 0)
                    Reset();
                return;
            }

            var gameMain = GameMain.Instance();
            var currentDuty = gameMain == null ? 0u : gameMain->CurrentContentFinderConditionId;
            if (currentDuty != dutyId)
            {
                roster.Clear();
                dutyId = currentDuty;
            }

            // Use the localized game's duty flags rather than matching English duty names.
            IsCasualMatch = currentDuty != 0
                && dataManager.GetExcelSheet<ContentFinderCondition>().TryGetRow(currentDuty, out var duty)
                && duty.PvP && duty.CrystallineConflictCasualRoulette
                && !duty.CrystallineConflictRankedRoulette;

            if (!IsCasualMatch)
            {
                roster.Clear();
                Status = "Waiting for a casual Crystalline Conflict duty.";
                return;
            }

            var local = objectTable.LocalPlayer;
            if (local == null || local.Address == IntPtr.Zero)
            {
                Status = "Casual CC detected. Waiting for your character to load...";
                return;
            }

            var players = new List<ObservedPlayer>();
            foreach (var obj in objectTable)
            {
                if (obj is not IPlayerCharacter player || player.Address == IntPtr.Zero
                    || player.EntityId == local.EntityId || !player.HomeWorld.IsValid)
                    continue;

                players.Add(new ObservedPlayer(player.EntityId, player.Name.ToString(), player.HomeWorld.RowId,
                    player.HomeWorld.Value.Name.ToString(), ((Character*)player.Address)->Battalion));
            }

            roster.Observe(local.EntityId, ((Character*)local.Address)->Battalion, players);
            Status = Players.Count == 9
                ? "Casual CC: all 9 other players detected."
                : $"Casual CC: {Players.Count}/9 other players detected. Waiting for players to load...";
            if (Players.Count == 9 && Players[0].Team == MatchTeam.Unknown)
                Status += " Waiting for team assignments...";
            errorLogged = false;
        }
        catch (Exception exception)
        {
            Status = "Could not read the match roster. Retrying; see /xllog for details.";
            if (!errorLogged)
            {
                log.Error(exception, "Failed to read the casual CC roster.");
                errorLogged = true;
            }
        }
    }
}
