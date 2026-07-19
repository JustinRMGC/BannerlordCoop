// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Fonza Launcher / Coop server console.  Docs: /custom/CLAUDE.md
// =============================================================================

using System;
using System.Windows.Forms;

namespace CoopServerConsole
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Headless construction check (used for build verification): new up the window and
            // dispose it without a message loop. Exit code signals success.
            if (args.Length > 0 && string.Equals(args[0], "--selftest", StringComparison.OrdinalIgnoreCase))
            {
                try { using (var f = new MainForm()) { } return 0; }
                catch { return 1; }
            }

            Native.EnableDarkMode();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Fonza Launcher — fatal error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            return 0;
        }
    }
}
