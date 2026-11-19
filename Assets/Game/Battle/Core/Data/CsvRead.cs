using System;
using System.Globalization;

namespace RingsideHearts.Battle.Core
{
    /// <summary>Typed cell reads for data tables; every error names the row id and the column.</summary>
    static class CsvRead
    {
        public static void RequireColumns(CsvTable table, string file, params string[] columns)
        {
            foreach (string c in columns)
                if (!table.HasColumn(c))
                    throw new FormatException($"{file}: column '{c}' is missing");
        }

        public static string Text(CsvTable t, int row, string column) => t.Get(row, column).Trim();

        public static float Number(CsvTable t, int row, string column, string id)
        {
            string raw = Text(t, row, column);
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                throw new FormatException($"'{id}': column '{column}' is not a number ('{raw}')");
            return v;
        }

        public static int Integer(CsvTable t, int row, string column, string id)
        {
            string raw = Text(t, row, column);
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                throw new FormatException($"'{id}': column '{column}' is not a whole number ('{raw}')");
            return v;
        }
    }
}
