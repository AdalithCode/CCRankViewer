using System;
using Dalamud.Configuration;

namespace CCRankViewer;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool ShowExplanatoryText { get; set; } = true;
}
