using System;
using System.Collections.Generic;

namespace RingsideHearts.Battle.Core
{
    /// <summary>열혈, 냉정, 개방, 성실, 음침</summary>
    public enum FighterAttribute { Hot, Cool, Open, Steady, Shady }

    public enum FighterType { Striker, Showman, Grappler, Stampeder, Highflier }

    /// <summary>Tag chain role: 선봉 → 연결 → 마무리.</summary>
    public enum ChainRole { Vanguard, Link, Finisher }

    public enum Faction { Face, Heel }

    /// <summary>What a teammate does when your shot touches them on the apron (design v0.2 5).</summary>
    public enum AssistKind { None, Lariat, Stomp, Cheer, Icing }

    /// <summary>One row of characters.csv. The same data serves either team (design 4.1).</summary>
    public sealed class CharacterData
    {
        public string Id;
        public string Name;
        public string Short;
        public Faction Faction;
        public FighterAttribute Attribute;
        public FighterType Type;
        public ChainRole Role;
        public int Grade;
        public float Stamina;
        public float Power;
        public float Guts;
        public bool HeatDependent;
        public float AppealHeat;
        public string SpecialName;
        public float SpecialMul;
        public int SpecialTurns;
        /// <summary>The special's cut-in fires when the shooter first hits a rope (else right on release).</summary>
        public bool SpecialAtRope;
        public AssistKind Assist;
        public string SecondSkill;
        public string SecondText;
        public bool Playable;

        public static readonly string[] AttributeNames = { "열혈", "냉정", "개방", "성실", "음침" };
        public static readonly string[] TypeNames = { "스트라이커", "쇼메이커", "그래플러", "스탬피더", "하이플라이어" };
        public static readonly string[] TypeLetters = { "S", "M", "G", "T", "H" };
        public static readonly string[] RoleNames = { "선봉", "연결", "마무리" };
        public static readonly string[] AssistNames = { "없음", "라리아트", "스톰프", "함성", "아이싱" };

        public static Dictionary<string, CharacterData> ParseAll(string csv)
        {
            const string file = "characters.csv";
            CsvTable t = CsvTable.Parse(csv);
            CsvRead.RequireColumns(t, file, "id", "name", "short", "faction", "attribute", "type", "role", "grade", "stamina",
                "power", "guts", "heat_dep", "appeal_heat", "special_name", "special_mul", "special_turns", "special_timing", "assist",
                "second_skill", "second_text", "playable");
            var all = new Dictionary<string, CharacterData>(StringComparer.Ordinal);
            for (int i = 0; i < t.RowCount; i++)
            {
                string id = CsvRead.Text(t, i, "id");
                if (id.Length == 0) continue;
                if (all.ContainsKey(id)) throw new FormatException($"{file}: id '{id}' appears twice");
                string skill = CsvRead.Text(t, i, "second_skill");
                EffectBlock.Parse(skill, id); // validate early
                all[id] = new CharacterData
                {
                    Id = id,
                    Name = CsvRead.Text(t, i, "name"),
                    Short = CsvRead.Text(t, i, "short"),
                    Faction = (Faction)Pick(t, i, "faction", id, "페이스", "힐"),
                    Attribute = (FighterAttribute)Pick(t, i, "attribute", id, AttributeNames),
                    Type = (FighterType)Pick(t, i, "type", id, TypeNames),
                    Role = (ChainRole)Pick(t, i, "role", id, RoleNames),
                    Grade = CsvRead.Integer(t, i, "grade", id),
                    Stamina = CsvRead.Number(t, i, "stamina", id),
                    Power = CsvRead.Number(t, i, "power", id),
                    Guts = CsvRead.Number(t, i, "guts", id),
                    HeatDependent = Pick(t, i, "heat_dep", id, "보통", "높음") == 1,
                    AppealHeat = CsvRead.Number(t, i, "appeal_heat", id),
                    SpecialName = CsvRead.Text(t, i, "special_name"),
                    SpecialMul = CsvRead.Number(t, i, "special_mul", id),
                    SpecialTurns = CsvRead.Integer(t, i, "special_turns", id),
                    SpecialAtRope = Pick(t, i, "special_timing", id, "발사", "로프") == 1,
                    Assist = (AssistKind)Pick(t, i, "assist", id, AssistNames),
                    SecondSkill = skill,
                    SecondText = CsvRead.Text(t, i, "second_text"),
                    Playable = CsvRead.Integer(t, i, "playable", id) != 0,
                };
            }
            return all;
        }

        static int Pick(CsvTable t, int row, string column, string id, params string[] names)
        {
            string v = CsvRead.Text(t, row, column);
            int k = Array.IndexOf(names, v);
            if (k < 0) throw new FormatException($"'{id}': unknown {column} '{v}' ({string.Join("/", names)})");
            return k;
        }
    }
}
