using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace LVP_WPF.Windows
{
    /// <summary>
    /// Interaction logic for TvShowWindow.xaml
    /// </summary>

    [ObservableObject]
    public partial class TvShowWindow : Window
    {
        static private TvShow tvShow;

        public static void Show(TvShow t)
        {
            tvShow = t;
            TvShowWindow window = new TvShowWindow();
            window.ShowName = tvShow.Name + " (" + tvShow.Date.GetValueOrDefault().Year + ")";
            window.Description = tvShow.Overview.Length > 377 ? tvShow.Overview.Substring(0, 377) + "..." : tvShow.Overview;
            string img = tvShow.Backdrop == null ? "Resources/noPrevWide.png" : tvShow.Backdrop;
            window.Backdrop = Cache.LoadImage(img, 960);
            window.seasonButton.Content = "Season " + tvShow.CurrSeason.ToString();
            Episode[] episodes = tvShow.Seasons[tvShow.CurrSeason - 1].Episodes;
            window.Overlay = Cache.LoadImage("Resources/play.png", 960);
            window.EpisodeBox.ItemsSource = CreateEpisodeBoxes(episodes);
            window.ShowDialog();
        }

        [ObservableProperty]
        private string showName;
        [ObservableProperty]
        private string runningTime;
        [ObservableProperty]
        private string description;
        [ObservableProperty]
        private BitmapImage backdrop;
        [ObservableProperty]
        private BitmapImage overlay;

        public TvShowWindow()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void Backdrop_MouseEnter(object sender, MouseEventArgs e)
        {
            this.PlayOverlay.Opacity = 1.0;
        }

        private void Backdrop_MouseLeave(object sender, MouseEventArgs e)
        {
            this.PlayOverlay.Opacity = 0;
        }

        private void Play_Click(object sender, MouseButtonEventArgs e)
        {
            if (tvShow.LastEpisode == null)
            {
                PlayerWindow.Show(tvShow.Seasons[0].Episodes[0]);
            }
            else
            {
                PlayerWindow.Show(tvShow.LastEpisode);
            }
        }

        private void EpisodeListView_MouseMove(object sender, MouseEventArgs e)
        {
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(EpisodeBox, Mouse.GetPosition(EpisodeBox));
            if (hitTestResult == null) return;
            DependencyObject item = hitTestResult.VisualHit;
            while (item != null && !(item is ListViewItem))
            {
                item = VisualTreeHelper.GetParent(item);
            }

            EpisodeWindowBox episodeWindowBox = null;
            if (item != null)
            {
                ListBoxItem listItem = (ListBoxItem)item;
                episodeWindowBox = (EpisodeWindowBox)listItem.DataContext;
                episodeWindowBox.Opacity = 1.0;
            }

            for (int i = 0; i < EpisodeBox.Items.Count; i++)
            {
                EpisodeWindowBox ep = (EpisodeWindowBox)EpisodeBox.Items[i];
                if (ep == episodeWindowBox) continue;
                ep.Opacity = 0.0;
            }
        }

        private void Update(int seasonIndex)
        {
            this.EpisodeBox.ItemsSource = null;
            Episode[] episodes;
            if (seasonIndex == -1)
            {
                this.seasonButton.Content = "Extras";
                episodes = tvShow.Seasons[tvShow.Seasons.Length - 1].Episodes;
            }
            else
            {
                this.seasonButton.Content = "Season " + seasonIndex.ToString();
                episodes = tvShow.Seasons[seasonIndex - 1].Episodes;
            }
            this.EpisodeBox.ItemsSource = CreateEpisodeBoxes(episodes);
        }

        static private EpisodeWindowBox[] CreateEpisodeBoxes(Episode[] episodes)
        {
            EpisodeWindowBox[] episodeBoxes = new EpisodeWindowBox[episodes.Length];
            for (int i = 0; i < episodes.Length; i++)
            {
                string img = episodes[i].Backdrop == null ? "Resources/noPrevWide.png" : episodes[i].Backdrop;
                string description;
                if (episodes[i].Overview != null)
                {
                    description = episodes[i].Overview.Length > 610 ? episodes[i].Overview.Substring(0, 610) + "..." : episodes[i].Overview;
                }
                else
                {
                    description = episodes[i].Name;
                }

                long total = episodes[i].Length == 0 ? 1 : episodes[i].Length;
                episodeBoxes[i] = new EpisodeWindowBox
                {
                    Id = episodes[i].Id,
                    Name = episodes[i].Name,
                    Description = description,
                    Image = Cache.LoadImage(img, 300),
                    Progress = (int)episodes[i].SavedTime,
                    Total = (int)total,
                    Overlay = Cache.LoadImage("Resources/play.png", 960),
                    Opacity = 0
                };
            }
            return episodeBoxes;
        }

        private void SeasonButton_Click(object sender, RoutedEventArgs e)
        {
            mainGrid.Opacity = 0.1;
            int seasonIndex = SeasonWindow.Show(tvShow);
            if (seasonIndex != 0) Update(seasonIndex);
            mainGrid.Opacity = 1.0;
        }

        private void EpisodeListView_MouseLeave(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < EpisodeBox.Items.Count; i++)
            {
                EpisodeWindowBox ep = (EpisodeWindowBox)EpisodeBox.Items[i];
                ep.Opacity = 0.0;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void EpisodeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            EpisodeWindowBox item = (EpisodeWindowBox)(sender as ListView).SelectedItem;
            Episode[] episodes = tvShow.Seasons[tvShow.CurrSeason - 1].Episodes;
            foreach (Episode episode in episodes)
            {
                if (item.Id == episode.Id)
                {
                    PlayerWindow.Show(episode);
                    return;
                }
            }
        }
    }
}
