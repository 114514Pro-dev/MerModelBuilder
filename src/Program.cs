using System;
using System.Windows.Forms;
using MerBuilder.UI;

namespace MerBuilder
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args != null && args.Length > 0 && (args[0] == "--cli" || args[0] == "--selftest"))
            {
                string[] rest = args;
                if (args[0] == "--cli")
                {
                    rest = new string[args.Length - 1];
                    Array.Copy(args, 1, rest, 0, rest.Length);
                }
                return Cli.Run(rest);
            }
            try { Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); }
            catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }
    }
}
