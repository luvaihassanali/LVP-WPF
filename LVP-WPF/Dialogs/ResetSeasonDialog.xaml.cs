using CommunityToolkit.Mvvm.ComponentModel;
using LVP_WPF.Models;
using LVP_WPF.Util;
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

            // First entry is the "All" pseudo-row; the rest are per-season.
            OptionWindowBox[] seasonBoxes = new OptionWindowBox[tvShow.Seasons.Length + 1];
            seasonBoxes[0] = new OptionWindowBox { Id = 0, Name = "  All" };
            for (int i = 0; i < tvShow.Seasons.Length; i++)
            {
                Season season = tvShow.Seasons[i];
                string name = season.Id == -1 ? "  Extras" : $"   Season {season.Id}";
                seasonBoxes[i + 1] = new OptionWindowBox { Id = season.Id, Name = name };
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
            => results.Add(SeasonIdFromCheckbox((CheckBox)sender));

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
            => results.Remove(SeasonIdFromCheckbox((CheckBox)sender));

        // Map a CheckBox's content label back to the season id encoding used
        // by the results list. "  All" -> 0, "  Extras" -> -1, "  Season N" -> N.
        private static int SeasonIdFromCheckbox(CheckBox c)
        {
            string name = c.Content.ToString();
            if (name.Equals("  All")) return 0;
            if (name.Equals("  Extras")) return -1;
            return Int32.Parse(name.Replace("  Season ", ""));
        }

        // XAML still wires this handler; keep the empty body so the binding
        // resolves. The original click-toggles-checkbox logic was disabled
        // long ago when row click started auto-toggling via the ListView
        // selector instead.
        private void SeasonListView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) { }
    }
}
