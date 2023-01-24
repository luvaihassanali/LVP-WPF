﻿using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LVP_WPF.Dialogs
{
    /// <summary>
    /// Interaction logic for ResetSeasonWindow.xaml
    /// </summary>

    [ObservableObject]
    public partial class ResetSeasonDialog : Window
    {
        private static List<int> results = new List<int>();

        public static int[] Show(TvShow tvShow)
        {
            results.Clear();
            ResetSeasonDialog resetDialog = new ResetSeasonDialog();
            string epString = tvShow.LastEpisode == null ? "" : "E" + tvShow.LastEpisode.Id.ToString();
            resetDialog.Header = tvShow.Name + " (" + "S" + tvShow.CurrSeason + epString + ")";
            OptionWindowBox[] seasonBoxes = new OptionWindowBox[tvShow.Seasons.Length + 1];
            seasonBoxes[0] = new OptionWindowBox { Id = 0, Name = "  All" };
            int idx = 0;
            for (int i = 1; i <= tvShow.Seasons.Length; i++)
            {
                string name;
                if (tvShow.Seasons[idx].Id == -1) name = "  Extras";
                else name = "   Season " + tvShow.Seasons[idx].Id.ToString();
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
            bool selected = (bool)c.IsChecked;
            string name = c.Content.ToString();
            if (name.Equals("  All"))
            {
                if (selected)
                {
                    results.Add(0);
                }
                else
                {
                    results.Remove(0);
                }
            }
            else if (name.Equals("Extras"))
            {
                if (selected)
                {
                    results.Add(-1);
                }
                else
                {
                    results.Remove(-1);
                }
            }
            else
            {
                name = name.Replace("Season ", "");
                if (selected)
                {
                    results.Add(Int32.Parse(name));
                }
                else
                {
                    results.Remove(Int32.Parse(name));
                }
            }

        }
    }
}
