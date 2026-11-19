using System;
using System.Collections.Generic;

namespace RingsideHearts.Battle.Core
{
    /// <summary>One row of matches.csv: a test match and its rule switches (design v0.2 6).</summary>
    public sealed class MatchRules
    {
        public string Id;
        public string Name;
        public string EnemyTeam;
        public bool Cage;
        public bool Hardcore;
        public bool NoRopeBreak;
        public bool SubmissionOnly;
        public bool Handicap;
        public bool Tornado;
        public int TurnLimit = 30;
        public string Description = "";

        public bool RingoutAllowed => !Cage && !SubmissionOnly;
        public bool PinfallAllowed => !SubmissionOnly;
        public bool AssistsAllowed => !Cage;
        public bool TagAllowed => !Tornado;
        public int InRingPerTeam => Tornado ? 2 : 1;

        public static MatchRules Default(string enemyTeam) => new MatchRules { Id = "tag", Name = "태그 매치", EnemyTeam = enemyTeam };

        static readonly string[] Known = { "cage", "hardcore", "no_rope_break", "submission_only", "handicap", "tornado" };

        public static Dictionary<string, MatchRules> ParseAll(string csv)
        {
            const string file = "matches.csv";
            CsvTable t = CsvTable.Parse(csv);
            CsvRead.RequireColumns(t, file, "id", "name", "enemy_team", "rules", "turn_limit", "description");
            var all = new Dictionary<string, MatchRules>(StringComparer.Ordinal);
            for (int i = 0; i < t.RowCount; i++)
            {
                string id = CsvRead.Text(t, i, "id");
                if (id.Length == 0) continue;
                if (all.ContainsKey(id)) throw new FormatException($"{file}: id '{id}' appears twice");
                var m = new MatchRules
                {
                    Id = id,
                    Name = CsvRead.Text(t, i, "name"),
                    EnemyTeam = CsvRead.Text(t, i, "enemy_team"),
                    TurnLimit = CsvRead.Integer(t, i, "turn_limit", id),
                    Description = CsvRead.Text(t, i, "description"),
                };
                foreach (string raw in CsvRead.Text(t, i, "rules").Split(';'))
                {
                    string r = raw.Trim();
                    if (r.Length == 0) continue;
                    if (Array.IndexOf(Known, r) < 0) throw new FormatException($"'{id}': unknown rule '{r}'");
                    if (r == "cage") m.Cage = true;
                    else if (r == "hardcore") m.Hardcore = true;
                    else if (r == "no_rope_break") m.NoRopeBreak = true;
                    else if (r == "submission_only") m.SubmissionOnly = true;
                    else if (r == "handicap") m.Handicap = true;
                    else m.Tornado = true;
                }
                all[id] = m;
            }
            return all;
        }
    }
}
