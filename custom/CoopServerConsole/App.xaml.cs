// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Fonza Launcher (WPF) — application bootstrap. Preserves the old CLI contract:
//   --selftest        construct the window and exit 0 (build verification)
//   --tab <name>      deep-link to a page (home|serverlog|clientlog|players|settings)
// =============================================================================

using System;
using System.Linq;
using System.Windows;
using CoopServerConsole.ViewModels;

namespace CoopServerConsole
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var args = Environment.GetCommandLineArgs();

            // Headless construction check (build verification): build the visual tree,
            // then exit. Success => 0, exception => 1. No window is shown.
            if (args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase)))
            {
                try { var _ = new MainWindow(); Shutdown(0); }
                catch { Shutdown(1); }
                return;
            }

            DispatcherUnhandledException += (s, dea) =>
            {
                MessageBox.Show(dea.Exception.ToString(), "Fonza Launcher — fatal error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                dea.Handled = true;
                Shutdown(1);
            };

            var win = new MainWindow();

            string tab = ReadTab(args);
            if (!string.IsNullOrEmpty(tab))
                (win.DataContext as MainViewModel)?.SelectByKey(tab);

            win.Show();
        }

        // --tab <name>  (value may contain spaces if quoted, e.g. --tab "server log")
        private static string ReadTab(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], "--tab", StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }
    }
}
