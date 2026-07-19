using System;
using System.Collections.Generic;
using System.Threading;

namespace CoopServerConsole
{
    /// <summary>Live, color-coded log viewer that follows a file until Esc.</summary>
    internal static class LogView
    {
        public static void Follow(string path, string title, AppConfig cfg)
        {
            Ui.Header(title);
            Ui.WriteLine("Following: " + path, ConsoleColor.DarkGray);
            RedrawHelp(cfg);
            Console.WriteLine(new string('-', 64));

            var follower = new LogFollower(path, startAtEnd: false);
            bool first = true;
            bool waitingNoted = false;

            while (true)
            {
                if (Ui.KeyAvailable())
                {
                    var key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Escape) return;
                    if (key == ConsoleKey.D)
                    {
                        cfg.HideDebugLogLines = !cfg.HideDebugLogLines;
                        cfg.Save();
                        Ui.WriteLine("  [filter] DBG/VRB now " + (cfg.HideDebugLogLines ? "HIDDEN" : "SHOWN"), ConsoleColor.DarkCyan);
                    }
                    else if (key == ConsoleKey.C)
                    {
                        Ui.Header(title);
                        Ui.WriteLine("Following: " + path, ConsoleColor.DarkGray);
                        RedrawHelp(cfg);
                        Console.WriteLine(new string('-', 64));
                    }
                }

                var lines = follower.Poll();
                if (lines.Count > 0)
                {
                    waitingNoted = false;
                    IEnumerable<string> toPrint = lines;
                    if (first && lines.Count > 200)
                        toPrint = lines.GetRange(lines.Count - 200, 200); // don't dump a whole 30MB backlog
                    first = false;
                    foreach (var l in toPrint) PrintLine(l, cfg);
                }
                else if (!follower.FileExists && !waitingNoted)
                {
                    Ui.WriteLine("(waiting for the log to appear — start the server/client)", ConsoleColor.DarkGray);
                    waitingNoted = true;
                }

                Thread.Sleep(150);
            }
        }

        private static void RedrawHelp(AppConfig cfg)
        {
            Ui.WriteLine("[Esc] back    [d] toggle DBG/VRB (" + (cfg.HideDebugLogLines ? "hidden" : "shown") + ")    [c] clear screen",
                ConsoleColor.DarkGray);
        }

        private static void PrintLine(string line, AppConfig cfg)
        {
            var level = LogInsights.LevelOf(line);
            if (cfg.HideDebugLogLines && (level == "DBG" || level == "VRB")) return;
            Ui.WriteLine(line, ColorFor(level));
        }

        private static ConsoleColor ColorFor(string level)
        {
            switch (level)
            {
                case "ERR": return ConsoleColor.Red;
                case "FTL": return ConsoleColor.Magenta;
                case "WRN": return ConsoleColor.Yellow;
                case "INF": return ConsoleColor.White;
                case "DBG": return ConsoleColor.DarkGray;
                case "VRB": return ConsoleColor.DarkGray;
                default: return ConsoleColor.Gray;
            }
        }
    }
}
