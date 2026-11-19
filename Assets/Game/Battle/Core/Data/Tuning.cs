using System;
using System.Collections.Generic;
using System.Globalization;

namespace RingsideHearts.Battle.Core
{
    /// <summary>
    /// All tunable numbers by key. The game fills it from GameVariables.asset; csv text
    /// (columns: key, value, note) is also accepted.
    /// </summary>
    public sealed class Tuning
    {
        readonly Dictionary<string, float> _values = new Dictionary<string, float>(StringComparer.Ordinal);

        public static Tuning FromCsv(string text)
        {
            var tuning = new Tuning();
            var table = CsvTable.Parse(text);
            for (int i = 0; i < table.RowCount; i++)
            {
                string key = table.Get(i, "key").Trim();
                if (key.Length == 0) continue;
                string raw = table.Get(i, "value").Trim();
                if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                    throw new FormatException($"tuning '{key}' has a non-numeric value '{raw}'");
                if (tuning._values.ContainsKey(key))
                    throw new FormatException($"tuning '{key}' is defined twice");
                tuning._values[key] = value;
            }
            return tuning;
        }

        public float Get(string key)
        {
            if (_values.TryGetValue(key, out float value)) return value;
            throw new KeyNotFoundException($"tuning key '{key}' is missing (GameVariables)");
        }

        public bool Has(string key) => _values.ContainsKey(key);

        public Tuning Clone()
        {
            var t = new Tuning();
            foreach (var kv in _values) t._values[kv.Key] = kv.Value;
            return t;
        }

        public void Set(string key, float value) => _values[key] = value;
    }
}
