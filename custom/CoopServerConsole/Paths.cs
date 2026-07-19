// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Fonza Launcher / Coop server console.  Docs: /custom/CLAUDE.md
// =============================================================================

using System;
using System.IO;

namespace CoopServerConsole
{
    /// <summary>
    /// Resolves game / module / log / save locations relative to where this exe runs.
    /// When deployed into Modules\Coop the layout is:
    ///   &lt;GameRoot&gt;\Modules\Coop\CoopServerConsole.exe   (this exe)
    ///   &lt;GameRoot&gt;\bin\Win64_Shipping_Client\Bannerlord.exe   (engine + logs written here)
    /// The game folder can also be set explicitly in settings (used when running the
    /// exe from its build output during development).
    /// </summary>
    internal static class Paths
    {
        public static string ExeDir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'); }
        }

        /// <summary>The engine bin dir. Prefers a configured game root, else resolves it from the exe location.</summary>
        public static string GameBinDir(string configuredGameRoot)
        {
            if (!string.IsNullOrWhiteSpace(configuredGameRoot))
            {
                var bin = Path.Combine(configuredGameRoot, "bin", "Win64_Shipping_Client");
                if (File.Exists(Path.Combine(bin, "Bannerlord.exe")))
                    return bin;
            }

            // Deployed case: exe sits in Modules\Coop -> up two folders is the game root.
            return Path.GetFullPath(Path.Combine(ExeDir, "..", "..", "bin", "Win64_Shipping_Client"));
        }

        public static string BannerlordExe(string configuredGameRoot)
        {
            return Path.Combine(GameBinDir(configuredGameRoot), "Bannerlord.exe");
        }

        public static string ServerLog(string configuredGameRoot)
        {
            return Path.Combine(GameBinDir(configuredGameRoot), "Coop_server.log");
        }

        public static string ClientLog(string configuredGameRoot)
        {
            return Path.Combine(GameBinDir(configuredGameRoot), "Coop_client.log");
        }

        public static string BootPatchesLog(string configuredGameRoot)
        {
            return Path.Combine(GameBinDir(configuredGameRoot), "BootPatches.log");
        }

        public static string ModuleDir(string configuredGameRoot)
        {
            return Path.GetFullPath(Path.Combine(GameBinDir(configuredGameRoot), "..", "..", "Modules", "Coop"));
        }

        public static string SavesDir()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(docs, "Mount and Blade II Bannerlord", "Game Saves");
        }

        public static string ConfigFile
        {
            get { return Path.Combine(ExeDir, "coopconsole.cfg"); }
        }
    }
}
