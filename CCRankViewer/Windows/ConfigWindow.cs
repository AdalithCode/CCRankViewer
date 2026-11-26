using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace CCRankViewer.Windows;

internal sealed class ConfigWindow : Window
{
    private readonly Plugin plugin;

    internal ConfigWindow(Plugin plugin) : base("CC Rank Viewer Settings###CCRankViewerSettings")
    {
        this.plugin = plugin;
        Size = new Vector2(430, 160);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var showExplanatoryText = plugin.Configuration.ShowExplanatoryText;
        if (ImGui.Checkbox("Show rank explanation", ref showExplanatoryText))
        {
            plugin.Configuration.ShowExplanatoryText = showExplanatoryText;
            plugin.SaveConfiguration();
        }

        ImGui.Spacing();
        ImGui.TextWrapped("Settings save automatically. This starter does not make network requests.");
    }
}
