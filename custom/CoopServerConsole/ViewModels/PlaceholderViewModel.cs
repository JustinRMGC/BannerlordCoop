// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Temporary stand-in for pages built next (Server/Client Log, Players, Settings).
// =============================================================================

using CoopServerConsole.Mvvm;

namespace CoopServerConsole.ViewModels
{
    internal sealed class PlaceholderViewModel : ViewModelBase
    {
        public string Title { get; }
        public string Message { get; }
        public PlaceholderViewModel(string title, string message)
        {
            Title = title;
            Message = message;
        }
    }
}
