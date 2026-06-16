using System.Data;
using System.Text;

namespace dbfview.Readers;

public class DbfReader : IFileReader
{
    private const byte DeletedFlag = 0x2A;

    public string[] Extensions => [".dbf"];
    public string FormatName => "DBF";
    public bool SupportsSave => true;

    public Encoding DetectEncoding(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (fs.Length < 32) return Encoding.GetEncoding("gbk");

        var header = new byte[32];
        fs.ReadExactly(header);

        if (header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF)
            return Encoding.UTF8;

        var headerLength = BitConverter.ToInt16(header, 8);
        if (headerLength <= 32 || headerLength > fs.Length) return Encoding.GetEncoding("gbk");

        var fieldAreaLen = (int)Math.Min(headerLength - 32, fs.Length - 32);
        var fieldArea = new byte[fieldAreaLen];
        fs.ReadExactly(fieldArea);

        return EncodingDetector.Detect(fieldArea, fieldAreaLen);
    }

    public DataTable Read(string path, Encoding enc)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(fs);

        reader.ReadByte();
        reader.ReadByte(); reader.ReadByte(); reader.ReadByte();
        var recordCount = reader.ReadInt32();
        var headerLength = reader.ReadInt16();
        reader.ReadInt16();
        reader.ReadBytes(20);

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

            var type = (char)reader.ReadByte();
            reader.ReadBytes(4);
            var length = reader.ReadByte();
            var decimals = reader.ReadByte();
            reader.ReadBytes(14);

            columnNames.Add(string.IsNullOrEmpty(name) ? $"F{i}" : name);
            columnTypes.Add(type);
            columnLengths.Add(length);
            columnDecimals.Add(decimals);
        }

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
}
