using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace LVP_WPF
{
    [ObservableObject]
    public partial class InputDialog : Window
    {
        private static string tmdbUrl;

        public static bool Show(string caption, string message, TvShow tvShow = null, int currSeason = 0)
        {
            InputDialog dialog = new InputDialog();
            dialog.Caption = caption;
            dialog.Message = message;
            dialog.Topmost = true;
            if (tvShow == null)
            {
                dialog.tmdbBtn.Visibility = Visibility.Hidden;
            }
            else
            {
                tmdbUrl = "https://www.themoviedb.org/tv/" + tvShow.Id.ToString() + "/season/" + currSeason;
            }
            dialog.ShowDialog();
            if ((bool)dialog.DialogResult)
            {
                return true;
            }
            return false;
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
            Process.Start(new ProcessStartInfo(tmdbUrl) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
