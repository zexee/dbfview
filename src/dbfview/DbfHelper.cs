using System.Data;
using System.Text;

namespace dbfview;

public static class DbfHelper
{
    private const byte SignatureFoxPro = 0x30;
    private const byte DeletedFlag = 0x2A;
    private const byte ValidFlag = 0x20;
    private const byte FieldTerminator = 0x0D;
    private const byte EndOfFile = 0x1A;

    public static Encoding DetectEncoding(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 32) return Encoding.GetEncoding("gbk");

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8;

        var headerLength = BitConverter.ToInt16(bytes, 8);
        if (headerLength <= 32 || headerLength > bytes.Length) return Encoding.GetEncoding("gbk");

        var fieldCount = (headerLength - 33) / 32;
        var utf8 = Encoding.UTF8;

        for (var i = 0; i < fieldCount; i++)
        {
            var offset = 32 + i * 32;
            var nullPos = Array.IndexOf(bytes, (byte)0, offset, 11);
            var nameLen = nullPos >= 0 ? nullPos - offset : 11;
            if (nameLen <= 0) continue;

            try
            {
                var text = utf8.GetString(bytes, offset, nameLen);
                if (text.Contains('\uFFFD'))
                    return Encoding.GetEncoding("gbk");
            }
            catch
            {
                return Encoding.GetEncoding("gbk");
            }
        }

        return utf8;
    }

    public static DataTable Load(string path, Encoding enc)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(fs);
        
        var version = reader.ReadByte();          // 0
        reader.ReadByte(); reader.ReadByte(); reader.ReadByte(); // 1-3 date
        var recordCount = reader.ReadInt32();      // 4-7
        var headerLength = reader.ReadInt16();     // 8-9
        var recordLength = reader.ReadInt16();     // 10-11
        reader.ReadBytes(20);                      // 12-31 reserved

        var fieldCount = (headerLength - 33) / 32;

        var columnNames = new List<string>();
        var columnTypes = new List<char>();
        var columnLengths = new List<int>();
        var columnDecimals = new List<int>();

        for (var i = 0; i < fieldCount; i++)
        {
            var nameBytes = reader.ReadBytes(11);
            var nullPos = Array.IndexOf(nameBytes, (byte)0);
            var name = enc.GetString(nameBytes, 0, nullPos >= 0 ? nullPos : 11).Trim();

            var type = (char)reader.ReadByte();    // 11
            reader.ReadBytes(4);                   // 12-15
            var length = reader.ReadByte();         // 16
            var decimals = reader.ReadByte();       // 17
            reader.ReadBytes(14);                   // 18-31

            columnNames.Add(string.IsNullOrEmpty(name) ? $"F{i}" : name);
            columnTypes.Add(type);
            columnLengths.Add(length);
            columnDecimals.Add(decimals);
        }

        // skip terminator (0x0D) and any padding to header end
        fs.Seek(headerLength, SeekOrigin.Begin);

        var table = new DataTable();
        table.Columns.Add("_deleted", typeof(bool));
        table.Columns.Add("_row", typeof(int));
        for (var i = 0; i < columnNames.Count; i++)
        {
            var colType = columnTypes[i] == 'N' || columnTypes[i] == 'F' ? typeof(decimal) : typeof(string);
            var col = new DataColumn(columnNames[i], colType) { AllowDBNull = true };
            table.Columns.Add(col);
        }

        for (var r = 0; r < recordCount; r++)
        {
            var flag = reader.ReadByte();
            var rowData = new object[2 + columnNames.Count];
            rowData[0] = flag == DeletedFlag;
            rowData[1] = r + 1;

            for (var f = 0; f < columnNames.Count; f++)
            {
                var fieldBytes = reader.ReadBytes(columnLengths[f]);
                if (flag == DeletedFlag)
                {
                    rowData[2 + f] = DBNull.Value;
                    continue;
                }

                var str = enc.GetString(fieldBytes).TrimEnd('\0', ' ');
                if (columnTypes[f] == 'N' || columnTypes[f] == 'F')
                {
                    if (decimal.TryParse(str.Trim(), out var num))
                        rowData[2 + f] = num;
                    else
                        rowData[2 + f] = DBNull.Value;
                }
                else
                {
                    rowData[2 + f] = str;
                }
            }
            table.Rows.Add(rowData);
        }

        return table;
    }

    public static void Save(string path, DataTable table, Encoding enc)
    {
        var columns = new List<(string Name, char Type, int Length, int Decimals)>();
        foreach (DataColumn col in table.Columns)
        {
            if (col.ColumnName == "_deleted" || col.ColumnName == "_row") continue;
            if (col.ColumnName.Length > 10)
                throw new InvalidOperationException($"字段名 '{col.ColumnName}' 超过10个字符");

            if (col.DataType == typeof(decimal))
                columns.Add((col.ColumnName, 'N', Math.Max(20, col.ColumnName.Length * 2 + 10), 4));
            else
                columns.Add((col.ColumnName, 'C', Math.Max(col.MaxLength, col.ColumnName.Length * 2 + 10), 0));
        }

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fs);

        // Header
        writer.Write(SignatureFoxPro);              // 0 version
        var now = DateTime.Now;
        writer.Write((byte)(now.Year - 1900));      // 1
        writer.Write((byte)now.Month);              // 2
        writer.Write((byte)now.Day);                // 3
        writer.Write(table.Rows.Count);             // 4-7 record count
        var headerLen = (short)(32 + columns.Count * 32 + 1);
        writer.Write(headerLen);                    // 8-9 header length
        writer.Write((short)(1 + columns.Sum(c => c.Length))); // 10-11 record length
        writer.Write((short)0);                     // 12-13 reserved
        writer.Write((byte)0);                      // 14
        writer.Write((byte)0);                      // 15
        writer.Write(0);                            // 16-19
        writer.Write(0);                            // 20-23
        writer.Write(0);                            // 24-27
        writer.Write((byte)0);                      // 28
        writer.Write((byte)0x86);                   // 29 language driver (0x86 = Chinese)
        writer.Write((short)0);                     // 30-31

        // Field descriptors
        foreach (var col in columns)
        {
            var nameBytes = new byte[11];
            var encoded = enc.GetBytes(col.Name);
            Array.Copy(encoded, nameBytes, Math.Min(encoded.Length, 10));
            writer.Write(nameBytes);                // 0-10
            writer.Write((byte)col.Type);           // 11
            writer.Write(0);                        // 12-15
            writer.Write((byte)col.Length);         // 16
            writer.Write((byte)col.Decimals);       // 17
            writer.Write((short)0);                 // 18-19
            writer.Write(new byte[12]);             // 20-31
        }
        writer.Write(FieldTerminator);

        // Records
        foreach (DataRow row in table.Rows)
        {
            var isDeleted = row["_deleted"] is bool d && d;
            writer.Write(isDeleted ? DeletedFlag : ValidFlag);

            for (var i = 0; i < columns.Count; i++)
            {
                var val = row[2 + i];
                string str;
                if (val == DBNull.Value || val == null)
                {
                    str = "";
                }
                else if (columns[i].Type == 'N')
                {
                    var num = (decimal)val;
                    str = num.ToString($"F{columns[i].Decimals}");
                }
                else
                {
                    str = val.ToString()!;
                }

                var fieldBytes = enc.GetBytes(str.PadRight(columns[i].Length)[..columns[i].Length]);
                if (fieldBytes.Length < columns[i].Length)
                {
                    var padded = new byte[columns[i].Length];
                    Array.Copy(fieldBytes, padded, fieldBytes.Length);
                    fieldBytes = padded;
                }
                writer.Write(fieldBytes);
            }
        }

        writer.Write(EndOfFile);
    }
}
