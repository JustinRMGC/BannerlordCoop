// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Value converters used across the WPF views.
// =============================================================================

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CoopServerConsole.Mvvm
{
    /// <summary>bool -> Visibility. ConverterParameter="invert" flips it.</summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            bool b = value is bool bb && bb;
            if (p as string == "invert") b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => (value is Visibility v && v == Visibility.Visible) ^ (p as string == "invert");
    }

    /// <summary>Non-empty string -> Visible. ConverterParameter="invert" flips it.</summary>
    public sealed class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            bool has = !string.IsNullOrWhiteSpace(value as string);
            if (p as string == "invert") has = !has;
            return has ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }

    /// <summary>bool -> inverted bool.</summary>
    public sealed class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => !(value is bool b && b);
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => !(value is bool b && b);
    }

    /// <summary>
    /// Maps a status-kind string ("running"/"stopped"/"warn"/"error"/"info") to a themed brush.
    /// ConverterParameter selects the role: "dot" | "text" | "bg" | "border".
    /// </summary>
    public sealed class StatusBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            string kind = (value as string ?? "idle").Trim().ToLowerInvariant();
            string role = (p as string ?? "dot").Trim().ToLowerInvariant();
            string key;
            switch (kind)
            {
                case "running": case "on": case "success":
                    key = role == "text" ? "SuccessTextBrush" : role == "bg" ? "SuccessTintBrush" : role == "border" ? "SuccessTintBorderBrush" : "SuccessSolidBrush";
                    break;
                case "warn": case "warning":
                    key = role == "text" ? "WarnTextBrush" : role == "bg" ? "WarnTintBrush" : role == "border" ? "WarnTintBorderBrush" : "WarnSolidBrush";
                    break;
                case "error": case "bad": case "danger":
                    key = role == "text" ? "ErrorTextBrush" : role == "bg" ? "ErrorTintBrush" : role == "border" ? "ErrorTintBorderBrush" : "ErrorSolidBrush";
                    break;
                case "info":
                    key = role == "text" ? "InfoTextBrush" : role == "bg" ? "InfoTintBrush" : role == "border" ? "InfoTintBorderBrush" : "InfoSolidBrush";
                    break;
                default: // stopped / idle / off -> neutral
                    key = role == "text" ? "TextSecondaryBrush" : role == "bg" ? "Surface2Brush" : role == "border" ? "BorderStrongBrush" : "TextMutedBrush";
                    break;
            }
            return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Transparent;
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }
}
