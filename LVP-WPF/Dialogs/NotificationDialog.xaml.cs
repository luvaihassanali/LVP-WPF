using CommunityToolkit.Mvvm.ComponentModel;
using LVP_WPF.Services;
using Serilog;
using System;
using System.Windows;

namespace LVP_WPF
{
    [ObservableObject]
    public partial class NotificationDialog : Window
    {
        public static void Show(string caption, string message)
        {
            // Already logged at Error level by WpfUserPrompts.ShowError before
            // this is called, but the static factory may also be invoked
            // directly from MediaEnricher / App.CurrentDomain_UnhandledException
            // - logging here covers both paths so every NotificationDialog
            // appearance is reconstructable from the file log.
            Log.Information("NotificationDialog.Show: {Caption} - {Message}", caption, message);

            if (System.Diagnostics.Debugger.IsAttached)
            {
                Log.Warning("NotificationDialog: debugger already attached, launching interactive break");
                System.Diagnostics.Debugger.Launch();
            }

            Application.Current.Dispatcher.Invoke(delegate
            {
                NotificationDialog dialog = new NotificationDialog
                {
                    Caption = caption,
                    Message = message,
                    Topmost = true
                };
                dialog.ShowDialog();
            });
        }

        [ObservableProperty]
        private string caption;
        [ObservableProperty]
        private string message;

        public NotificationDialog()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Log.Information("NotificationDialog: user clicked Save (library is {LibraryState})",
                MainWindow.library != null ? "loaded" : "null");
            MainWindow.library?.SaveData();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Log.Information("NotificationDialog: user clicked Exit -> Environment.Exit(0)");
            CursorManager.RestoreSystemCursor();
            Environment.Exit(0);
        }
    }
}
