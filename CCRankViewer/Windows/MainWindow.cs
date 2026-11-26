using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace CCRankViewer.Windows;

internal sealed class MainWindow : Window
{
    private readonly Plugin plugin;

    internal MainWindow(Plugin plugin) : base("Crystalline Conflict Rank Viewer###CCRankViewerMain")
    {
        this.plugin = plugin;
        Size = new Vector2(620, 320);
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
            ImGui.TextWrapped("Best season finish is based on earned CC ranking achievements. It does not represent current rank.");
        }

        ImGui.Separator();
        ImGui.TextWrapped("Template ready. Match roster detection and Lodestone achievement lookups are not connected yet.");
        ImGui.Spacing();

        if (ImGui.BeginTable("RankResults", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            try
            {
                ImGui.TableSetupColumn("Player");
                ImGui.TableSetupColumn("Home world");
                ImGui.TableSetupColumn("Best season finish");
                ImGui.TableSetupColumn("Status");
                ImGui.TableHeadersRow();
            }
            finally
            {
                ImGui.EndTable();
            }
        }
    }
}
