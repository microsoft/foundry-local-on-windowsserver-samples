using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PatientSummaryTool.Utils.Behaviors
{
    public class AutoScrollBehavior
    {
        public static readonly DependencyProperty AutoScrollToEndProperty =
            DependencyProperty.RegisterAttached(
                "AutoScrollToEnd",
                typeof(bool),
                typeof(AutoScrollBehavior),
                new PropertyMetadata(false, OnAutoScrollChanged));

        public static bool GetAutoScrollToEnd(DependencyObject obj) =>
            (bool)obj.GetValue(AutoScrollToEndProperty);

        public static void SetAutoScrollToEnd(DependencyObject obj, bool value) =>
            obj.SetValue(AutoScrollToEndProperty, value);

        // For detecting streaming vs bulk load
        private static DateTime _lastScrollTime = DateTime.MinValue;
        private static double _lastExtentHeight = 0;
        private static readonly TimeSpan StreamThreshold = TimeSpan.FromMilliseconds(500); // treat as "stream" if updates < 500 ms apart
        private static readonly double BulkSizeThreshold = 500; // treat as "bulk load" if content grows > 500px at once

        private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer scrollViewer)
            {
                if ((bool)e.NewValue)
                    scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
                else
                    scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
            }
        }

        private static void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = (ScrollViewer)sender;

            // Detect if user is near the bottom
            bool isUserAtBottom = scrollViewer.VerticalOffset + scrollViewer.ViewportHeight >= scrollViewer.ExtentHeight - 10;

            // Skip if user scrolled up
            if (!isUserAtBottom)
            {
                _lastExtentHeight = scrollViewer.ExtentHeight;
                return;
            }

            // Calculate change magnitude and timing
            double heightChange = e.ExtentHeightChange;
            var now = DateTime.Now;
            bool isStreaming = heightChange > 0 &&
                               heightChange < BulkSizeThreshold &&
                               (now - _lastScrollTime) < StreamThreshold;

            if (isStreaming)
            {
                // Auto-scroll only for continuous small updates
                scrollViewer.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new Action(scrollViewer.ScrollToEnd));
            }

            _lastScrollTime = now;
            _lastExtentHeight = scrollViewer.ExtentHeight;
        }
    }
}
