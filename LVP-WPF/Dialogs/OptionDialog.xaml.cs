using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows;

namespace LVP_WPF
{
    [ObservableObject]
    public partial class OptionDialog : Window
    {
        private static int returnId = -1;

        public static int Show(string title, string path, string[][] info, DateTime?[] dates)
        {
            OptionDialog dialog = new OptionDialog();
            dialog.Caption = title + "?";
            dialog.Message = "Select the correct entry for: " + title;
            dialog.Path = path;
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
            dialog.OptionListView.ItemsSource = entries;
            dialog.ShowDialog();
            return returnId;
        }

        [ObservableProperty]
        private string caption;
        [ObservableProperty]
        private string message;
        [ObservableProperty]
        private string path;

        public OptionDialog()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            GuiModel.RestoreSystemCursor();
            Cache.SaveData();
            Environment.Exit(0);
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            if (OptionListView.SelectedIndex == -1) return;
            OptionWindowBox o = (OptionWindowBox)OptionListView.SelectedItem;
            returnId = o.Id;
            this.Close();
        }

        private void OptionWindow_Loaded(object sender, RoutedEventArgs e)
        {
            OptionListView.SelectedIndex= 0;
        }
    }
}
