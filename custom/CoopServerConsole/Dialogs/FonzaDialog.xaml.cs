// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Dark, theme-matched modal replacing the light system MessageBox / InputBox.
// =============================================================================

using System.Windows;

namespace CoopServerConsole
{
    public partial class FonzaDialog : Window
    {
        public FonzaDialog()
        {
            InitializeComponent();
            OkBtn.Click     += (s, e) => { DialogResult = true; };
            CancelBtn.Click += (s, e) => { DialogResult = false; };
        }

        /// <summary>Yes/No style confirmation. Returns true if confirmed.</summary>
        public static bool Confirm(Window owner, string title, string message, bool danger = false)
        {
            var d = new FonzaDialog();
            if (owner != null && owner.IsVisible) d.Owner = owner;
            d.TitleText.Text = title;
            d.MessageText.Text = message;
            d.Input.Visibility = Visibility.Collapsed;
            d.OkBtn.Content = "Confirm";
            if (danger && Application.Current != null &&
                Application.Current.TryFindResource("Btn.Danger") is Style s)
                d.OkBtn.Style = s;
            return d.ShowDialog() == true;
        }

        /// <summary>Single-line input. Returns the text, or null if cancelled.</summary>
        public static string Prompt(Window owner, string title, string message, string initial)
        {
            var d = new FonzaDialog();
            if (owner != null && owner.IsVisible) d.Owner = owner;
            d.TitleText.Text = title;
            d.MessageText.Text = message;
            d.Input.Visibility = Visibility.Visible;
            d.Input.Text = initial ?? "";
            d.OkBtn.Content = "OK";
            d.Loaded += (s, e) => { d.Input.Focus(); d.Input.SelectAll(); };
            return d.ShowDialog() == true ? d.Input.Text : null;
        }
    }
}
