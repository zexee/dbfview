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

        writer.Write(SignatureFoxPro);
        var now = DateTime.Now;
        writer.Write((byte)(now.Year - 1900));
        writer.Write((byte)now.Month);
        writer.Write((byte)now.Day);
        writer.Write(table.Rows.Count);
        var headerLen = (short)(32 + columns.Count * 32 + 1);
        writer.Write(headerLen);
        writer.Write((short)(1 + columns.Sum(c => c.Length)));
        writer.Write((short)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write((byte)0);
        writer.Write((byte)0x86);
        writer.Write((short)0);

        foreach (var col in columns)
        {
            var nameBytes = new byte[11];
            var encoded = enc.GetBytes(col.Name);
            Array.Copy(encoded, nameBytes, Math.Min(encoded.Length, 10));
            writer.Write(nameBytes);
            writer.Write((byte)col.Type);
            writer.Write(0);
            writer.Write((byte)col.Length);
            writer.Write((byte)col.Decimals);
            writer.Write((short)0);
            writer.Write(new byte[12]);
        }
        writer.Write(FieldTerminator);

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
