// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Players page code-behind + a local converter for the empty-state text tint.
// =============================================================================

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace CoopServerConsole.Views
{
    public partial class PlayersView : UserControl
    {
        public PlayersView() { InitializeComponent(); }
    }

    /// <summary>Empty-state kind -> text brush: "warn" -> amber, anything else -> muted.
    /// Local to this page (see the UserControl.Resources instance keyed "EmptyBrush").</summary>
    public sealed class EmptyKindToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            string kind = (value as string ?? "").Trim().ToLowerInvariant();
            string key = kind == "warn" ? "WarnTextBrush" : "TextMutedBrush";
            return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
        }

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }
}
