using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using CCRankViewer.Roster;

namespace CCRankViewer.Windows;

internal sealed class MainWindow : Window
{
    private readonly Plugin plugin;

    internal MainWindow(Plugin plugin) : base("Crystalline Conflict Rank Viewer###CCRankViewerMain")
    {
        this.plugin = plugin;
        Size = new Vector2(720, 420);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 220),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void Draw()
    {
        if (ImGui.Button("Settings"))
            plugin.ToggleConfigUi();

        if (plugin.Configuration.ShowExplanatoryText)
        {
            ImGui.Spacing();
            ImGui.TextWrapped("Best confirmed finish is based on earned CC achievements from Bronze through Ultima. It does not represent current rank.");
        }

        ImGui.Separator();
        ImGui.TextWrapped(plugin.Roster.Status);
        ImGui.Spacing();

        if (!plugin.Roster.IsCasualMatch)
            return;

        DrawTeam(MatchTeam.Yours, "Your team (excluding you)");
        DrawTeam(MatchTeam.Opposing, "Opposing team");
        DrawTeam(MatchTeam.Unknown, "Players awaiting team assignment");
    }

    private void DrawTeam(MatchTeam team, string title)
    {
        var count = 0;
        foreach (var player in plugin.Roster.Players)
            if (player.Team == team)
                count++;

        if (team == MatchTeam.Unknown && count == 0)
            return;

        ImGui.TextUnformatted($"{title} ({count})");

        if (ImGui.BeginTable($"RankResults{team}", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            try
            {
                ImGui.TableSetupColumn("Player");
                ImGui.TableSetupColumn("Home world");
                ImGui.TableSetupColumn("Best confirmed finish");
                ImGui.TableSetupColumn("Status");
                ImGui.TableHeadersRow();
                foreach (var player in plugin.Roster.Players)
                {
                    if (player.Team != team)
                        continue;
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(player.Name);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(player.HomeWorld);
                    ImGui.TableNextColumn();
                    var character = plugin.Characters.Get(player);
                    ImGui.TextUnformatted(character.Rank?.RankLabel ?? (character.Status is Lodestone.SearchStatus.Queued
                        or Lodestone.SearchStatus.Searching or Lodestone.SearchStatus.CheckingAchievements ? "Not loaded" : "Unknown"));
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(character.Label);
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 30);
                        ImGui.TextUnformatted(character.Rank?.Detail ?? character.Detail ?? character.Label);
                        if (character.CharacterId is { } id)
                            ImGui.TextUnformatted($"Lodestone ID: {id}");
                        ImGui.PopTextWrapPos();
                        ImGui.EndTooltip();
                    }
                }
            }
            finally
            {
                ImGui.EndTable();
            }
        }
        ImGui.Spacing();
    }
}
