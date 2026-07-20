// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Players page: live connected-players table + admin actions. Reads the bridge
// snapshot (svc.LastSnapshot) each shared tick and talks to the in-game bridge
// via ControlChannel.SendCommand. Mirrors DashboardViewModel's conventions.
// =============================================================================

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using CoopServerConsole.Mvvm;

namespace CoopServerConsole.ViewModels
{
    /// <summary>One connected-player row. Fields update in place so the DataGrid
    /// keeps its selection/scroll across refreshes.</summary>
    public sealed class PlayerRow : ViewModelBase
    {
        public string ControllerId { get; set; } = "";   // identity; set once

        private int _index;
        public int Index { get => _index; set => Set(ref _index, value); }

        private string _name = "";
        public string Name { get => _name; set => Set(ref _name, value); }

        private string _clan = "";
        public string Clan { get => _clan; set => Set(ref _clan, value); }

        private string _state = "";
        public string State { get => _state; set => Set(ref _state, value); }

        // status-kind for the dot ("running" | "warn" | "muted"); fed through StatusBrush.
        private string _kind = "muted";
        public string Kind { get => _kind; set => Set(ref _kind, value); }
    }

    internal sealed class PlayersViewModel : ViewModelBase, ITickable
    {
        private readonly LauncherServices _svc;

        internal PlayersViewModel(LauncherServices svc)
        {
            _svc = svc;

            SaveCommand   = new RelayCommand(() => Send("save",    "Saving game…"));
            PauseCommand  = new RelayCommand(() => Send("pause",   "Pausing…"));
            ResumeCommand = new RelayCommand(() => Send("resume",  "Resuming…"));
            FastCommand   = new RelayCommand(() => Send("speed 2", "Setting speed to 2×…"));
            MenuCommand   = new RelayCommand(ReturnToMenu);

            RenameCommand = new RelayCommand(() => DoRename(SelectedPlayer));
            KickCommand   = new RelayCommand(() => DoKick(SelectedPlayer));

            RenameRowCommand = new RelayCommand(o => { if (o is PlayerRow r) { SelectedPlayer = r; DoRename(r); } });
            KickRowCommand   = new RelayCommand(o => { if (o is PlayerRow r) { SelectedPlayer = r; DoKick(r); } });

            OnTick(0);
        }

        // ---------------- session / player commands ----------------
        public ICommand SaveCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand ResumeCommand { get; }
        public ICommand FastCommand { get; }
        public ICommand MenuCommand { get; }
        public ICommand RenameCommand { get; }
        public ICommand KickCommand { get; }
        public ICommand RenameRowCommand { get; }
        public ICommand KickRowCommand { get; }

        // ---------------- bound state ----------------
        public ObservableCollection<PlayerRow> Players { get; } = new ObservableCollection<PlayerRow>();

        private PlayerRow _selectedPlayer;
        public PlayerRow SelectedPlayer { get => _selectedPlayer; set => Set(ref _selectedPlayer, value); }

        private bool _bridgeLive;
        public bool BridgeLive { get => _bridgeLive; private set => Set(ref _bridgeLive, value); }

        private string _infoText = "";
        public string InfoText { get => _infoText; private set => Set(ref _infoText, value); }

        private bool _emptyVisible = true;
        public bool EmptyVisible { get => _emptyVisible; private set => Set(ref _emptyVisible, value); }

        private string _emptyText = "";
        public string EmptyText { get => _emptyText; private set => Set(ref _emptyText, value); }

        private string _emptyKind = "muted"; // "warn" (amber) | "muted"
        public string EmptyKind { get => _emptyKind; private set => Set(ref _emptyKind, value); }

        // ---------------- the shared 500 ms tick ----------------
        public void OnTick(long tick)
        {
            var snap = _svc.LastSnapshot ?? new StatusSnapshot();

            if (!snap.BridgeLive)
            {
                BridgeLive = false;
                InfoText = "";
                if (Players.Count > 0) Players.Clear();
                SelectedPlayer = null;

                EmptyVisible = true;
                if (snap.StatusFilePresent)
                {
                    EmptyKind = "warn";
                    EmptyText = "Server bridge not responding — rebuild the mod so the bridge ships.";
                }
                else
                {
                    EmptyKind = "muted";
                    EmptyText = "No live server. Start & host a server (with the updated mod) to see players.";
                }
                return;
            }

            BridgeLive = true;
            InfoText = BuildInfo(snap);
            Reconcile(snap.Players);

            if (Players.Count == 0)
            {
                EmptyVisible = true;
                EmptyKind = "muted";
                EmptyText = "No players connected yet.";
            }
            else
            {
                EmptyVisible = false;
            }
        }

        private static string BuildInfo(StatusSnapshot s)
        {
            string port = (string.IsNullOrEmpty(s.Port) || s.Port == "?") ? "" : "   ·   port " + s.Port;
            return $"{s.State}   ·   save {s.Save}{port}   ·   {s.Players.Count} player(s)";
        }

        /// <summary>Sync the row collection to the snapshot in place, preserving the
        /// selected row (matched by ControllerId) and avoiding a full rebuild/flicker.</summary>
        private void Reconcile(List<PlayerInfo> players)
        {
            string selId = SelectedPlayer?.ControllerId;

            // Drop rows that are no longer present.
            var present = new HashSet<string>();
            foreach (var p in players) present.Add(p.ControllerId ?? "");
            for (int i = Players.Count - 1; i >= 0; i--)
                if (!present.Contains(Players[i].ControllerId)) Players.RemoveAt(i);

            // Add / update / reorder to match snapshot order.
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                string id = p.ControllerId ?? "";

                int cur = -1;
                for (int j = 0; j < Players.Count; j++)
                    if (Players[j].ControllerId == id) { cur = j; break; }

                PlayerRow row;
                if (cur < 0)
                {
                    row = new PlayerRow { ControllerId = id };
                    Players.Insert(i <= Players.Count ? i : Players.Count, row);
                }
                else
                {
                    row = Players[cur];
                    if (cur != i) Players.Move(cur, i);
                }

                row.Index = i + 1;
                row.Name  = string.IsNullOrWhiteSpace(p.Name) ? "(connecting)" : p.Name;
                row.Clan  = string.IsNullOrWhiteSpace(p.Clan) ? "—" : p.Clan;
                row.State = string.IsNullOrWhiteSpace(p.State) ? "—" : p.State;
                row.Kind  = DotKind(p.State, p.Name);
            }

            // Restore selection if the previously selected row was replaced.
            if ((SelectedPlayer == null || !Players.Contains(SelectedPlayer)) && !string.IsNullOrEmpty(selId))
                foreach (var r in Players)
                    if (r.ControllerId == selId) { SelectedPlayer = r; break; }
        }

        private static string DotKind(string state, string name)
        {
            string s = (state ?? "").Trim().ToLowerInvariant();
            if (s == "playing" || s == "running") return "running";
            if (s == "joining" || s == "connecting" || string.IsNullOrWhiteSpace(name)) return "warn";
            return "muted";
        }

        // ---------------- player actions ----------------
        private void DoRename(PlayerRow row)
        {
            if (row == null) { _svc.ShowToast("Select a player row first.", ToastKind.Warning); return; }

            string oldName = row.Name;
            string name = _svc.Prompt("Rename player", $"New name for “{oldName}”:", oldName);
            if (!string.IsNullOrWhiteSpace(name) && name != oldName)
                Send($"rename {row.ControllerId} {name}", $"Renaming to “{name}”…");
        }

        private void DoKick(PlayerRow row)
        {
            if (row == null) { _svc.ShowToast("Select a player row first.", ToastKind.Warning); return; }

            if (_svc.Confirm("Kick player", $"Kick “{row.Name}”?", danger: true))
                Send($"kick {row.ControllerId}", $"Kicking “{row.Name}”…");
        }

        private void ReturnToMenu()
        {
            if (_svc.Confirm("Return to main menu",
                             "Return the server to the main menu? This disconnects everyone.",
                             danger: true))
                Send("menu", "Returning to main menu…");
        }

        /// <summary>Write a command to the bridge on a background thread, reporting the ack via toast.</summary>
        private void Send(string command, string optimistic)
        {
            if (!BridgeLive) { _svc.ShowToast("Server bridge is not live.", ToastKind.Warning); return; }

            _svc.ShowToast(optimistic, ToastKind.Info);
            Task.Run(() =>
            {
                bool ok = false;
                string msg = "";
                try { ok = ControlChannel.SendCommand(_svc.Bin, command, out msg); }
                catch (Exception ex) { msg = ex.Message; }

                if (string.IsNullOrWhiteSpace(msg)) msg = ok ? "Done." : "Command failed.";
                _svc.ShowToast(msg, ok ? ToastKind.Success : ToastKind.Error);
            });
        }
    }
}
