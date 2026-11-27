# Crystalline Conflict Rank Viewer

Minimal Dalamud plugin foundation for a viewer of achievement-confirmed CC season-finish tiers.

## Included

- `/ccrank` toggles the main window.
- `/ccrank config` toggles settings.
- Dalamud's plugin installer can open either window.
- A saved setting controls the rank explanation.
- Casual CC detection polls the current duty and loaded player objects every half second.
- The roster excludes you and shows the other nine players' names and home worlds.
- Players are grouped into four teammates and five opponents once team assignments are validated.
- Partial loading shows a detected-player count and an awaiting-assignment section.
- Players temporarily outside object range remain listed until you leave the match.
- Automatically searches Lodestone for each player's exact character name and home world, once per match.
- The Status column shows queued, searching, found, missing, ambiguous, or unavailable results. Hover it for details and the matched Lodestone ID.
- Command handlers and UI subscriptions are removed when the plugin unloads.

Achievement lookups, rank badges, and persistent caching are future work. Ranks still show "Not loaded" even after a character is found.

Character searches send the detected names and home worlds to the public North American Lodestone website over HTTPS. One background worker makes requests sequentially with a one-second gap; rate-limit responses pause further requests. No login, shared database, or third-party lookup service is used. Results exist only in memory for the current match. Leaving cancels pending work; old responses cannot fill a new match's rows.

Searches require an exact name and home-world match. Ambiguous or multi-page results are left for review. A blocked, failed, or unexpected response is "Lookup unavailable", never Unranked. Searches are attempted once per character per match; reload the plugin later to retry an error. Persistent caching and a refresh control are future work.

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

1. Resolve Lodestone character IDs and inspect achievement visibility.
2. Map the Solid Bronze through Solid Crystal achievements to confirmed tiers.
3. Add asynchronous lookups, request limits, caching, and clear unavailable/error states.

## Verify roster detection

Reload the plugin after building and open `/ccrank` manually (automatic opening is future work).

1. Outside a match, the window should ask you to enter casual CC and show no players.
2. Enter casual CC. As characters load, the count should reach 9/9. Confirm four teammates and five opponents, excluding you, against the game's teams.
3. Confirm home worlds are correct, including visiting players. Temporarily distant or dead players should remain listed.
4. Leave the duty. The roster should clear. Enter another match and check that no previous players remain.
5. Ranked CC, custom CC, Frontline, Rival Wings, and the Wolves' Den should not populate this roster.
6. Reload the plugin midmatch and confirm it rebuilds the roster as player objects become available.
7. Watch Status progress from Queued to Character found. Hover it and compare the Lodestone ID with that player's profile. Rank should still say Not loaded.
8. Leave while searches are pending, then enter another match. Previous results must not appear beside different players.

The implementation reads the installed client structures' battalion field for team membership and the duty sheet's casual CC flag. Live duty identification, team assignments, and player visibility need validation in game; a successful build cannot verify those behaviors.

Run the dependency-free roster and character-search checks (including simulated HTTP responses) with:

```powershell
dotnet run --project Tests/CCRankViewer.RosterChecks.csproj
```

These achievements establish a season-finish milestone, rather than current rank or the highest tier reached during an unfinished season. Missing or private data must not be presented as Unranked. Coverage of any newer tiers needs verification before expanding the mapping.

Project structure follows the official [Dalamud SamplePlugin](https://github.com/goatcorp/SamplePlugin) and [Dalamud.NET.Sdk](https://github.com/goatcorp/Dalamud.NET.Sdk).
