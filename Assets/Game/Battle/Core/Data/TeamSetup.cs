using System;
using System.Collections.Generic;

namespace RingsideHearts.Battle.Core
{
    public enum Control { Human, AI }

    /// <summary>
    /// One row of teams.csv: the only thing that differs between my team and the enemy team
    /// (design 4.1). Up to three slots, each with a second (may be empty).
    /// </summary>
    public sealed class TeamSetup
    {
        public string Id;
        public string Name;
        public string[] Slots;
        public string[] Seconds;
        public Control Control;
        public float StaminaMul = 1f;
        public float PowerMul = 1f;
        public float BodyMul = 1f;
        /// <summary>AI only: random aim error, ± degrees.</summary>
        public float AiAimError;
        /// <summary>AI only: picks among this many best shots.</summary>
        public int AiCandidates = 3;
        /// <summary>AI only: finale mash taps per second.</summary>
        public float AiTapRate = 7f;
        /// <summary>AI only: finale timing accuracy 0..1.</summary>
        public float AiTiming = 0.6f;

        public TeamSetup Clone()
        {
            var c = (TeamSetup)MemberwiseClone();
            c.Slots = (string[])Slots.Clone();
            c.Seconds = (string[])Seconds.Clone();
            return c;
        }

        public static Dictionary<string, TeamSetup> ParseAll(string csv)
        {
            const string file = "teams.csv";
            CsvTable t = CsvTable.Parse(csv);
            CsvRead.RequireColumns(t, file, "id", "name", "slot1", "second1", "slot2", "second2", "slot3", "second3", "control",
                "stamina_mul", "power_mul", "body_mul", "ai_aim_error", "ai_candidates", "ai_tap_rate", "ai_timing");
            var all = new Dictionary<string, TeamSetup>(StringComparer.Ordinal);
            for (int i = 0; i < t.RowCount; i++)
            {
                string id = CsvRead.Text(t, i, "id");
                if (id.Length == 0) continue;
                if (all.ContainsKey(id)) throw new FormatException($"{file}: id '{id}' appears twice");
                string control = CsvRead.Text(t, i, "control");
                Control c;
                if (control == "사람") c = Control.Human;
                else if (control == "AI") c = Control.AI;
                else throw new FormatException($"'{id}': control must be 사람 or AI ('{control}')");
                var slots = new List<string>();
                var seconds = new List<string>();
                for (int s = 1; s <= 3; s++)
                {
                    string slot = CsvRead.Text(t, i, "slot" + s);
                    if (slot.Length == 0) continue;
                    slots.Add(slot);
                    seconds.Add(CsvRead.Text(t, i, "second" + s));
                }
                if (slots.Count == 0) throw new FormatException($"'{id}': needs at least slot1");
                all[id] = new TeamSetup
                {
                    Id = id,
                    Name = CsvRead.Text(t, i, "name"),
                    Slots = slots.ToArray(),
                    Seconds = seconds.ToArray(),
                    Control = c,
                    StaminaMul = CsvRead.Number(t, i, "stamina_mul", id),
                    PowerMul = CsvRead.Number(t, i, "power_mul", id),
                    BodyMul = CsvRead.Number(t, i, "body_mul", id),
                    AiAimError = CsvRead.Number(t, i, "ai_aim_error", id),
                    AiCandidates = CsvRead.Integer(t, i, "ai_candidates", id),
                    AiTapRate = CsvRead.Number(t, i, "ai_tap_rate", id),
                    AiTiming = CsvRead.Number(t, i, "ai_timing", id),
                };
            }
            return all;
        }
    }
}
