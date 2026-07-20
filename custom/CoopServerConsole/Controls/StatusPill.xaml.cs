// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// =============================================================================

using System.Windows;
using System.Windows.Controls;

namespace CoopServerConsole
{
    public partial class StatusPill : UserControl
    {
        public StatusPill() { InitializeComponent(); }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(StatusPill), new PropertyMetadata(""));
        public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

        public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
            nameof(Kind), typeof(string), typeof(StatusPill), new PropertyMetadata("idle"));
        public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    }
}
