using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CoopServerConsole
{
    /// <summary>
    /// Launches and tracks the Bannerlord engine as server / client child processes,
    /// using the same arguments as the start-*.bat launchers.
    /// </summary>
    internal sealed class GameLauncher
    {
        // Same module list + order the start-*.bat files use.
        public const string ModuleList = "_MODULES_*Native*SandBoxCore*SandBox*StoryMode*Coop*_MODULES_";

        private readonly AppConfig cfg;
        private Process _server;
        private Process _client;

        public GameLauncher(AppConfig cfg) { this.cfg = cfg; }

        public Process ServerProcess { get { return _server; } }
        public Process ClientProcess { get { return _client; } }

        public bool IsRunning(Process p)
        {
            try { return p != null && !p.HasExited; }
            catch { return false; }
        }

        public static int BannerlordProcessCount()
        {
            try { return Process.GetProcessesByName("Bannerlord").Length; }
            catch { return 0; }
        }

        private string ResolveExeOrThrow()
        {
            var exe = Paths.BannerlordExe(cfg.GameRoot);
            if (!File.Exists(exe))
            {
                throw new FileNotFoundException(
                    "Bannerlord.exe not found at:\n  " + exe +
                    "\n\nRun this exe from inside the game's Modules\\Coop folder, " +
                    "or set the game folder in Settings.");
            }
            return exe;
        }

        /// <summary>Starts the server, unattended-hosting the configured save (auto-loads via /coopsave).</summary>
        public void StartServer()
        {
            if (IsRunning(_server))
                throw new InvalidOperationException("Server is already running (pid " + _server.Id + ").");

            var exe = ResolveExeOrThrow();
            var bin = Path.GetDirectoryName(exe);

            var save = string.IsNullOrWhiteSpace(cfg.SaveName) ? "MP" : cfg.SaveName;
            var args = new StringBuilder();
            args.Append("/singleplayer /server ").Append(Quote(ModuleList));
            args.Append(" /coopsave ").Append(Quote(save));
            args.Append(" /coopvisibility ").Append(AppConfig.NormalizeVisibility(cfg.Visibility));
            if (!string.IsNullOrEmpty(cfg.Password))
                args.Append(" /cooppassword ").Append(Quote(cfg.Password));

            _server = Launch(exe, bin, args.ToString());
        }

        /// <summary>Starts the client. The host address is entered in-game on the join screen.</summary>
        public void StartClient()
        {
            if (IsRunning(_client))
                throw new InvalidOperationException("Client is already running (pid " + _client.Id + ").");

            var exe = ResolveExeOrThrow();
            var bin = Path.GetDirectoryName(exe);
            var args = "/singleplayer /client " + Quote(ModuleList);
            _client = Launch(exe, bin, args);
        }

        public string StopServer(bool force) { return Stop(ref _server, "Server", force); }
        public string StopClient(bool force) { return Stop(ref _client, "Client", force); }

        private string Stop(ref Process p, string name, bool force)
        {
            if (!IsRunning(p)) { p = null; return name + " is not running."; }
            int pid = p.Id;
            try
            {
                if (!force && p.CloseMainWindow() && p.WaitForExit(8000))
                    return name + " closed gracefully (pid " + pid + ").";

                p.Kill();
                p.WaitForExit(5000);
                return force
                    ? name + " force-killed (pid " + pid + ")."
                    : name + " did not close gracefully; killed (pid " + pid + ").";
            }
            catch (Exception ex)
            {
                return "Could not stop " + name + ": " + ex.Message;
            }
            finally
            {
                if (!IsRunning(p)) p = null;
            }
        }

        private static Process Launch(string exe, string workingDir, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = workingDir, // logs are written to the process CWD, like the .bat's "cd"
                UseShellExecute = true,        // launch detached in its own window, like start
            };
            return Process.Start(psi);
        }

        /// <summary>Quotes a single Windows argument (handles spaces and the module token safely).</summary>
        public static string Quote(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return "\"\"";
            bool needs = false;
            foreach (var c in arg)
            {
                if (c == ' ' || c == '\t' || c == '"') { needs = true; break; }
            }
            if (!needs) return arg;
            return "\"" + arg.Replace("\"", "\\\"") + "\"";
        }
    }
}
