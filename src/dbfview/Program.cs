using System.Text;

namespace dbfview;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.ToString(), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
    }
}
