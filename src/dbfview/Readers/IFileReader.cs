using System.Data;
using System.Text;

namespace dbfview.Readers;

public interface IFileReader
{
    DataTable Read(string path, Encoding encoding);
    Encoding DetectEncoding(string path);
    string[] Extensions { get; }
    string FormatName { get; }
    bool SupportsSave { get; }
}
