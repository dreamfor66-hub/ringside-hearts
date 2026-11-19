using System;
using System.Globalization;

namespace RingsideHearts.Battle.Core
{
    /// <summary>
    /// A second skill written as "when | to whom | what" (checklist 5.2, design v0.1 9.2), e.g.
    /// <c>on:partner_causes_down | to:team | heat+10</c>. "none" or empty means no effect.
    /// </summary>
    public sealed class EffectBlock
    {
        public static readonly string[] Whens = { "always", "on:partner_causes_down", "on:partner_appeal", "once:no_hit_turn", "on:partner_first_shot" };
        public static readonly string[] Targets = { "partner", "team", "self", "enemy_second", "enemy_active" };
        public static readonly string[] Stats =
        {
            "heat", "appeal_heat", "max_stamina", "intervention", "down_recovery", "friction", "finale_power",
            "power", "lariat", "intervention_time", "submission_resist", "cancel_heat_penalty",
        };

        public string When;
        public string To;
        public string Stat;
        /// <summary>'*' multiply, '+' add, '!' flag (cancel_heat_penalty).</summary>
        public char Op;
        public float Value;

        public static EffectBlock Parse(string text, string owner)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Trim() == "none") return null;
            string[] parts = text.Split('|');
            if (parts.Length != 3) throw new FormatException($"'{owner}': effect block needs 'when | to | what' ('{text}')");
            string when = parts[0].Trim();
            string to = parts[1].Trim();
            string what = parts[2].Trim();
            if (Array.IndexOf(Whens, when) < 0) throw new FormatException($"'{owner}': unknown when '{when}'");
            if (!to.StartsWith("to:") || Array.IndexOf(Targets, to.Substring(3)) < 0) throw new FormatException($"'{owner}': unknown target '{to}'");
            var e = new EffectBlock { When = when, To = to.Substring(3) };
            if (what == "cancel_heat_penalty")
            {
                e.Stat = what;
                e.Op = '!';
                return e;
            }
            int k = what.IndexOfAny(new[] { '*', '+' });
            if (k <= 0) throw new FormatException($"'{owner}': effect '{what}' needs stat*x or stat+x");
            e.Stat = what.Substring(0, k);
            e.Op = what[k];
            if (Array.IndexOf(Stats, e.Stat) < 0) throw new FormatException($"'{owner}': unknown stat '{e.Stat}'");
            if (!float.TryParse(what.Substring(k + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out e.Value))
                throw new FormatException($"'{owner}': effect value in '{what}' is not a number");
            return e;
        }
    }
}
