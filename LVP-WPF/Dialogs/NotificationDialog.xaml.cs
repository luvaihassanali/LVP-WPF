using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Diagnostics;
using System.Security.Policy;
using System.Windows;

namespace LVP_WPF
{
    [ObservableObject]
    public partial class NotificationDialog : Window
    {
        public static void Show(string caption, string message)
        {
            NotificationDialog dialog = new NotificationDialog();
            dialog.Caption = caption;
            dialog.Message = message;
            dialog.Topmost = true;
            dialog.ShowDialog();
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

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            GuiModel.RestoreSystemCursor();
            Environment.Exit(0);
        }
    }
}
