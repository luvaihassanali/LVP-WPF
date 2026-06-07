using System.Windows;
using System.Windows.Controls;

namespace LVP_WPF.Services
{
    /// <summary>
    /// WPF-backed implementation of <see cref="ILoadProgress"/>. Owned by
    /// MainWindow; threads writes through the relevant controls' dispatchers
    /// so MediaLibrary can call from a Task.Run worker.
    /// </summary>
    internal sealed class WpfLoadProgress : ILoadProgress
    {
        private readonly ProgressBar _progressBar;
        private readonly MediaElement _spinner;
        private readonly TextBox _log;

        public WpfLoadProgress(ProgressBar progressBar, MediaElement spinner, TextBox log)
        {
            _progressBar = progressBar;
            _spinner = spinner;
            _log = log;
        }

        public void AppendLog(string message)
        {
            _log.Dispatcher.Invoke(delegate
            {
                _log.Text += MainWindow.gui.ProgressBarValue != 1
                    ? $"[{MainWindow.gui.ProgressBarValue}/{MainWindow.gui.ProgressBarMax}] {message}\n"
                    : $"{message}\n";
                _log.Focus();
                _log.CaretIndex = _log.Text.Length;
                _log.ScrollToEnd();
            });
        }

        public void ShowRebuildIndicators()
        {
            Application.Current.Dispatcher.Invoke(delegate
            {
                _progressBar.Visibility = Visibility.Visible;
                _spinner.Visibility = Visibility.Visible;
                _log.Visibility = Visibility.Visible;
            });
        }
    }
}
