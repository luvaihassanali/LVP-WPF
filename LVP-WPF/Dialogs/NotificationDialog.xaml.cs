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
    /// https://stackoverflow.com/questions/18325239/how-to-set-the-font-in-different-styles-for-message-box-in-wpf
    
    [ObservableObject]
    public partial class NotificationDialog : Window
    {
        public static void Show(string title, string caption, string message, NotifyIcon icon)
        {
            NotificationDialog dialog = new NotificationDialog();
            dialog.Title = title;
            dialog.Caption = caption;
            dialog.Message = message;
            dialog.Image = dialog.GetIcon(icon);
            dialog.Topmost = true;
            dialog.ShowDialog();
        }

        [ObservableProperty]
        private string caption;
        [ObservableProperty]
        private string message;
        [ObservableProperty]
        private BitmapSource image;

        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public NotificationDialog()
        {
            DataContext = this;
            InitializeComponent();
            Loaded += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
            };
        }

        private BitmapSource GetIcon(NotifyIcon iconType)
        {
            Icon icon = (Icon)typeof(SystemIcons).GetProperty(iconType.ToString(), BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
            return Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }

        private void Button_Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }

    public enum NotifyIcon
    {
        Application,
        Asterisk,
        Error,
        Exclamation,
        Hand,
        Information,
        Question,
        Shield,
        Warning,
        WinLogo
    }
}
