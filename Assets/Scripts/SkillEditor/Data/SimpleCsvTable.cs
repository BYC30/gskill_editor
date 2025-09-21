using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GSkill.SkillEditor.Data
{
    /// <summary>
    /// 轻量级 CSV 表结构解析工具，仅支持制表符分隔的配置文件。
    /// </summary>
    internal sealed class SimpleCsvTable
    {
        private readonly Dictionary<string, int> _columnLookup;
        private readonly List<string[]> _rows;

        private SimpleCsvTable(IReadOnlyList<string> headers, List<string[]> rows)
        {
            Headers = headers;
            _rows = rows;
            _columnLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                var header = headers[i];
                if (string.IsNullOrWhiteSpace(header))
                {
                    continue;
                }

                var cleaned = header.Trim();
                if (!_columnLookup.ContainsKey(cleaned))
                {
                    _columnLookup.Add(cleaned, i);
                }
            }
        }

        public IReadOnlyList<string> Headers { get; }
        public IReadOnlyList<string[]> Rows => _rows;

        public static SimpleCsvTable Load(string path, string expectedKey)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"未找到配置文件: {path}", path);
            }

            var lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length == 0)
            {
                throw new InvalidDataException($"配置文件为空: {path}");
            }

            var headerIndex = Array.FindIndex(lines, line => line.Split('\t').Any(cell => string.Equals(cell.Trim(), expectedKey, StringComparison.OrdinalIgnoreCase)));
            if (headerIndex < 0)
            {
                throw new InvalidDataException($"在 {path} 中未找到字段 {expectedKey}");
            }

            var headerLine = SplitLine(lines[headerIndex]);
            // 类型行位于 header 下一行，正文再下一行开始
            var dataStartIndex = Math.Min(headerIndex + 2, lines.Length);
            var rows = new List<string[]>();
            for (var i = dataStartIndex; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var cells = SplitLine(line);
                if (cells.Length == 0)
                {
                    continue;
                }

                rows.Add(cells);
            }

            return new SimpleCsvTable(headerLine, rows);
        }

        public bool ContainsColumn(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                return false;
            }

            return _columnLookup.ContainsKey(columnName.Trim());
        }

        public bool TryGetColumnIndex(string columnName, out int index)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                index = -1;
                return false;
            }

            return _columnLookup.TryGetValue(columnName.Trim(), out index);
        }

        public string GetString(int rowIndex, string columnName, string defaultValue = "")
        {
            if (!_columnLookup.TryGetValue(columnName.Trim(), out var columnIndex))
            {
                return defaultValue;
            }

            if (rowIndex < 0 || rowIndex >= _rows.Count)
            {
                return defaultValue;
            }

            var row = _rows[rowIndex];
            if (columnIndex < 0 || columnIndex >= row.Length)
            {
                return defaultValue;
            }

            return CleanCell(row[columnIndex]) ?? defaultValue;
        }

        public int GetInt(int rowIndex, string columnName, int defaultValue = 0)
        {
            var raw = GetString(rowIndex, columnName, string.Empty);
            return int.TryParse(raw, out var value) ? value : defaultValue;
        }

        public double GetDouble(int rowIndex, string columnName, double defaultValue = 0d)
        {
            var raw = GetString(rowIndex, columnName, string.Empty);
            return double.TryParse(raw, out var value) ? value : defaultValue;
        }

        public IReadOnlyList<int> GetIntArray(int rowIndex, string columnName, char separator = ';')
        {
            var raw = GetString(rowIndex, columnName, string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<int>();
            }

            return raw.Split(separator)
                .Select(token => token.Trim())
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Select(token => int.TryParse(token, out var value) ? value : 0)
                .ToArray();
        }

        public IReadOnlyList<double> GetDoubleArray(int rowIndex, string columnName, char separator = ';')
        {
            var raw = GetString(rowIndex, columnName, string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<double>();
            }

            return raw.Split(separator)
                .Select(token => token.Trim())
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Select(token => double.TryParse(token, out var value) ? value : 0d)
                .ToArray();
        }

        public IReadOnlyList<string> GetStringArray(int rowIndex, string columnName, char separator = ';')
        {
            var raw = GetString(rowIndex, columnName, string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<string>();
            }

            return raw.Split(separator)
                .Select(token => token.Trim())
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .ToArray();
        }

        private static string[] SplitLine(string line)
        {
            return line.Split('\t');
        }

        private static string CleanCell(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            if (trimmed.Length >= 2 && trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2);
            }

            return trimmed;
        }
    }
}
