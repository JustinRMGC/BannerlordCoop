// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Dashboard page: host/join controls + tools. Binds to the reused GameLauncher.
// =============================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using CoopServerConsole.Mvvm;

namespace CoopServerConsole.ViewModels
{
    internal sealed class DashboardViewModel : ViewModelBase, ITickable
    {
        private readonly LauncherServices _svc;

        public DashboardViewModel(LauncherServices svc)
        {
            _svc = svc;
            StartServerCommand   = new RelayCommand(StartServer);
            StopServerCommand    = new RelayCommand(StopServer);
            RestartServerCommand = new RelayCommand(RestartServer);
            StartClientCommand   = new RelayCommand(StartClient);
            StopClientCommand    = new RelayCommand(StopClient);
            OpenSavesCommand     = new RelayCommand(() => OpenFolder(Paths.SavesDir()));
            OpenLogsCommand      = new RelayCommand(() => OpenFolder(_svc.Bin));
            UnblockCommand       = new RelayCommand(UnblockDlls);
            OnTick(0);
        }

        public ICommand StartServerCommand { get; }
        public ICommand StopServerCommand { get; }
        public ICommand RestartServerCommand { get; }
        public ICommand StartClientCommand { get; }
        public ICommand StopClientCommand { get; }
        public ICommand OpenSavesCommand { get; }
        public ICommand OpenLogsCommand { get; }
        public ICommand UnblockCommand { get; }

        private bool _serverRunning, _clientRunning;
        public bool CanStartServer => !_serverRunning;
        public bool CanStopServer => _serverRunning;
        public bool CanStartClient => !_clientRunning;
        public bool CanStopClient => _clientRunning;

        private string _serverKind = "stopped", _serverPill = "Stopped", _serverDetail = "Not running", _serverSummary = "";
        public string ServerKind { get => _serverKind; private set => Set(ref _serverKind, value); }
        public string ServerPill { get => _serverPill; private set => Set(ref _serverPill, value); }
        public string ServerDetail { get => _serverDetail; private set => Set(ref _serverDetail, value); }
        public string ServerSummary { get => _serverSummary; private set => Set(ref _serverSummary, value); }

        private string _clientKind = "stopped", _clientPill = "Stopped", _clientDetail = "";
        public string ClientKind { get => _clientKind; private set => Set(ref _clientKind, value); }
        public string ClientPill { get => _clientPill; private set => Set(ref _clientPill, value); }
        public string ClientDetail { get => _clientDetail; private set => Set(ref _clientDetail, value); }

        public void OnTick(long tick)
        {
            var l = _svc.Launcher;
            bool srv = l.IsRunning(l.ServerProcess);
            bool cli = l.IsRunning(l.ClientProcess);

            if (srv != _serverRunning)
            {
                _serverRunning = srv;
                OnPropertyChanged(nameof(CanStartServer));
                OnPropertyChanged(nameof(CanStopServer));
            }
            if (cli != _clientRunning)
            {
                _clientRunning = cli;
                OnPropertyChanged(nameof(CanStartClient));
                OnPropertyChanged(nameof(CanStopClient));
            }

            ServerKind = srv ? "running" : "stopped";
            ServerPill = srv ? "Running" : "Stopped";
            ServerDetail = srv ? $"pid {SafeId(l.ServerProcess)}" : "Not running";
            string save = string.IsNullOrWhiteSpace(_svc.Cfg.SaveName) ? "MP" : _svc.Cfg.SaveName;
            ServerSummary = $"Hosting save “{save}”   ·   {_svc.Cfg.Visibility}";

            ClientKind = cli ? "running" : "stopped";
            ClientPill = cli ? "Running" : "Stopped";
            ClientDetail = cli
                ? $"pid {SafeId(l.ClientProcess)}"
                : $"Not connected   ·   join in-game at {_svc.Cfg.ServerAddressHint}";
        }

        private static int SafeId(Process p) { try { return p?.Id ?? 0; } catch { return 0; } }

        private void StartServer()
        {
            try
            {
                _svc.Commit();
                _svc.Launcher.StartServer();
                _svc.ShowToast($"Server starting (pid {SafeId(_svc.Launcher.ServerProcess)}).", ToastKind.Success);
            }
            catch (Exception ex) { _svc.ShowToast(ex.Message, ToastKind.Error); }
            OnTick(0);
        }

        private void StopServer()
        {
            Task.Run(() =>
            {
                try { _svc.ShowToast(_svc.Launcher.StopServer(false), ToastKind.Info); }
                catch (Exception ex) { _svc.ShowToast(ex.Message, ToastKind.Error); }
            });
        }

        private void RestartServer()
        {
            Task.Run(async () =>
            {
                try
                {
                    _svc.ShowToast("Restarting server…", ToastKind.Info);
                    _svc.Launcher.StopServer(false);
                    await Task.Delay(1500);
                    _svc.Commit();
                    _svc.Launcher.StartServer();
                    _svc.ShowToast($"Server restarted (pid {SafeId(_svc.Launcher.ServerProcess)}).", ToastKind.Success);
                }
                catch (Exception ex) { _svc.ShowToast(ex.Message, ToastKind.Error); }
            });
        }

        private void StartClient()
        {
            try
            {
                _svc.Launcher.StartClient();
                _svc.ShowToast($"Client starting (pid {SafeId(_svc.Launcher.ClientProcess)}). Enter the host address in-game (127.0.0.1).", ToastKind.Success);
            }
            catch (Exception ex) { _svc.ShowToast(ex.Message, ToastKind.Error); }
            OnTick(0);
        }

        private void StopClient()
        {
            Task.Run(() =>
            {
                try { _svc.ShowToast(_svc.Launcher.StopClient(false), ToastKind.Info); }
                catch (Exception ex) { _svc.ShowToast(ex.Message, ToastKind.Error); }
            });
        }

        private void OpenFolder(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                {
                    _svc.ShowToast("Folder not found: " + path, ToastKind.Warning);
                    return;
                }
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
            }
            catch (Exception ex) { _svc.ShowToast(ex.Message, ToastKind.Error); }
        }

        private void UnblockDlls()
        {
            try
            {
                string dir = Paths.ModuleDir(_svc.Cfg.GameRoot);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    _svc.ShowToast("Module folder not found — set the game path in Settings.", ToastKind.Warning);
                    return;
                }
                var psi = new ProcessStartInfo("powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -Command \"Get-ChildItem -LiteralPath '{dir}' -Recurse | Unblock-File\"")
                { UseShellExecute = false, CreateNoWindow = true };
                Process.Start(psi);
                _svc.ShowToast("Unblocking mod DLLs…", ToastKind.Info);
            }
            catch (Exception ex) { _svc.ShowToast(ex.Message, ToastKind.Error); }
        }
    }
}
