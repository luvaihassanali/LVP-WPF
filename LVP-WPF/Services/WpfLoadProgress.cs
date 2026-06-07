using System.Windows;
using System.Windows.Controls;

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

        public WpfLoadProgress(ProgressBar progressBar)
        {
            _progressBar = progressBar;
        }

        // The progress bar is already Visible (MainWindow_Loaded sets that for
        // the regular load path). On a rebuild we just flip it from
        // indeterminate (marquee) to determinate so the per-item ticks from
        // BuildCache become a real fill. The animated coffee.gif is always
        // visible, so no separate spinner toggle here anymore.
        public void ShowRebuildIndicators()
        {
            Application.Current.Dispatcher.Invoke(delegate
            {
                _progressBar.Visibility = Visibility.Visible;
                _progressBar.IsIndeterminate = false;
            });
        }
    }
}
