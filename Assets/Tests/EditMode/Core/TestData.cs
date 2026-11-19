using System.Collections.Generic;
using System.IO;
using RingsideHearts.Battle.Core;

namespace RingsideHearts.Battle.Tests
{
    /// <summary>The project's real data files, and a quick way to start a match from them.</summary>
    public static class TestData
    {
        public static Dictionary<string, CharacterData> Chars() => CharacterData.ParseAll(File.ReadAllText(TestPaths.DataFile("characters.csv")));
        public static Dictionary<string, TeamSetup> Teams() => TeamSetup.ParseAll(File.ReadAllText(TestPaths.DataFile("teams.csv")));
        public static Dictionary<string, MatchRules> Matches() => MatchRules.ParseAll(File.ReadAllText(TestPaths.DataFile("matches.csv")));

        /// <summary>player_default (human) against the match's enemy team.</summary>
        public static Match Start(string matchId = "tag", int seed = 1, Tuning t = null, bool meAi = false)
        {
            MatchRules rules = Matches()[matchId];
            Dictionary<string, TeamSetup> teams = Teams();
            TeamSetup me = teams["player_default"].Clone();
            if (meAi) me.Control = Control.AI;
            return new Match(rules, me, teams[rules.EnemyTeam], Chars(), t ?? TestTuning.Default(), seed);
        }

        /// <summary>A tiny shot that touches nobody.</summary>
        public static void Pass(Match m) => m.Shoot(new Vec2(0.3f, 0f));
    }
}
