// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Live log page view. Autoscroll + "jump to latest (N new)" live entirely in the
// code-behind (reacting to the ListBox's ScrollViewer + item changes) so the VM
// stays free of scroll concerns. Do NOT set DataContext — the shell binds it.
// =============================================================================

using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace CoopServerConsole.Views
{
    public partial class LogView : UserControl
    {
        private ScrollViewer _sv;
        private INotifyCollectionChanged _items;
        private bool _pinned = true;      // stuck to the bottom (autoscroll active)
        private int _newCount;            // visible lines added since the user unpinned
        private bool _scrollQueued;
        private const double Eps = 1.0;

        public LogView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            HookScrollViewer();
            HookItems();
            RepinToBottom();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_sv != null) { _sv.ScrollChanged -= OnScrollChanged; _sv = null; }
            if (_items != null) { _items.CollectionChanged -= OnItemsChanged; _items = null; }
        }

        // Container may be reused when switching Server<->Client (both are LogViewModel);
        // re-pin to the bottom for the newly bound stream.
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            HookItems();
            RepinToBottom();
        }

        private void RepinToBottom()
        {
            _pinned = true;
            _newCount = 0;
            UpdatePill();
            ScheduleScrollToBottom();
        }

        // ---------------- hookup ----------------
        private void HookScrollViewer()
        {
            if (_sv != null) return;
            LogList.ApplyTemplate();
            _sv = FindDescendant<ScrollViewer>(LogList);
            if (_sv != null) _sv.ScrollChanged += OnScrollChanged;
            else Dispatcher.BeginInvoke(new Action(HookScrollViewer), DispatcherPriority.Loaded);
        }

        private void HookItems()
        {
            var inc = LogList.Items as INotifyCollectionChanged;
            if (ReferenceEquals(inc, _items)) return;
            if (_items != null) _items.CollectionChanged -= OnItemsChanged;
            _items = inc;
            if (_items != null) _items.CollectionChanged += OnItemsChanged;
        }

        // ---------------- reactions ----------------
        private void OnItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (_pinned) ScheduleScrollToBottom();
                    else { _newCount += e.NewItems?.Count ?? 0; UpdatePill(); }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (_pinned) ScheduleScrollToBottom();   // buffer trim: stay glued
                    break;
                case NotifyCollectionChangedAction.Reset:
                    _newCount = 0; UpdatePill();             // clear / re-filter
                    if (_pinned) ScheduleScrollToBottom();
                    break;
            }
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_sv == null) return;

            // Content grew or viewport resized: keep glued to the bottom if pinned,
            // and don't reinterpret it as a user scroll.
            if (e.ExtentHeightChange > 0 || e.ViewportHeightChange != 0)
            {
                if (_pinned) _sv.ScrollToBottom();
                return;
            }

            // Pure user scroll: pin when at/near the bottom, unpin otherwise.
            bool atBottom = _sv.VerticalOffset >= _sv.ScrollableHeight - Eps;
            if (atBottom)
            {
                if (!_pinned) { _pinned = true; _newCount = 0; UpdatePill(); }
            }
            else
            {
                _pinned = false;
            }
        }

        private void JumpPill_Click(object sender, RoutedEventArgs e)
        {
            _pinned = true;
            _newCount = 0;
            UpdatePill();
            _sv?.ScrollToBottom();
        }

        // ---------------- helpers ----------------
        private void ScheduleScrollToBottom()
        {
            if (_scrollQueued || _sv == null) return;
            _scrollQueued = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _scrollQueued = false;
                _sv?.ScrollToBottom();
            }), DispatcherPriority.Background);
        }

        private void UpdatePill()
        {
            if (JumpPill == null) return;
            if (!_pinned && _newCount > 0)
            {
                JumpPill.Content = _newCount == 1
                    ? "Jump to latest (1 new)"
                    : string.Format(CultureInfo.InvariantCulture, "Jump to latest ({0} new)", _newCount);
                JumpPill.Visibility = Visibility.Visible;
            }
            else
            {
                JumpPill.Visibility = Visibility.Collapsed;
            }
        }

        private static T FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) return null;
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var c = VisualTreeHelper.GetChild(root, i);
                if (c is T hit) return hit;
                var deep = FindDescendant<T>(c);
                if (deep != null) return deep;
            }
            return null;
        }
    }

    /// <summary>bool -> TextWrapping for the Wrap chip (true = Wrap, false = NoWrap).</summary>
    public sealed class BoolToWrapConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => (value is bool b && b) ? TextWrapping.Wrap : TextWrapping.NoWrap;

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => value is TextWrapping w && w != TextWrapping.NoWrap;
    }
}
