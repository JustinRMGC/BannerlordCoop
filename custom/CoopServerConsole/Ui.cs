// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Attached properties used by the control templates in Theme/Controls.xaml.
// (Setter.Value can't hold a Binding in WPF, so per-kind hover/press colors are
//  carried as attached properties the templates read via TemplateBinding.)
// =============================================================================

using System.Windows;
using System.Windows.Media;

namespace CoopServerConsole
{
    /// <summary>Attached properties for the launcher's custom control look.</summary>
    internal static class Ui
    {
        // A Segoe Fluent glyph string shown before a button's text (null/empty = no glyph).
        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.RegisterAttached("Glyph", typeof(string), typeof(Ui),
                new PropertyMetadata(null));
        public static string GetGlyph(DependencyObject o) => (string)o.GetValue(GlyphProperty);
        public static void SetGlyph(DependencyObject o, string v) => o.SetValue(GlyphProperty, v);

        // Corner radius for templated controls that expose one.
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.RegisterAttached("CornerRadius", typeof(CornerRadius), typeof(Ui),
                new PropertyMetadata(new CornerRadius(8)));
        public static CornerRadius GetCornerRadius(DependencyObject o) => (CornerRadius)o.GetValue(CornerRadiusProperty);
        public static void SetCornerRadius(DependencyObject o, CornerRadius v) => o.SetValue(CornerRadiusProperty, v);
    }
}
