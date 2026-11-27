using System;
using System.Linq;
using CCRankViewer.Roster;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine($"PASS: {message}");
}

var roster = new MatchRoster();
var ten = Enumerable.Range(1, 10).Select(i => new ObservedPlayer((uint)i, $"Player {i}",
    21, "Example", (byte)(i <= 5 ? 0 : 1))).ToArray();
roster.Observe(1, 0, ten.Take(6));
Check(roster.Players.Count == 5 && roster.Players.All(p => p.Name != "Player 1"), "Local player excluded during partial loading");
Check(roster.Players.All(p => p.Team == MatchTeam.Unknown), "Partial team assignments are not guessed");
roster.Observe(1, 0, ten);
Check(roster.Players.Count == 9 && roster.Players.Count(p => p.Team == MatchTeam.Yours) == 4
    && roster.Players.Count(p => p.Team == MatchTeam.Opposing) == 5, "All nine players split 4/5, including battalion zero");
roster.Observe(1, 0, ten.Take(2));
Check(roster.Players.Count == 9, "Out-of-range players retained without duplicate rows");
roster.Clear();
Check(roster.Players.Count == 0, "Leaving a match clears its roster");
roster.Observe(1, 0, new[] {
    new ObservedPlayer(2, "Same Name", 21, "World A", 0),
    new ObservedPlayer(3, "Same Name", 22, "World B", 1),
    new ObservedPlayer(4, "", 21, "World A", 0),
    new ObservedPlayer(5, "Loading", 0, "", 0),
});
Check(roster.Players.Count == 2, "Same-name players on different worlds preserved; incomplete identities skipped");
roster.Clear();
roster.Observe(1, 0, ten.Select(p => p with { Battalion = 0 }));
Check(roster.Players.All(p => p.Team == MatchTeam.Unknown), "Uninitialized battalions do not produce false teams");
roster.Observe(1, 0, ten);
Check(roster.Players.Count(p => p.Team == MatchTeam.Opposing) == 5, "Team assignments recover after loading completes");
roster.Clear();
roster.Observe(1, 1, ten);
Check(roster.Players.All(p => p.Team == MatchTeam.Unknown), "Invalid team sizes remain unassigned");

await CharacterSearchChecks.RunAsync();
await RankingAchievementChecks.RunAsync();
await StandingsChecks.RunAsync();
if (args.Length == 1)
{
    var result = CCRankViewer.Lodestone.CharacterSearch.Parse(System.IO.File.ReadAllText(args[0]), "Freya Highwind", "Spriggan");
    Check(result.CharacterId == 37533969, "Downloaded public Lodestone search HTML resolves the expected character");
}
if (args.Length == 2)
{
    var rank = CCRankViewer.Lodestone.RankingAchievements.Parse(System.IO.File.ReadAllText(args[0]), 37533969);
    Check(rank.Status == CCRankViewer.Lodestone.RankStatus.Confirmed && rank.Tier == "Crystal",
        "Downloaded public ranking page confirms Crystal");
    var restricted = CCRankViewer.Lodestone.RankingAchievements.Parse(System.IO.File.ReadAllText(args[1]), 62713057, 403);
    Check(restricted.Status == CCRankViewer.Lodestone.RankStatus.Restricted,
        "Downloaded Lodestone restriction page recognized");
}
if (args.Length == 3)
{
    var first = CCRankViewer.Lodestone.SeasonStandings.Parse(System.IO.File.ReadAllText(args[0]), true);
    Check(first.Entries.Count == 50 && first.Entries[0].CharacterId == 37421725 && first.Entries[0].Tier == "Ultima",
        "Downloaded official standings full page parses with expected identity and tier");
    var next = CCRankViewer.Lodestone.SeasonStandings.Parse(System.IO.File.ReadAllText(args[1]), false);
    Check(next.Entries.Count == 50 && next.Entries[0].Name == "Zeta Salazar", "Downloaded official standings continuation parses");
    var bronze = CCRankViewer.Lodestone.SeasonStandings.Parse(System.IO.File.ReadAllText(args[2]), true);
    Check(bronze.Entries.Count == 10 && bronze.Entries[0].Tier == "Bronze", "Downloaded official tier list parses");
}
