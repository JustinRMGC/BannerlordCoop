// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Fonza Launcher / Coop server console.  Docs: /custom/CLAUDE.md
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace CoopServerConsole
{
    internal sealed class PlayerInfo
    {
        public string ControllerId = "";
        public string Name = "";
        public string Clan = "";
        public string State = "";
    }

    /// <summary>A parsed snapshot of the status file the in-game bridge writes.</summary>
    internal sealed class StatusSnapshot
    {
        public bool BridgeLive;          // status file present AND fresh
        public bool StatusFilePresent;
        public string Updated = "?";
        public string State = "?";
        public string Save = "?";
        public string Port = "?";
        public string PublicIp = "?";
        public double AgeSeconds = double.MaxValue;
        public readonly List<PlayerInfo> Players = new List<PlayerInfo>();
    }

    /// <summary>
    /// Console side of the file-based control channel shared with the in-game bridge.
    /// The mod writes a status file (players + server info); the console writes a command
    /// file for admin actions and reads back an ack file. Same-machine only (files live in
    /// the engine bin dir next to the logs).
    /// </summary>
    internal static class ControlChannel
    {
        public const string StatusFile = "coop_console_status.txt";
        public const string CommandFile = "coop_console_command.txt";
        public const string AckFile = "coop_console_ack.txt";

        // Status is considered "live" if written within this many seconds (bridge writes ~1/s).
        private const double FreshnessSeconds = 6.0;

        public static string StatusPath(string bin) { return Path.Combine(bin, StatusFile); }
        public static string CommandPath(string bin) { return Path.Combine(bin, CommandFile); }
        public static string AckPath(string bin) { return Path.Combine(bin, AckFile); }

        public static StatusSnapshot ReadStatus(string bin)
        {
            var snap = new StatusSnapshot();
            var path = StatusPath(bin);
            try
            {
                if (!File.Exists(path)) return snap;
                snap.StatusFilePresent = true;
                snap.AgeSeconds = (DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalSeconds;
                snap.BridgeLive = snap.AgeSeconds <= FreshnessSeconds;

                foreach (var raw in SafeReadLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    var k = line.Substring(0, eq).Trim().ToLowerInvariant();
                    var v = line.Substring(eq + 1).Trim();
                    switch (k)
                    {
                        case "updated": snap.Updated = v; break;
                        case "state": snap.State = v; break;
                        case "save": snap.Save = v; break;
                        case "port": snap.Port = v; break;
                        case "publicip": snap.PublicIp = v; break;
                        case "player":
                            var parts = v.Split('|');
                            var p = new PlayerInfo();
                            if (parts.Length > 0) p.ControllerId = parts[0];
                            if (parts.Length > 1) p.Name = parts[1];
                            if (parts.Length > 2) p.Clan = parts[2];
                            if (parts.Length > 3) p.State = parts[3];
                            snap.Players.Add(p);
                            break;
                    }
                }
            }
            catch { }
            return snap;
        }

        /// <summary>Writes one command and waits up to timeoutMs for the mod's ack. Returns success.</summary>
        public static bool SendCommand(string bin, string command, out string message, int timeoutMs = 5000)
        {
            message = "";
            try
            {
                var ack = AckPath(bin);
                try { if (File.Exists(ack)) File.Delete(ack); } catch { }

                File.WriteAllText(CommandPath(bin), "cmd=" + command + Environment.NewLine);

                var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                while (DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(150);
                    if (!File.Exists(ack)) continue;

                    Thread.Sleep(60); // let the writer finish flushing
                    bool ok = false;
                    foreach (var raw in SafeReadLines(ack))
                    {
                        var line = raw.Trim();
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        var k = line.Substring(0, eq).Trim().ToLowerInvariant();
                        var val = line.Substring(eq + 1).Trim();
                        if (k == "ok") ok = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                        else if (k == "msg") message = val;
                    }
                    try { File.Delete(ack); } catch { }
                    return ok;
                }

                message = "No response from the server. Is it running and hosting (bridge active)?";
                return false;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private static string[] SafeReadLines(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var sr = new StreamReader(fs))
                {
                    var list = new List<string>();
                    string line;
                    while ((line = sr.ReadLine()) != null) list.Add(line);
                    return list.ToArray();
                }
            }
            catch { return new string[0]; }
        }
    }
}
