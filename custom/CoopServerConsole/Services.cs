// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Shared UI services + small contracts wiring the WPF views to the (reused,
// UI-agnostic) backend classes. None of the backend files were changed.
// =============================================================================

using System;
using System.Windows;

namespace CoopServerConsole
{
    internal enum ToastKind { Info, Success, Warning, Error }

    /// <summary>A page view-model that wants the shell's shared 500 ms tick.</summary>
    internal interface ITickable
    {
        void OnTick(long tick);
    }

    /// <summary>
    /// Shared services handed to each page view-model: the config + launcher, the
    /// resolved engine-bin dir, the version stamp, the last bridge snapshot, and a
    /// toast callback back to the shell.
    /// </summary>
    internal sealed class LauncherServices
    {
        public AppConfig Cfg;
        public GameLauncher Launcher;
        public string Version = "v0.0.0";
        public StatusSnapshot LastSnapshot = new StatusSnapshot();
        public Action<string, ToastKind> Toast;

        public string Bin => Paths.GameBinDir(Cfg.GameRoot);

        public void ShowToast(string msg, ToastKind kind = ToastKind.Info) => Toast?.Invoke(msg, kind);

        /// <summary>Dark, theme-matched confirmation modal. Returns true if confirmed.</summary>
        public bool Confirm(string title, string message, bool danger = false)
            => FonzaDialog.Confirm(Application.Current?.MainWindow, title, message, danger);

        /// <summary>Dark, theme-matched single-line input modal. Returns text, or null if cancelled.</summary>
        public string Prompt(string title, string message, string initial)
            => FonzaDialog.Prompt(Application.Current?.MainWindow, title, message, initial);

        /// <summary>
        /// Normalize + persist settings. Preserves the original launcher's quirks:
        /// blank save name -> "MP"; save/hint/root trimmed; visibility normalized;
        /// password stored verbatim (NOT trimmed).
        /// </summary>
        public void Commit()
        {
            Cfg.SaveName = string.IsNullOrWhiteSpace(Cfg.SaveName) ? "MP" : Cfg.SaveName.Trim();
            Cfg.Visibility = AppConfig.NormalizeVisibility(Cfg.Visibility);
            Cfg.ServerAddressHint = (Cfg.ServerAddressHint ?? "").Trim();
            Cfg.GameRoot = (Cfg.GameRoot ?? "").Trim();
            Cfg.Save();
        }
    }
}
