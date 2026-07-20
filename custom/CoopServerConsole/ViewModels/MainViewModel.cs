// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Shell view-model: left-rail nav, current page, live rail/header status, toast.
// Owns the single 500 ms DispatcherTimer that drives all live updates.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using CoopServerConsole.Mvvm;

namespace CoopServerConsole.ViewModels
{
    /// <summary>One left-rail navigation entry.</summary>
    public sealed class NavEntry
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public string Glyph { get; set; }
        public ViewModelBase Page { get; set; }
    }

    internal sealed class MainViewModel : ViewModelBase
    {
        private readonly LauncherServices _svc;
        private readonly DispatcherTimer _timer;
        private readonly List<ITickable> _tickables = new List<ITickable>();
        private DispatcherTimer _toastRevert;
        private long _tick;

        public ObservableCollection<NavEntry> Nav { get; } = new ObservableCollection<NavEntry>();

        public MainViewModel()
        {
            var cfg = AppConfig.Load();
            _svc = new LauncherServices
            {
                Cfg = cfg,
                Launcher = new GameLauncher(cfg),
                Version = VersionString(),
                Toast = ShowToast
            };

            var dashboard = new DashboardViewModel(_svc);
            Nav.Add(new NavEntry { Key = "home",      Label = "Home",       Glyph = Ico("Icon.Home"),     Page = dashboard });
            Nav.Add(new NavEntry { Key = "serverlog", Label = "Server Log", Glyph = Ico("Icon.Server"),   Page = new LogViewModel(_svc, true) });
            Nav.Add(new NavEntry { Key = "clientlog", Label = "Client Log", Glyph = Ico("Icon.Client"),   Page = new LogViewModel(_svc, false) });
            Nav.Add(new NavEntry { Key = "players",   Label = "Players",    Glyph = Ico("Icon.Players"),  Page = new PlayersViewModel(_svc) });
            Nav.Add(new NavEntry { Key = "settings",  Label = "Settings",   Glyph = Ico("Icon.Settings"), Page = new SettingsViewModel(_svc) });

            foreach (var n in Nav)
                if (n.Page is ITickable t) _tickables.Add(t);

            SelectedNav = Nav[0];

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += (s, e) => Tick();
            _timer.Start();
            Tick();
        }

        public string BrandVersion => _svc.Version;

        private static string Ico(string key) => Application.Current?.TryFindResource(key) as string ?? "";

        private static string VersionString()
        {
            try
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return v == null ? "v0.0.0" : $"v{v.Major}.{v.Minor}.{v.Build}";
            }
            catch { return "v0.0.0"; }
        }

        // ---------------- nav / current page ----------------
        private NavEntry _selectedNav;
        public NavEntry SelectedNav
        {
            get => _selectedNav;
            set
            {
                if (Set(ref _selectedNav, value) && value != null)
                {
                    CurrentPage = value.Page;
                    HeaderTitle = value.Label;
                }
            }
        }

        private ViewModelBase _currentPage;
        public ViewModelBase CurrentPage { get => _currentPage; private set => Set(ref _currentPage, value); }

        private string _headerTitle = "Home";
        public string HeaderTitle { get => _headerTitle; private set => Set(ref _headerTitle, value); }

        public void SelectByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            key = key.Trim().ToLowerInvariant().Replace(" ", "");
            foreach (var n in Nav)
                if (n.Key == key) { SelectedNav = n; return; }
        }

        // ---------------- rail status ----------------
        private string _serverKind = "stopped", _serverText = "stopped";
        public string ServerKind { get => _serverKind; private set => Set(ref _serverKind, value); }
        public string ServerText { get => _serverText; private set => Set(ref _serverText, value); }

        private string _clientKind = "stopped", _clientText = "stopped";
        public string ClientKind { get => _clientKind; private set => Set(ref _clientKind, value); }
        public string ClientText { get => _clientText; private set => Set(ref _clientText, value); }

        private string _engineKind = "stopped", _engineText = "0 processes";
        public string EngineKind { get => _engineKind; private set => Set(ref _engineKind, value); }
        public string EngineText { get => _engineText; private set => Set(ref _engineText, value); }

        // ---------------- header status strip ----------------
        private string _hdrKind = "stopped", _hdrPill = "Server stopped", _hdrDetail = "";
        public string HeaderKind { get => _hdrKind; private set => Set(ref _hdrKind, value); }
        public string HeaderPill { get => _hdrPill; private set => Set(ref _hdrPill, value); }
        public string HeaderDetail
        {
            get => _hdrDetail;
            private set { if (Set(ref _hdrDetail, value)) OnPropertyChanged(nameof(HeaderDetailVisible)); }
        }
        public bool HeaderDetailVisible => !string.IsNullOrEmpty(_hdrDetail);

        // ---------------- toast / footer ----------------
        private string _toastText = "Ready.";
        public string ToastText { get => _toastText; private set => Set(ref _toastText, value); }
        private string _toastKind = "info";
        public string ToastKindStr { get => _toastKind; private set => Set(ref _toastKind, value); }

        public void ShowToast(string msg, ToastKind kind)
        {
            var d = Application.Current?.Dispatcher;
            if (d != null && !d.CheckAccess()) { d.BeginInvoke((Action)(() => ShowToast(msg, kind))); return; }

            ToastText = msg;
            ToastKindStr = kind == ToastKind.Success ? "running"
                         : kind == ToastKind.Error   ? "error"
                         : kind == ToastKind.Warning ? "warn" : "info";

            _toastRevert?.Stop();
            _toastRevert = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _toastRevert.Tick += (s, e) => { _toastRevert.Stop(); ToastText = "Ready."; ToastKindStr = "info"; };
            _toastRevert.Start();
        }

        // ---------------- the shared tick ----------------
        private void Tick()
        {
            _tick++;
            var l = _svc.Launcher;
            bool srv = l.IsRunning(l.ServerProcess);
            bool cli = l.IsRunning(l.ClientProcess);
            int eng = GameLauncher.BannerlordProcessCount();

            ServerKind = srv ? "running" : "stopped"; ServerText = srv ? "running" : "stopped";
            ClientKind = cli ? "running" : "stopped"; ClientText = cli ? "running" : "stopped";
            EngineKind = eng > 0 ? "info" : "stopped";
            EngineText = eng == 1 ? "1 process" : $"{eng} processes";

            StatusSnapshot snap = null;
            try { snap = ControlChannel.ReadStatus(_svc.Bin); } catch { }
            if (snap != null) _svc.LastSnapshot = snap;

            if (!srv)
            {
                HeaderKind = "stopped"; HeaderPill = "Server stopped"; HeaderDetail = "";
            }
            else if (snap != null && snap.BridgeLive)
            {
                HeaderKind = "running"; HeaderPill = "Running";
                string save = (string.IsNullOrWhiteSpace(snap.Save) || snap.Save == "?") ? (_svc.Cfg.SaveName ?? "") : snap.Save;
                string port = (string.IsNullOrEmpty(snap.Port) || snap.Port == "?") ? "" : $"   ·   port {snap.Port}";
                HeaderDetail = $"save “{save}”{port}   ·   {snap.Players.Count} player(s)";
            }
            else
            {
                HeaderKind = "warn"; HeaderPill = "Starting…"; HeaderDetail = "waiting for the server bridge";
            }

            for (int i = 0; i < _tickables.Count; i++)
            {
                try { _tickables[i].OnTick(_tick); } catch { }
            }
        }
    }
}
