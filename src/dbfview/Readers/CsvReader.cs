using System.Data;
using System.Text;

namespace dbfview.Readers;

public class CsvReader : IFileReader
{
    public string[] Extensions => [".csv"];
    public string FormatName => "CSV";
    public bool SupportsSave => false;

    public bool FirstRowAsHeader { get; set; } = true;
    public char Delimiter { get; set; } = ',';

    public Encoding DetectEncoding(string path)
    {
        var sampleLen = (int)Math.Min(new FileInfo(path).Length, 4096);
        var sample = new byte[sampleLen];
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.ReadExactly(sample);
        return EncodingDetector.Detect(sample, sampleLen);
    }

    public DataTable Read(string path, Encoding enc)
    {
        var raw = File.ReadAllText(path, enc);

        // Strip BOM if present
        if (raw.Length > 0 && raw[0] == '\uFEFF')
            raw = raw[1..];

        var lines = ParseLines(raw);
        if (lines.Count == 0)
            return new DataTable();

        var headers = new List<string?>();
        var startRow = 0;

        if (FirstRowAsHeader)
        {
            headers = lines[0];
            startRow = 1;
        }
        else
        {
            for (var i = 0; i < lines[0].Count; i++)
                headers.Add(ExcelColumnName(i));
        }

        // Deduplicate headers
        var usedNames = new Dictionary<string, int>();
        for (var i = 0; i < headers.Count; i++)
        {
            var name = string.IsNullOrEmpty(headers[i]) ? $"F{i}" : headers[i]!;
            if (usedNames.TryGetValue(name, out var count))
            {
                count++;
                usedNames[name] = count;
                name = $"{name}_{count}";
            }
            usedNames[name] = 1;
            headers[i] = name;
        }

        var table = new DataTable();
        table.Columns.Add("_row", typeof(int));

        var colTypes = new List<bool>(); // true = numeric
        for (var i = 0; i < headers.Count; i++)
        {
            colTypes.Add(false);
            table.Columns.Add(new DataColumn(headers[i], typeof(string)) { AllowDBNull = true });
        }

        // Read data rows
        var allRows = new List<List<string?>>();
        for (var r = startRow; r < lines.Count; r++)
        {
            var fields = lines[r];
            // Pad to header count
            while (fields.Count < headers.Count)
                fields.Add(string.Empty);
            allRows.Add(fields);
        }

        // Detect numeric columns: sample first 100 rows
        for (var c = 0; c < headers.Count; c++)
        {
            var sampleCount = Math.Min(allRows.Count, 100);
            var allNumeric = true;
            var anyValue = false;
            for (var r = 0; r < sampleCount; r++)
            {
                var val = c < allRows[r].Count ? allRows[r][c] : null;
                if (string.IsNullOrEmpty(val)) continue;
                anyValue = true;
                if (!decimal.TryParse(val.Trim(), out _))
                {
                    allNumeric = false;
                    break;
                }
            }
            if (allNumeric && anyValue)
            {
                colTypes[c] = true;
                var oldCol = table.Columns[headers[c]!];
                var idx = table.Columns.IndexOf(oldCol);
                table.Columns.RemoveAt(idx);
                var newCol = new DataColumn(headers[c]!, typeof(decimal)) { AllowDBNull = true };
                table.Columns.Add(newCol);
                newCol.SetOrdinal(idx);

                // Convert existing values
                foreach (var row in allRows)
                {
                    var val = c < row.Count ? row[c] : null;
                    if (!string.IsNullOrEmpty(val) && decimal.TryParse(val.Trim(), out var num))
                        row[c] = num.ToString(); // Keep as string in temp storage
                }
            }
        }

        // Populate DataTable
        for (var r = 0; r < allRows.Count; r++)
        {
            var rowData = new object[1 + headers.Count];
            rowData[0] = r + 1; // _row

            for (var c = 0; c < headers.Count; c++)
            {
                var val = c < allRows[r].Count ? allRows[r][c] : null;
                if (string.IsNullOrEmpty(val))
                {
                    rowData[1 + c] = DBNull.Value;
                }
                else if (colTypes[c])
                {
                    if (decimal.TryParse(val.Trim(), out var num))
                        rowData[1 + c] = num;
                    else
                        rowData[1 + c] = DBNull.Value;
                }
                else
                {
                    rowData[1 + c] = val;
                }
            }
            table.Rows.Add(rowData);
        }

        return table;
    }

    private List<List<string?>> ParseLines(string text)
    {
        var delim = Delimiter;
        var lines = new List<List<string?>>();
        var i = 0;
        var len = text.Length;

        while (i < len)
        {
            var fields = new List<string?>();
            while (i < len)
            {
                if (text[i] == '\r')
                {
                    i++;
                    if (i < len && text[i] == '\n') i++;
                    break;
                }
                if (text[i] == '\n') { i++; break; }

                var field = ParseField(text, ref i, delim);
                fields.Add(field);

                if (i < len && text[i] == delim)
                    i++;
            }
            lines.Add(fields);
        }

        return lines;
    }

    private static string? ParseField(string text, ref int i, char delim)
    {
        if (i < text.Length && text[i] == '"')
        {
            // Quoted field
            i++;
            var sb = new StringBuilder();
            while (i < text.Length)
            {
                if (text[i] == '"')
                {
                    i++;
                    if (i < text.Length && text[i] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    sb.Append(text[i]);
                    i++;
                }
            }
            return sb.ToString();
        }
        else
        {
            // Unquoted field
            var start = i;
            while (i < text.Length)
            {
                var c = text[i];
                if (c == delim || c == '\r' || c == '\n')
                    break;
                i++;
            }
            return text[start..i].Trim();
        }
    }

    private static string ExcelColumnName(int index)
    {
        var name = "";
        var n = index;
        while (n >= 0)
        {
            name = (char)('A' + (n % 26)) + name;
            n = n / 26 - 1;
        }
        return name;
    }
}
