using System.Windows.Controls;
using System.Windows.Input;

namespace LVP_WPF.Util
{
    /// <summary>
    /// Shared scroll-viewer helpers used by every scrollable window.
    /// Wraps two patterns that used to be copy-pasted across MainWindow,
    /// TvShowWindow, SeasonWindow, and MovieWindow:
    ///
    ///   - The "after a programmatic BringIntoView, nudge the viewport
    ///     by 300px in the move direction" trick used by LayoutPoint
    ///     navigation (driven by the MainWindow.gui.scrollViewerAdjust flag).
    ///   - The "mouse-wheel notch = scroll by 300px" handler.
    ///
    /// 300px is the tile-row stride; tied to the main grid's row height
    /// rather than a typical wheel scroll.
    /// </summary>
    public static class ScrollHelper
    {
        public const double StepSize = 300;

        /// <summary>
        /// If the LayoutPoint navigation just asked for a viewport nudge
        /// (MainWindow.gui.scrollViewerAdjust is set), apply it and clear
        /// the flag. No-op otherwise.
        /// </summary>
        public static void ApplyAdjust(ScrollViewer scrollViewer, ScrollChangedEventArgs e)
        {
            if (!MainWindow.gui.scrollViewerAdjust) return;
            MainWindow.gui.scrollViewerAdjust = false;
            double padding = e.VerticalChange > 0 ? StepSize : -StepSize;
            scrollViewer.ScrollToVerticalOffset(e.VerticalOffset + padding);
        }

        /// <summary>One-row scroll step in response to a mouse-wheel notch.</summary>
        public static void StepFromWheel(ScrollViewer scrollViewer, double currentOffset, MouseWheelEventArgs e)
        {
            double delta = e.Delta > 0 ? -StepSize : StepSize;
            scrollViewer.ScrollToVerticalOffset(currentOffset + delta);
        }
    }
}
