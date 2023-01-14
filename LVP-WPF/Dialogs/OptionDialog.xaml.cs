using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace LVP_WPF
{
    [ObservableObject]
    public partial class OptionDialog : Window
    {
        public static int Show(string title, string[][] info, DateTime?[] dates)
        {
            OptionDialog dialog = new OptionDialog();
            dialog.Caption = title + "?";
            dialog.Message = "Select the correct entry for: " + title;
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

        public OptionDialog()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void Button_Exit_Click(object sender, RoutedEventArgs e)
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
