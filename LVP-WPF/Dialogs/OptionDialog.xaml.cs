using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace LVP_WPF
{
    [ObservableObject]
    public partial class OptionDialog : Window
    {
        public static int Show(string title, string[][] info, DateTime?[] dates, NotifyIcon icon)
        {
            //To-do: remove icon from parameter
            OptionDialog dialog = new OptionDialog();
            dialog.Title = title + "?";
            dialog.Caption = title + "?";
            dialog.Message = "Select the correct entry for: " + title;
            dialog.Image = dialog.GetIcon(icon);
            dialog.Topmost = true;
            OptionWindowBox[] entries = new OptionWindowBox[info[0].Length];
            for (int i = 0; i < info[0].Length; i++)
            {
                entries[i] = new OptionWindowBox
                {
                    Name = info[0][i] + " (" + dates[i].GetValueOrDefault().Year + ")",
                    Description = info[2][i].Equals(String.Empty) ? "No description." : info[2][i],
                    Id = Int32.Parse(info[1][i])
                };
            }
            dialog.OptionBox.ItemsSource = entries;
            dialog.ShowDialog();
            return returnId;
        }

        [ObservableProperty]
        private string caption;
        [ObservableProperty]
        private string message;
        [ObservableProperty]
        private BitmapSource image;
        private static int returnId = -1;

        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public OptionDialog()
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
            Environment.Exit(0);
        }

        private void Button_Continue_Click(object sender, RoutedEventArgs e)
        {
            if (OptionBox.SelectedIndex == -1) return;
            OptionWindowBox o = (OptionWindowBox)OptionBox.SelectedItem;
            returnId = o.Id;
            this.Close();
        }
    }
}
