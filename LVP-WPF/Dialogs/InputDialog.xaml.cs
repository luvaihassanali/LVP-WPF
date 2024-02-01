using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows;

namespace LVP_WPF
{
    [ObservableObject]
    public partial class InputDialog : Window
    {
        private static string tmdbUrl;

        public static bool Show(string caption, string message, TvShow tvShow = null, int currSeason = 0)
        {
            bool res = false;
            Application.Current.Dispatcher.Invoke(delegate
            {
                InputDialog dialog = new InputDialog
                {
                    Caption = caption,
                    Message = message,
                    Topmost = true
                };
                if (tvShow == null)
                {
                    dialog.tmdbBtn.Visibility = Visibility.Hidden;
                }
                else
                {
                    tmdbUrl = $"https://www.themoviedb.org/tv/{tvShow.Id}/season/{currSeason - 1}";
                }
                dialog.ShowDialog();
                if (dialog.DialogResult != null && (bool)dialog.DialogResult)
                {
                    res = true;
                }
            });
            return res;
        }

        [ObservableProperty]
        private string caption;
        [ObservableProperty]
        private string message;

        public InputDialog()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            GuiModel.RestoreSystemCursor();
            Environment.Exit(0);
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void TmdbButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tmdbUrl) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
