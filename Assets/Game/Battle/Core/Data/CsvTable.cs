using System;
using System.Collections.Generic;
using System.Text;

namespace RingsideHearts.Battle.Core
{
    /// <summary>
    /// Minimal CSV reader for spreadsheet-exported data: strips a UTF-8 BOM, skips blank lines and
    /// lines starting with '#', and supports quoted fields with commas and doubled quotes.
    /// </summary>
    public sealed class CsvTable
    {
        readonly List<string> _header;
        readonly List<string[]> _rows;
        readonly Dictionary<string, int> _columns;

        CsvTable(List<string> header, List<string[]> rows)
        {
            _header = header;
            _rows = rows;
            _columns = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < header.Count; i++)
                _columns[header[i]] = i;
        }

        public IReadOnlyList<string> Header => _header;
        public int RowCount => _rows.Count;

        public bool HasColumn(string column) => _columns.ContainsKey(column);

        public string Get(int row, string column)
        {
            if (!_columns.TryGetValue(column, out int index))
                throw new KeyNotFoundException($"CSV column '{column}' not found");
            string[] cells = _rows[row];
            return index < cells.Length ? cells[index] : "";
        }

        public static CsvTable Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);

            var records = new List<string[]>();
            var fields = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;
            bool lineHasContent = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = true;
                    lineHasContent = true;
                }
                else if (c == ',')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    lineHasContent = true;
                }
                else if (c == '\r')
                {
                    // handled by the following '\n' (or treated as a line end on its own)
                    if (i + 1 >= text.Length || text[i + 1] != '\n')
                        EndLine(records, fields, field, ref lineHasContent);
                }
                else if (c == '\n')
                {
                    EndLine(records, fields, field, ref lineHasContent);
                }
                else
                {
                    field.Append(c);
                    lineHasContent = true;
                }
            }
            EndLine(records, fields, field, ref lineHasContent);

            if (records.Count == 0) return new CsvTable(new List<string>(), new List<string[]>());
            var header = new List<string>(records[0]);
            for (int i = 0; i < header.Count; i++) header[i] = header[i].Trim();
            records.RemoveAt(0);
            return new CsvTable(header, records);
        }

        static void EndLine(List<string[]> records, List<string> fields, StringBuilder field, ref bool lineHasContent)
        {
            if (lineHasContent)
            {
                fields.Add(field.ToString());
                bool isComment = fields.Count > 0 && fields[0].TrimStart().StartsWith("#", StringComparison.Ordinal);
                if (!isComment) records.Add(fields.ToArray());
            }
            fields.Clear();
            field.Clear();
            lineHasContent = false;
        }
    }
}
