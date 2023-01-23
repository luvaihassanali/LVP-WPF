using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows;

namespace LVP_WPF
{
    [ObservableObject]
    public partial class InputDialog : Window
    {
        public static bool Show(string caption, string message)
        {
            InputDialog dialog = new InputDialog();
            dialog.Caption = caption;
            dialog.Message = message;
            dialog.Topmost = true;
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
            Environment.Exit(0);
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
