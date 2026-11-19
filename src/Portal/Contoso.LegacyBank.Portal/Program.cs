using System;
using System.Windows.Forms;
using Contoso.LegacyBank.Shared;

namespace Contoso.LegacyBank.Portal
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            AppPaths.EnsureAll();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
