using System.Text;

namespace dbfview.Readers;

public static class EncodingDetector
{
    public static Encoding Detect(byte[] sample, int sampleLength)
    {
        if (sampleLength < 3) return Encoding.GetEncoding("gbk");

        if (sample[0] == 0xEF && sample[1] == 0xBB && sample[2] == 0xBF)
            return Encoding.UTF8;

        var utf8 = Encoding.UTF8;
        try
        {
            var text = utf8.GetString(sample, 0, sampleLength);
            if (text.Contains('\uFFFD'))
                return Encoding.GetEncoding("gbk");
            return utf8;
        }
        catch
        {
            return Encoding.GetEncoding("gbk");
        }
    }
}
