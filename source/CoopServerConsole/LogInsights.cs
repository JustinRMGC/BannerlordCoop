using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace CoopServerConsole
{
    /// <summary>A digest of what a Coop server log currently says.</summary>
    internal sealed class ServerStatus
    {
        public bool LogFound;
        public string Build = "?";
        public string PublicIp = "?";
        public string Port = "?";
        public bool MapLoaded;
        public bool LobbyCreated;
        public string SaveLoadError;         // set if the log shows a save failed to load
        public int ErrorCount;
        public int WarningCount;
        public string LastActivity = "?";
        public readonly List<string> RecentErrors = new List<string>();
        public readonly List<string> ConnectionEvents = new List<string>();
    }

    /// <summary>
    /// Reads meaning out of the mod's Serilog output.
    /// Line format: <c>[(pid) HH:mm:ss LVL Source] message</c>
    /// </summary>
    internal static class LogInsights
    {
        private static readonly string[] Levels = { "ERR", "FTL", "WRN", "INF", "DBG", "VRB" };
        private static readonly Regex TimeRx = new Regex(@"\)\s(\d\d:\d\d:\d\d)\s", RegexOptions.Compiled);

        /// <summary>Extracts the 3-letter Serilog level from a line, or "" if none.</summary>
        public static string LevelOf(string line)
        {
            if (string.IsNullOrEmpty(line)) return "";
            int close = line.IndexOf(']');
            string head = close > 0 ? line.Substring(0, close) : line;
            foreach (var lv in Levels)
                if (head.IndexOf(" " + lv + " ", StringComparison.Ordinal) >= 0) return lv;
            return "";
        }

        private static string TimeOf(string line)
        {
            var m = TimeRx.Match(line);
            return m.Success ? m.Groups[1].Value : null;
        }

        public static ServerStatus Summarize(string logPath)
        {
            var s = new ServerStatus();
            if (!File.Exists(logPath)) return s;
            s.LogFound = true;

            string content;
            try
            {
                using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var sr = new StreamReader(fs))
                    content = sr.ReadToEnd();
            }
            catch { return s; }

            foreach (var raw in content.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;

                var t = TimeOf(line);
                if (t != null) s.LastActivity = t;

                var lvl = LevelOf(line);
                if (lvl == "ERR" || lvl == "FTL") { s.ErrorCount++; s.RecentErrors.Add(line); }
                else if (lvl == "WRN") s.WarningCount++;

                Capture(line, "BannerlordCoop build ", ref s.Build);
                CaptureToken(line, "publicIp=", ref s.PublicIp);
                Capture(line, "Server starting on port ", ref s.Port);

                if (line.IndexOf("changing to MapState", StringComparison.OrdinalIgnoreCase) >= 0) s.MapLoaded = true;
                if (line.IndexOf("Steam lobby", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    line.IndexOf("created", StringComparison.OrdinalIgnoreCase) >= 0) s.LobbyCreated = true;

                int fail = line.IndexOf("Failed to load save with name", StringComparison.OrdinalIgnoreCase);
                if (fail >= 0) s.SaveLoadError = line.Substring(fail);

                if (IsConnectionEvent(line)) s.ConnectionEvents.Add(line);
            }

            TrimToLast(s.RecentErrors, 8);
            TrimToLast(s.ConnectionEvents, 12);
            return s;
        }

        // Connection / player lifecycle lines worth surfacing. Kept broad but excludes the DBG detour spam.
        private static bool IsConnectionEvent(string line)
        {
            if (LevelOf(line) == "DBG" || LevelOf(line) == "VRB") return false;
            string[] needles =
            {
                "Connection", "ConnectionLogic", "client connected", "client disconnected",
                "NetworkClientValidate", "ResolveCharacter", "CreateCharacter", "TransferSave",
                "PlayerCampaignEntered", "new player", "hero", "peer connected", "peer disconnected",
                "All players", "joined", "Disconnect"
            };
            foreach (var n in needles)
                if (line.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static void Capture(string line, string marker, ref string target)
        {
            int i = line.IndexOf(marker, StringComparison.Ordinal);
            if (i < 0) return;
            var rest = line.Substring(i + marker.Length).Trim();
            // stop at first space for single-token values that may be followed by more text
            int sp = rest.IndexOf(' ');
            target = sp > 0 ? rest.Substring(0, sp) : rest;
        }

        private static void CaptureToken(string line, string marker, ref string target)
        {
            int i = line.IndexOf(marker, StringComparison.Ordinal);
            if (i < 0) return;
            var rest = line.Substring(i + marker.Length);
            int end = 0;
            while (end < rest.Length && !char.IsWhiteSpace(rest[end])) end++;
            if (end > 0) target = rest.Substring(0, end);
        }

        private static void TrimToLast(List<string> list, int keep)
        {
            if (list.Count > keep) list.RemoveRange(0, list.Count - keep);
        }
    }
}
