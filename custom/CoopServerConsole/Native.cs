// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Windows 11 window chrome for the WPF window: dark title bar + rounded corners
// (+ best-effort Mica), via DWM. Mirrors what the old WinForms build did.
// =============================================================================

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CoopServerConsole
{
    internal static class Native
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndAfter, int x, int y, int cx, int cy, uint flags);

        // Dark title bar: 20 on Win10 2004+/Win11; 19 on the earlier 1809/1903 builds.
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE  = 33; // Win11 rounded corners
        private const int DWMWA_SYSTEMBACKDROP_TYPE       = 38; // Win11 22H2+ backdrop (Mica)
        private const int DWMWCP_ROUND    = 2;
        private const int DWMSBT_MAINWINDOW = 2; // Mica

        private const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_NOZORDER = 0x0004, SWP_FRAMECHANGED = 0x0020;

        /// <summary>
        /// Apply dark title bar + rounded corners to a WPF window. Call once the HWND
        /// exists (e.g. from Window.OnSourceInitialized). All calls are best-effort and
        /// silently no-op on OS versions that don't support the attribute.
        /// </summary>
        public static void ApplyModernChrome(Window window, bool mica = false)
        {
            if (window == null) return;
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) hwnd = new WindowInteropHelper(window).EnsureHandle();
            if (hwnd == IntPtr.Zero) return;

            if (TrySet(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, 1) != 0)
                TrySet(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, 1);
            TrySet(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND);
            if (mica) TrySet(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, DWMSBT_MAINWINDOW);

            // Force the non-client frame to repaint so the dark caption takes effect
            // immediately (without this it can stay light until the first resize).
            try { SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED); }
            catch { }
        }

        private static int TrySet(IntPtr hwnd, int attr, int value)
        {
            try { int v = value; return DwmSetWindowAttribute(hwnd, attr, ref v, sizeof(int)); }
            catch { return -1; }
        }
    }
}
