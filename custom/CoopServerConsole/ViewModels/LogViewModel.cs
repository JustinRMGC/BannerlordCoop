// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Live log page VM: tails Coop_server.log / Coop_client.log, colorizes by level,
// and drives live search + level/debug/noise filters. The shell creates two
// instances (server + client) and maps both to LogView via DataTemplate.
// Mirrors DashboardViewModel/PlayersViewModel conventions.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using CoopServerConsole.Mvvm;

namespace CoopServerConsole.ViewModels
{
    /// <summary>One rendered log row: the raw text + its level-colored brush.
    /// Level/IsNoise are precomputed so the live view filter stays cheap. Values
    /// never change after construction, so plain auto-properties are enough
    /// (WPF binds to properties, not fields).</summary>
    internal sealed class LogLine
    {
        public string Text { get; set; }
        public Brush Brush { get; set; }
        public string Level { get; set; }   // ERR/FTL/WRN/INF/DBG/VRB/"" — for the level chips
        public bool IsNoise { get; set; }    // LogInsights.IsSyncNoise(Text), precomputed
    }

    internal sealed class LogViewModel : ViewModelBase, ITickable
    {
        private const int MaxLines = 5000;      // hard cap on the backing buffer
        private const int TrimTo = 4500;        // trim target so trimming happens in batches
        private const int FirstPollCap = 400;   // avoid dumping the whole existing log at once

        private readonly LauncherServices _svc;
        private readonly LogFollower _follower;
        private readonly ICollectionView _view;
        private bool _primed;                   // set once the first non-empty batch is seen

        // Level brushes, resolved once from the app theme.
        private readonly Brush _bInf, _bMuted, _bWrn, _bErr, _bFtl;

        internal LogViewModel(LauncherServices svc, bool isServer)
        {
            _svc = svc;

            _bInf   = Res("LogInfBrush");
            _bMuted = Res("LogMutedBrush");
            _bWrn   = Res("LogWrnBrush");
            _bErr   = Res("LogErrBrush");
            _bFtl   = Res("LogFtlBrush");

            string path = isServer ? Paths.ServerLog(svc.Cfg.GameRoot) : Paths.ClientLog(svc.Cfg.GameRoot);
            _follower = new LogFollower(path, startAtEnd: false);

            Lines = new ObservableCollection<LogLine>();
            _view = CollectionViewSource.GetDefaultView(Lines);
            _view.Filter = o => Passes(o as LogLine);

            ClearCommand = new RelayCommand(() => Lines.Clear());
            EmptyHint = isServer ? "No server log output yet." : "No client log output yet.";

            OnTick(0);
        }

        // ---------------- bound state ----------------
        public ObservableCollection<LogLine> Lines { get; }
        public ICollectionView LinesView => _view;
        public string EmptyHint { get; }
        public ICommand ClearCommand { get; }

        // ---- search (live, case-insensitive Contains) ----
        private string _search = "";
        public string SearchText
        {
            get => _search;
            set { if (Set(ref _search, value ?? "")) _view.Refresh(); }
        }

        // ---- level chips (pure view filter; default all on) ----
        private bool _showInf = true, _showWrn = true, _showErr = true;
        public bool ShowInf { get => _showInf; set { if (Set(ref _showInf, value)) _view.Refresh(); } }
        public bool ShowWrn { get => _showWrn; set { if (Set(ref _showWrn, value)) _view.Refresh(); } }
        public bool ShowErr { get => _showErr; set { if (Set(ref _showErr, value)) _view.Refresh(); } }

        // ---- debug / noise chips: flip the persisted cfg flags live (they gate
        //      both ingestion and the view, so toggling off also hides already-buffered lines). ----
        public bool ShowDebug
        {
            get => !_svc.Cfg.HideDebugLogLines;
            set { _svc.Cfg.HideDebugLogLines = !value; _svc.Cfg.Save(); OnPropertyChanged(); _view.Refresh(); }
        }
        public bool ShowNoise
        {
            get => !_svc.Cfg.HideSyncNoise;
            set { _svc.Cfg.HideSyncNoise = !value; _svc.Cfg.Save(); OnPropertyChanged(); _view.Refresh(); }
        }

        // ---- wrap toggle ----
        private bool _wrap = true;
        public bool WrapText { get => _wrap; set => Set(ref _wrap, value); }

        // ---------------- the shared 500 ms tick ----------------
        public void OnTick(long tick)
        {
            List<string> batch;
            try { batch = _follower.Poll(); }
            catch { return; }
            if (batch == null || batch.Count == 0) return;

            // First real dump: keep only the tail so we don't flood on open.
            if (!_primed)
            {
                if (batch.Count > FirstPollCap)
                    batch = batch.GetRange(batch.Count - FirstPollCap, FirstPollCap);
                _primed = true;
            }

            foreach (var raw in batch)
            {
                string level = LogInsights.LevelOf(raw);

                // Ingestion filter: keep the buffer full of *useful* lines (the log is
                // flooded with DBG/sync noise). Chips flip these cfg flags live.
                if (_svc.Cfg.HideDebugLogLines && (level == "DBG" || level == "VRB")) continue;
                bool noise = LogInsights.IsSyncNoise(raw);
                if (_svc.Cfg.HideSyncNoise && noise) continue;

                Lines.Add(new LogLine { Text = raw, Brush = BrushFor(level), Level = level, IsNoise = noise });
            }

            // Bounded buffer: trim oldest in a batch when the cap is exceeded.
            if (Lines.Count > MaxLines)
            {
                int remove = Lines.Count - TrimTo;
                for (int i = 0; i < remove; i++) Lines.RemoveAt(0);
            }
        }

        // ---------------- live view filter ----------------
        private bool Passes(LogLine ln)
        {
            if (ln == null) return false;

            // Mirror the ingestion gates so turning a chip off hides already-buffered lines too.
            if (_svc.Cfg.HideSyncNoise && ln.IsNoise) return false;
            if (_svc.Cfg.HideDebugLogLines && (ln.Level == "DBG" || ln.Level == "VRB")) return false;

            if (_search.Length > 0 && ln.Text.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            switch (ln.Level)
            {
                case "ERR":
                case "FTL": return _showErr;
                case "WRN": return _showWrn;
                case "DBG":
                case "VRB": return true;   // gated by the Debug chip above, not the level chips
                default:    return _showInf; // INF or ""
            }
        }

        private Brush BrushFor(string level)
        {
            switch (level)
            {
                case "ERR": return _bErr;
                case "FTL": return _bFtl;
                case "WRN": return _bWrn;
                case "DBG":
                case "VRB": return _bMuted;
                default:    return _bInf;   // INF or ""
            }
        }

        private static Brush Res(string key)
            => Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }
}
