using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LVP_WPF.Services
{
    /// <summary>
    /// WPF-backed implementation of <see cref="ILoadProgress"/>. Owned by
    /// MainWindow; marshals writes through the controls' dispatchers so
    /// MediaLibrary can call from a worker thread.
    /// </summary>
    internal sealed class WpfLoadProgress : ILoadProgress
    {
        private readonly ProgressBar _progressBar;
        private readonly TextBox _logTxtBox;
        private readonly DispatcherTimer _drainTimer;

        public WpfLoadProgress(ProgressBar progressBar, TextBox logTxtBox)
        {
            _progressBar = progressBar;
            _logTxtBox = logTxtBox;

            // Background priority so layout/render passes (incl. coffeeGif
            // animation frame ticks) get to run first. 100ms is a sweet
            // spot - fast enough to feel live for a human reader, slow
            // enough that a flood of log lines during scan batches up
            // instead of starving the render thread.
            _drainTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _drainTimer.Tick += DrainTick;
            _drainTimer.Start();
        }

        // The progress bar is already Visible (MainWindow_Loaded sets that for
        // the regular load path). On a rebuild path keep it INDETERMINATE
        // (marquee animation) rather than flipping to determinate per-item
        // fill - per-item ticks during a multi-minute TMDB rebuild end up
        // very chunky (each tile is a network round-trip + image download)
        // and the bar mostly looks frozen. The marquee makes it obvious
        // that work is happening even while a single item is in flight.
        // The animated coffee.gif is always visible, so no separate spinner
        // toggle here anymore. Per-item Value/Max writes from BuildCache
        // still happen but have no visual effect while IsIndeterminate is
        // true - they're just there to be ready when we flip to determinate
        // for the tile-population phase in MainWindow_Loaded.
        public void ShowRebuildIndicators()
        {
            Application.Current.Dispatcher.Invoke(delegate
            {
                _progressBar.Visibility = Visibility.Visible;
                _progressBar.IsIndeterminate = true;
            });
        }

        public void StopLogDrain()
        {
            _drainTimer.Stop();
            // One last drain pass so trailing "load complete" lines from
            // late-running phases (CheckForUpdates, final timing summary,
            // dialog auto-logs) make it into the visible TextBox before it
            // gets hidden.
            DrainTick(this, EventArgs.Empty);
        }

        // Drain everything currently queued, capped so a sudden burst can't
        // monopolize a single UI frame. Cap is generous (1000 lines/tick =
        // 10k lines/sec at the 100ms interval, which dwarfs any realistic
        // log rate even during a from-scratch rebuild).
        //
        // AppendText is the cheap path - we explicitly DO NOT touch
        // CaretIndex or Focus (those triggered selection invalidations
        // every line and were a hot spot in the old AppendLog impl).
        private void DrainTick(object sender, EventArgs e)
        {
            if (LoadScreenSink.Queue.IsEmpty) return;

            StringBuilder sb = new StringBuilder();
            int max = 1000;
            while (max-- > 0 && LoadScreenSink.Queue.TryDequeue(out string line))
            {
                sb.AppendLine(line);
            }
            if (sb.Length == 0) return;

            _logTxtBox.AppendText(sb.ToString());
            _logTxtBox.ScrollToEnd();
        }
    }
}
