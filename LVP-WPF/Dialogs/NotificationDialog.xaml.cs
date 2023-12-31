﻿using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows;

namespace LVP_WPF
{
    [ObservableObject]
    public partial class NotificationDialog : Window
    {
        public static void Show(string caption, string message)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Break();
            }

            NotificationDialog dialog = new NotificationDialog
            {
                Caption = caption,
                Message = message,
                Topmost = true
            };
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
