using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

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

        private void Button_Exit_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
