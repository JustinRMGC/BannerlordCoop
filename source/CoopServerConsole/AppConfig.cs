using System;
using System.Collections.Generic;
using System.IO;

namespace CoopServerConsole
{
    /// <summary>
    /// Simple key=value settings persisted next to the exe (coopconsole.cfg).
    /// Hand-rolled so the tool stays a single dependency-free exe.
    /// </summary>
    internal sealed class AppConfig
    {
        public string GameRoot = "";                    // optional override; empty = auto-resolve from exe location
        public string SaveName = "MP";                  // hard-coded server load target is "MP"
        public string Visibility = "public";            // public | friends_only | none
        public string Password = "";                    // optional server password
        public string ServerAddressHint = "127.0.0.1";  // shown to the user; client address is typed in-game
        public bool HideDebugLogLines = true;           // logs are flooded with DBG noise; hide by default
        public bool HideSyncNoise = true;               // hide the mod's routine sync-layer ERR spam by default

        public static AppConfig Load()
        {
            var cfg = new AppConfig();
            try
            {
                if (File.Exists(Paths.ConfigFile))
                {
                    foreach (var raw in File.ReadAllLines(Paths.ConfigFile))
                    {
                        var line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#")) continue;
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        cfg.Set(line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim());
                    }
                }
            }
            catch { /* fall back to defaults */ }
            return cfg;
        }

        private void Set(string key, string val)
        {
            switch (key.ToLowerInvariant())
            {
                case "gameroot": GameRoot = val; break;
                case "savename": SaveName = val; break;
                case "visibility": Visibility = NormalizeVisibility(val); break;
                case "password": Password = val; break;
                case "serveraddresshint": ServerAddressHint = val; break;
                case "hidedebugloglines": HideDebugLogLines = ParseBool(val, true); break;
                case "hidesyncnoise": HideSyncNoise = ParseBool(val, true); break;
            }
        }

        public static string NormalizeVisibility(string v)
        {
            if (v == null) return "public";
            switch (v.Trim().ToLowerInvariant())
            {
                case "public": return "public";
                case "friends_only":
                case "friends":
                case "friendsonly": return "friends_only";
                case "none": return "none";
                default: return "public";
            }
        }

        private static bool ParseBool(string v, bool def)
        {
            return bool.TryParse(v, out var b) ? b : def;
        }

        public void Save()
        {
            try
            {
                var lines = new List<string>
                {
                    "# CoopServerConsole settings",
                    "gameRoot=" + GameRoot,
                    "saveName=" + SaveName,
                    "visibility=" + Visibility,
                    "password=" + Password,
                    "serverAddressHint=" + ServerAddressHint,
                    "hideDebugLogLines=" + HideDebugLogLines,
                    "hideSyncNoise=" + HideSyncNoise,
                };
                File.WriteAllLines(Paths.ConfigFile, lines);
            }
            catch
            {
                // Best effort — a failed settings write must not crash the app.
            }
        }
    }
}
