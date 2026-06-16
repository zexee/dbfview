using System.Data;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace dbfview.Readers;

public class ExcelReader : IFileReader
{
    public string[] Extensions => [".xlsx"];
    public string FormatName => "Excel";
    public bool SupportsSave => false;

    public Encoding DetectEncoding(string path)
    {
        // .xlsx is always UTF-8 internally
        return Encoding.UTF8;
    }

    public DataTable Read(string path, Encoding encoding)
    {
        using var zip = ZipFile.OpenRead(path);

        // Read shared strings
        var sharedStrings = ReadSharedStrings(zip);

        // Find first worksheet path
        var sheetPath = FindFirstSheet(zip);
        if (sheetPath == null)
            return new DataTable();

        // Parse worksheet
        var entry = zip.GetEntry(sheetPath);
        if (entry == null)
            return new DataTable();

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);

        var ns = doc.Root!.Name.Namespace;
        var sheetData = doc.Root.Element(ns + "sheetData");
        if (sheetData == null)
            return new DataTable();

        var rows = sheetData.Elements(ns + "row").ToList();
        if (rows.Count == 0)
            return new DataTable();

        // Parse headers from first row
        var firstRowCells = rows[0].Elements(ns + "c").ToList();
        var colCount = firstRowCells.Count;
        for (var i = 1; i < rows.Count; i++)
        {
            var cellCount = rows[i].Elements(ns + "c").Count();
            if (cellCount > colCount)
                colCount = cellCount;
        }

        var headers = new List<string>();
        var headersFromFile = false;
        for (var i = 0; i < colCount; i++)
        {
            // Check if first row looks like header (non-numeric strings)
            headers.Add(ExcelColumnName(i));
        }
        if (firstRowCells.Count > 0)
        {
            // Use first row as header
            for (var i = 0; i < colCount; i++)
            {
                if (i < firstRowCells.Count)
                {
                    var val = GetCellValue(firstRowCells[i], ns, sharedStrings);
                    if (!string.IsNullOrWhiteSpace(val))
                        headers[i] = val;
                }
            }
            headersFromFile = true;
        }

        // Deduplicate headers
        var usedNames = new Dictionary<string, int>();
        for (var i = 0; i < headers.Count; i++)
        {
            var name = string.IsNullOrEmpty(headers[i]) ? $"F{i}" : headers[i];
            if (usedNames.TryGetValue(name, out var count))
            {
                count++;
                usedNames[name] = count;
                name = $"{name}_{count}";
            }
            usedNames[name] = 1;
            headers[i] = name;
        }

        // Build DataTable
        var table = new DataTable();
        table.Columns.Add("_row", typeof(int));

        var colTypes = new List<bool>();
        for (var i = 0; i < headers.Count; i++)
        {
            colTypes.Add(false);
            table.Columns.Add(new DataColumn(headers[i], typeof(string)) { AllowDBNull = true });
        }

        // Read all rows content
        var dataRows = new List<List<string?>>();
        var startRow = headersFromFile ? 1 : 0;
        for (var r = startRow; r < rows.Count; r++)
        {
            var cells = rows[r].Elements(ns + "c").ToList();
            var cellMap = new Dictionary<int, string?>();
            foreach (var cell in cells)
            {
                var colIdx = GetColumnIndex(GetCellReference(cell));
                if (colIdx < 0 || colIdx >= colCount) continue;
                cellMap[colIdx] = GetCellValue(cell, ns, sharedStrings);
            }

            var rowData = new List<string?>(colCount);
            for (var c = 0; c < colCount; c++)
                rowData.Add(cellMap.TryGetValue(c, out var v) ? v : null);
            dataRows.Add(rowData);
        }

        // Detect numeric columns
        for (var c = 0; c < headers.Count; c++)
        {
            var sampleCount = Math.Min(dataRows.Count, 100);
            var allNumeric = true;
            var anyValue = false;
            for (var r = 0; r < sampleCount; r++)
            {
                var val = c < dataRows[r].Count ? dataRows[r][c] : null;
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
                var oldCol = table.Columns[headers[c]];
                var idx = table.Columns.IndexOf(oldCol);
                table.Columns.RemoveAt(idx);
                var newCol = new DataColumn(headers[c], typeof(decimal)) { AllowDBNull = true };
                table.Columns.Add(newCol);
                newCol.SetOrdinal(idx);
            }
        }

        // Populate DataTable
        for (var r = 0; r < dataRows.Count; r++)
        {
            var rowData = new object[1 + headers.Count];
            rowData[0] = r + 1;

            for (var c = 0; c < headers.Count; c++)
            {
                var val = c < dataRows[r].Count ? dataRows[r][c] : null;
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

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var strings = new List<string>();
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry == null) return strings;

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        var ns = doc.Root!.Name.Namespace;

        foreach (var si in doc.Root.Elements(ns + "si"))
        {
            var t = si.Element(ns + "t");
            strings.Add(t?.Value ?? "");
        }

        return strings;
    }

    private static string? FindFirstSheet(ZipArchive zip)
    {
        var entry = zip.GetEntry("xl/workbook.xml");
        if (entry == null) return null;

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        var ns = doc.Root!.Name.Namespace;

        var sheet = doc.Root.Element(ns + "sheets")?.Element(ns + "sheet");
        if (sheet == null) return null;

        var name = sheet.Attribute("name")?.Value ?? "sheet1";
        var sheetId = sheet.Attribute("sheetId")?.Value ?? "1";

        // Try to find by r:id relationship
        var rId = sheet.Attribute(XName.Get("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"))?.Value;
        if (rId != null)
        {
            // Parse rels to find actual path
            var relsPath = "xl/_rels/workbook.xml.rels";
            var relsEntry = zip.GetEntry(relsPath);
            if (relsEntry != null)
            {
                using var relsStream = relsEntry.Open();
                var relsDoc = XDocument.Load(relsStream);
                var relsNs = relsDoc.Root!.Name.Namespace;
                foreach (var rel in relsDoc.Root.Elements())
                {
                    if (rel.Attribute("Id")?.Value == rId)
                    {
                        var target = rel.Attribute("Target")?.Value;
                        if (target != null)
                            return "xl/" + target;
                    }
                }
            }
        }

        // Fallback: try common patterns
        return $"xl/worksheets/sheet{sheetId}.xml";
    }

    private static string GetCellReference(XElement cell)
    {
        return cell.Attribute("r")?.Value ?? "";
    }

    private static int GetColumnIndex(string reference)
    {
        // Extract column letters: "A1" → 0, "AB3" → 27
        var letters = new string(reference.TakeWhile(char.IsLetter).ToArray());
        var col = 0;
        foreach (var c in letters)
            col = col * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
        return col - 1;
    }

    private static string? GetCellValue(XElement cell, XNamespace ns, List<string> sharedStrings)
    {
        var t = cell.Attribute("t")?.Value;
        var v = cell.Element(ns + "v")?.Value;
        var isElement = cell.Element(ns + "is");

        if (isElement != null)
        {
            // Inline string
            return isElement.Element(ns + "t")?.Value ?? "";
        }

        if (v == null) return null;

        if (t == "s")
        {
            // Shared string
            if (int.TryParse(v, out var idx) && idx >= 0 && idx < sharedStrings.Count)
                return sharedStrings[idx];
            return "";
        }

        if (t == "b")
            return v == "1" ? "TRUE" : "FALSE";

        // Number or plain string
        return v;
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
