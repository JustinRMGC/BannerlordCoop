// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Fonza Launcher / Coop server console.  Docs: /custom/CLAUDE.md
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CoopServerConsole
{
    /// <summary>
    /// Incrementally reads new complete lines appended to a log file. Opens the file with
    /// shared read/write/delete access because Serilog holds it open, and resets when the
    /// file shrinks (the mod deletes+recreates its log each startup, and compacts on size).
    /// </summary>
    internal sealed class LogFollower
    {
        private readonly string path;
        private long pos;
        private string carry = "";

        public LogFollower(string path, bool startAtEnd)
        {
            this.path = path;
            if (startAtEnd)
            {
                try { if (File.Exists(path)) pos = new FileInfo(path).Length; } catch { }
            }
        }

        public bool FileExists { get { try { return File.Exists(path); } catch { return false; } } }

        /// <summary>Returns any complete lines appended since the previous call.</summary>
        public List<string> Poll()
        {
            var result = new List<string>();
            try
            {
                if (!File.Exists(path)) { pos = 0; carry = ""; return result; }

                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (fs.Length < pos) { pos = 0; carry = ""; } // rotated / truncated / recreated
                    if (fs.Length <= pos) return result;

                    long available = fs.Length - pos;
                    fs.Seek(pos, SeekOrigin.Begin);
                    var buf = new byte[available];
                    int read = fs.Read(buf, 0, (int)available);
                    pos += read;

                    var text = carry + Encoding.UTF8.GetString(buf, 0, read);
                    int start = 0, idx;
                    while ((idx = text.IndexOf('\n', start)) >= 0)
                    {
                        result.Add(text.Substring(start, idx - start).TrimEnd('\r'));
                        start = idx + 1;
                    }
                    carry = text.Substring(start);
                }
            }
            catch { /* transient IO; try again next poll */ }
            return result;
        }
    }
}
