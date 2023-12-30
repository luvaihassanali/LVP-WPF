using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LVP_WPF.Dialogs
{
    [ObservableObject]
    public partial class ResetSeasonDialog : Window
    {
        private static List<int> results = new List<int>();

        public static int[] Show(TvShow tvShow)
        {
            results.Clear();
            ResetSeasonDialog resetDialog = new ResetSeasonDialog();
            string epString = tvShow.LastEpisode == null ? "" : $"E{tvShow.LastEpisode.Id}";
            resetDialog.Header = $"{tvShow.Name} (S{tvShow.CurrSeason}{epString})";
            OptionWindowBox[] seasonBoxes = new OptionWindowBox[tvShow.Seasons.Length + 1];
            seasonBoxes[0] = new OptionWindowBox { Id = 0, Name = "  All" };
            int idx = 0;
            for (int i = 1; i <= tvShow.Seasons.Length; i++)
            {
                string name;
                if (tvShow.Seasons[idx].Id == -1) name = "  Extras";
                else name = $"   Season {tvShow.Seasons[idx].Id}";
                seasonBoxes[i] = new OptionWindowBox
                {
                    Id = tvShow.Seasons[idx++].Id,
                    Name = name
                };
            }
            resetDialog.SeasonListView.ItemsSource = seasonBoxes;
            resetDialog.ShowDialog();
            return results.ToArray();
        }

        [ObservableProperty]
        private string header;

        public ResetSeasonDialog()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            results.Add(Int32.MinValue);
            this.Close();
        }

        private void FillButton_Click(object sender, RoutedEventArgs e)
        {
            results.Add(Int32.MaxValue);
            this.Close();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox c = (CheckBox)sender;
            string name = c.Content.ToString();
            if (name.Equals("  All"))
            {
                results.Add(0);
            }
            else if (name.Equals("  Extras"))
            {
                results.Add(-1);
            }
            else
            {
                name = name.Replace("  Season ", "");
                results.Add(Int32.Parse(name));
            }
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox c = (CheckBox)sender;
            string name = c.Content.ToString();
            if (name.Equals("  All"))
            {
                results.Remove(0);
            }
            else if (name.Equals("  Extras"))
            {
                results.Remove(-1);
            }
            else
            {
                name = name.Replace("  Season ", "");
                results.Remove(Int32.Parse(name));
            }
        }

        private void SeasonListView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            /*ListView seasonListView = sender as ListView;
            OptionWindowBox item = (OptionWindowBox)seasonListView.SelectedItem;
            ItemContainerGenerator generator = seasonListView.ItemContainerGenerator;
            ListViewItem container = (ListViewItem)generator.ContainerFromItem(item);
            CheckBox c = GuiModel.GetChildrenByType(container, typeof(CheckBox), "checkbox") as CheckBox;
            if (c != null) c.IsChecked = (bool)c.IsChecked ? false : true;*/
        }
    }
}
