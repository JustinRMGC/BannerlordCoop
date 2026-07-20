// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Settings page: edits the shared AppConfig. The two log toggles persist
// immediately; text fields + visibility persist via "Save settings" (svc.Commit()).
// =============================================================================

using System.Windows.Input;
using CoopServerConsole.Mvvm;

namespace CoopServerConsole.ViewModels
{
    internal sealed class SettingsViewModel : ViewModelBase
    {
        private readonly LauncherServices _svc;

        internal SettingsViewModel(LauncherServices svc)
        {
            _svc = svc;
            SaveCommand = new RelayCommand(SaveSettings);
        }

        // ------------------------------ Save file ------------------------------
        // Bind THROUGH to the shared cfg field; no trim/normalize here (Commit does that).
        public string SaveName
        {
            get => _svc.Cfg.SaveName;
            set => Set(ref _svc.Cfg.SaveName, value);
        }

        // ------------------------------ Hosting --------------------------------
        public string[] VisibilityOptions { get; } = new[] { "Public", "Friends", "None" };

        /// <summary>
        /// Display value for the segmented control. Maps both ways to cfg.Visibility
        /// ("public"|"friends_only"|"none"); stored normalized. Persists on Save only.
        /// </summary>
        public string VisibilityDisplay
        {
            get
            {
                switch (_svc.Cfg.Visibility)
                {
                    case "friends_only": return "Friends";
                    case "none": return "None";
                    default: return "Public";
                }
            }
            set
            {
                string canonical;
                switch ((value ?? "").Trim().ToLowerInvariant())
                {
                    case "friends": canonical = "friends_only"; break;
                    case "none": canonical = "none"; break;
                    default: canonical = "public"; break;
                }
                Set(ref _svc.Cfg.Visibility, AppConfig.NormalizeVisibility(canonical));
            }
        }

        public string Password
        {
            get => _svc.Cfg.Password;
            set => Set(ref _svc.Cfg.Password, value);
        }

        // -------------------------------- Logs ---------------------------------
        // These two persist IMMEDIATELY on change (match the original launcher).
        public bool HideDebugLogLines
        {
            get => _svc.Cfg.HideDebugLogLines;
            set { if (Set(ref _svc.Cfg.HideDebugLogLines, value)) _svc.Cfg.Save(); }
        }

        public bool HideSyncNoise
        {
            get => _svc.Cfg.HideSyncNoise;
            set { if (Set(ref _svc.Cfg.HideSyncNoise, value)) _svc.Cfg.Save(); }
        }

        // ------------------------------ Advanced -------------------------------
        public string ServerAddressHint
        {
            get => _svc.Cfg.ServerAddressHint;
            set => Set(ref _svc.Cfg.ServerAddressHint, value);
        }

        public string GameRoot
        {
            get => _svc.Cfg.GameRoot;
            set => Set(ref _svc.Cfg.GameRoot, value);
        }

        public ICommand SaveCommand { get; }

        private void SaveSettings()
        {
            _svc.Commit();
            // Reflect any normalization Commit applied (blank save -> "MP"; trimmed hint/root).
            OnPropertyChanged(nameof(SaveName));
            OnPropertyChanged(nameof(ServerAddressHint));
            OnPropertyChanged(nameof(GameRoot));
            _svc.ShowToast("✓ Settings saved to coopconsole.cfg", ToastKind.Success);
        }
    }
}
