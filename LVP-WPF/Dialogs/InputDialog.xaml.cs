using CommunityToolkit.Mvvm.ComponentModel;
using LVP_WPF.Services;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace LVP_WPF
{
    [ObservableObject]
    public partial class InputDialog : Window
    {
        private string tmdbUrl;
        private string episodePath;

        public static bool Show(string caption, string message, TvShow tvShow = null, int currSeason = 0, string episodePath = null)
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
                    dialog.tmdbUrl = $"https://www.themoviedb.org/tv/{tvShow.Id}/season/{currSeason - 1}";
                }
                // Folder button: shown only when caller supplied a path. Lets
                // the user jump straight to the offending file in Explorer
                // to fix it (rename, delete, replace) before clicking
                // Continue. Existence isn't checked at Show time because the
                // dialog may be reporting a rename-in-flight where the file
                // briefly doesn't exist at either name.
                if (string.IsNullOrEmpty(episodePath))
                {
                    dialog.folderBtn.Visibility = Visibility.Hidden;
                }
                else
                {
                    dialog.episodePath = episodePath;
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
            CursorManager.RestoreSystemCursor();
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

        // "Folder" opens Windows File Explorer with the offending file
        // highlighted in its containing directory (the /select, switch).
        // If the file itself is gone (the rename already moved it, or the
        // path was stale), fall back to opening the parent directory by
        // path so the user still lands somewhere useful.
        private void FolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists(episodePath))
                {
                    // /select takes a full path; quote it to survive spaces.
                    Process.Start("explorer.exe", $"/select,\"{episodePath}\"");
                }
                else
                {
                    string dir = Path.GetDirectoryName(episodePath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        Process.Start("explorer.exe", $"\"{dir}\"");
                    }
                    else
                    {
                        Log.Warning("Folder button: path '{Path}' doesn't exist and parent dir '{Dir}' doesn't either", episodePath, dir);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Folder button failed for '{Path}': {Msg}", episodePath, ex.Message);
            }
            e.Handled = true;
        }
    }
}
