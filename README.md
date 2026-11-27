# Crystalline Conflict Rank Viewer

Dalamud plugin for viewing achievement-confirmed CC season-finish tiers in casual matches.

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
- Reads each matched character's public ranking achievements and shows the highest confirmed tier from Bronze through Ultima.
- The Status column shows lookup progress, confirmation, no qualifying achievement, restricted access, or unavailable results. Hover it for evidence and the matched Lodestone ID.
- Command handlers and UI subscriptions are removed when the plugin unloads.

Rank badges and persistent caching are future work. Confirmed ranks currently display as text.

Lookups send the detected names and home worlds to the public North American Lodestone website over HTTPS, then request the matched character's ranking-achievement page. One background worker makes requests sequentially with a one-second gap; rate-limit responses pause further requests. No login, shared database, or third-party lookup service is used. Results exist only in memory for the current match. Leaving cancels pending work; old responses cannot fill a new match's rows.

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

1. Add persistent caching and a refresh control.
2. Add rank badges and automatic window behavior.

## Verify roster detection

Reload the plugin after building and open `/ccrank` manually (automatic opening is future work).

1. Outside a match, the window should ask you to enter casual CC and show no players.
2. Enter casual CC. As characters load, the count should reach 9/9. Confirm four teammates and five opponents, excluding you, against the game's teams.
3. Confirm home worlds are correct, including visiting players. Temporarily distant or dead players should remain listed.
4. Leave the duty. The roster should clear. Enter another match and check that no previous players remain.
5. Ranked CC, custom CC, Frontline, Rival Wings, and the Wolves' Den should not populate this roster.
6. Reload the plugin midmatch and confirm it rebuilds the roster as player objects become available.
7. Watch Status progress from Queued to Finding character to Checking achievements. Compare a confirmed rank against the player's public Ranking achievements; hover Status for the achievement ID and explanation.
8. Leave while searches are pending, then enter another match. Previous results must not appear beside different players.
9. Check a character with public achievements but no earned tier achievement: the rank should show Unranked. Restricted achievements and failed lookups should still show Unknown.

The implementation reads the installed client structures' battalion field for team membership and the duty sheet's casual CC flag. Live duty identification, team assignments, and player visibility need validation in game; a successful build cannot verify those behaviors.

Run the dependency-free roster, character-search, and rank-parsing checks (including simulated HTTP responses) with:

```powershell
dotnet run --project Tests/CCRankViewer.RosterChecks.csproj
```

These achievements establish a season-finish milestone, rather than current rank or the highest tier reached during an unfinished season. The mapping is Solid Bronze (#3046), Solid Silver (#3047), Solid Gold (#3048), Solid Platinum (#3049), Solid Diamond (#3050), Solid Crystal (#3051), Omega Material (#3750), and Ultima Material (#3751). Only entries marked earned are used, ordered Bronze < Silver < Gold < Platinum < Diamond < Crystal < Omega < Ultima. A successfully read public list with none of these earned displays Unranked. This is the viewer's label for no earned season-finish rank achievement; current in-game rank is not checked. Explicit access restrictions display Private / restricted; generic failures, blocked pages, changed markup, or incomplete pages display unavailable. These restricted or failed lookups keep the rank Unknown.

Omega and Ultima mappings are confirmed by the official Lodestone achievement details: [Omega Material](https://na.finalfantasyxiv.com/lodestone/character/36047772/achievement/detail/3750/) and [Ultima Material](https://na.finalfantasyxiv.com/lodestone/character/28168413/achievement/detail/3751/).

Project structure follows the official [Dalamud SamplePlugin](https://github.com/goatcorp/SamplePlugin) and [Dalamud.NET.Sdk](https://github.com/goatcorp/Dalamud.NET.Sdk).
