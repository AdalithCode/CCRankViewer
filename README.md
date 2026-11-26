# Crystalline Conflict Rank Viewer

Minimal Dalamud plugin foundation for a viewer of achievement-confirmed CC season-finish tiers.

## Included

- `/ccrank` toggles the main window.
- `/ccrank config` toggles settings.
- Dalamud's plugin installer can open either window.
- A saved setting controls the rank explanation.
- An empty results table is ready for player, world, best season finish, and lookup status.
- Command handlers and UI subscriptions are removed when the plugin unloads.

Match detection, achievement lookups, rank badges, and caching are future work. No player data is collected and no network requests are made by this starter.

## Build

Requires .NET 10 SDK and an updated XIVLauncher/Dalamud installation. The project uses `Dalamud.NET.Sdk/15.0.0` and the installed Dalamud development assemblies.

```powershell
dotnet build CCRankViewer.slnx -c Debug
```

The default Windows assembly location is `%APPDATA%\XIVLauncher\addon\Hooks\dev`. If your installation is elsewhere, set `DALAMUD_HOME` to the directory containing `Dalamud.dll` before building.

## Load in game

1. Build the project.
2. Open Dalamud settings with `/xlsettings` and find the Experimental developer plugin locations.
3. Add the full path to `bin\x64\Debug\CCRankViewer.dll` inside this project.
4. Open `/xlplugins`, find the plugin under Dev Tools, and enable it.
5. Run `/ccrank`. Run `/ccrank config` to check the saved setting.

Unload the development plugin before rebuilding if files are locked; reload it after the build. The starter does not modify your Dalamud configuration or register itself automatically.

## Next implementation steps

1. Identify match participants and their home worlds.
2. Resolve Lodestone character IDs and inspect achievement visibility.
3. Map the Solid Bronze through Solid Crystal achievements to confirmed tiers.
4. Add asynchronous lookups, request limits, caching, and clear unavailable/error states.

These achievements establish a season-finish milestone, rather than current rank or the highest tier reached during an unfinished season. Missing or private data must not be presented as Unranked. Coverage of any newer tiers needs verification before expanding the mapping.

Project structure follows the official [Dalamud SamplePlugin](https://github.com/goatcorp/SamplePlugin) and [Dalamud.NET.Sdk](https://github.com/goatcorp/Dalamud.NET.Sdk).
