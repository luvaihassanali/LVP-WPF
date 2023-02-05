using CommunityToolkit.Mvvm.ComponentModel;
using LVP_WPF.Dialogs;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LVP_WPF.Windows
{
    /// <summary>
    /// Interaction logic for TvShowWindow.xaml
    /// </summary>

    [ObservableObject]
    public partial class TvShowWindow : Window
    {
        static internal TvShow tvShow;
        static internal bool cartoonShuffle = false;
        static internal int cartoonIndex = 0;
        static internal int cartoonLimit = 20;
        static internal List<TvShow> cartoons = new List<TvShow>();
        static internal List<Episode> cartoonShuffleList = new List<Episode>();
        static internal EpisodeWindowBox[] episodes;

        public static void Show(TvShow t)
        {
            tvShow = t;
            TvShowWindow window = new TvShowWindow();
            window.ShowName = tvShow.Name + " (" + tvShow.Date.GetValueOrDefault().Year + ")";
            window.Description = tvShow.Overview.Length > 377 ? tvShow.Overview.Substring(0, 377) + "..." : tvShow.Overview;
            string img = tvShow.Backdrop == null ? "Resources\\noPrevWide.png" : tvShow.Backdrop;
            window.Backdrop = Cache.LoadImage(img, 960);
            window.seasonButton.Content = "Season " + tvShow.CurrSeason.ToString();
            Episode[] episodes = tvShow.Seasons[tvShow.CurrSeason - 1].Episodes;
            window.Overlay = Cache.LoadImage("Resources\\play.png", 960);
            TvShowWindow.episodes = CreateEpisodeListItems(episodes);
            window.EpisodeListView.ItemsSource = TvShowWindow.episodes;
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
        private double scrollViewerOffset = 0;

        public TvShowWindow()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void TvShowWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MainWindow.tcpWorker.layoutPoint.tvControlList.Add(this.tvBackdrop);
            MainWindow.tcpWorker.layoutPoint.tvControlList.Add(this.seasonButton);
            MainWindow.gui.episodeScrollViewer = this.scrollViewer;
            MainWindow.gui.tvMovieCloseButton = this.closeButton;

            ItemContainerGenerator generator = EpisodeListView.ItemContainerGenerator;
            for (int j = 0; j < episodes.Length; j++)
            {
                ListViewItem container = (ListViewItem)generator.ContainerFromItem(episodes[j]);
                Image img = GuiModel.GetChildrenByType(container, typeof(Image), "episodeImage") as Image;
                MainWindow.tcpWorker.layoutPoint.tvControlList.Add(img);
            }
            MainWindow.tcpWorker.layoutPoint.Select("TvShowWindow");
        }

        static private EpisodeWindowBox[] CreateEpisodeListItems(Episode[] episodes)
        {
            EpisodeWindowBox[] episodeBoxes = new EpisodeWindowBox[episodes.Length];
            for (int i = 0; i < episodes.Length; i++)
            {
                string img = episodes[i].Backdrop == null ? "Resources\\noPrevWide.png" : episodes[i].Backdrop;
                string description;
                if (episodes[i].Overview != null)
                {
                    description = episodes[i].Overview.Length > 605 ? episodes[i].Overview.Substring(0, 605) + "..." : episodes[i].Overview;
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
                    Overlay = Cache.LoadImage("Resources\\play.png", 960),
                    Opacity = 0
                };
            }
            return episodeBoxes;
        }

        private void EpisodeListView_MouseMove(object sender, MouseEventArgs e)
        {
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(EpisodeListView, Mouse.GetPosition(EpisodeListView));
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

            for (int i = 0; i < EpisodeListView.Items.Count; i++)
            {
                EpisodeWindowBox ep = (EpisodeWindowBox)EpisodeListView.Items[i];
                if (ep == episodeWindowBox) continue;
                ep.Opacity = 0.0;
            }
        }

        private void EpisodeListView_MouseLeave(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < EpisodeListView.Items.Count; i++)
            {
                EpisodeWindowBox ep = (EpisodeWindowBox)EpisodeListView.Items[i];
                ep.Opacity = 0.0;
            }
        }

        private void EpisodeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            EpisodeWindowBox item = (EpisodeWindowBox)(sender as ListView).SelectedItem;
            if (item == null) return;
            Episode[] episodes = tvShow.Seasons[tvShow.CurrSeason - 1].Episodes;
            foreach (Episode episode in episodes)
            {
                if (item.Id == episode.Id)
                {
                    PlayerWindow.Show(episode, this);
                    EpisodeListView.SelectedIndex = -1;
                    return;
                }
            }
        }

        private void SeasonButton_Click(object sender, RoutedEventArgs e)
        {
            mainGrid.Opacity = 0.1;
            int prevIndex = tvShow.CurrSeason;
            int seasonIndex = SeasonWindow.Show(tvShow);
            if (seasonIndex != 0 && seasonIndex != prevIndex) Update(seasonIndex);
            mainGrid.Opacity = 1.0;
        }

        internal void Update(int seasonIndex)
        {
            scrollViewer.ScrollToHome();
            this.EpisodeListView.ItemsSource = null;
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

            TvShowWindow.episodes = CreateEpisodeListItems(episodes);
            this.EpisodeListView.ItemsSource = TvShowWindow.episodes;
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
                PlayerWindow.Show(tvShow.Seasons[0].Episodes[0], this);
            }
            else
            {
                PlayerWindow.Show(tvShow.LastEpisode, this);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        internal static void PlayRandomCartoons()
        {
            cartoonShuffle = true;
            cartoonLimit = Int32.Parse(ConfigurationManager.AppSettings["CartoonLimit"]);
            for (int i = 0; i < cartoonLimit; i++)
            {
                Episode e = GetRandomEpisode();
                cartoonShuffleList.Add(e);
            }
            Episode rndEpisode = cartoonShuffleList[cartoonIndex];
            PlayerWindow.Show(rndEpisode);
            cartoonShuffle = false;
        }

        internal static Random rnd = new Random();
        internal static Episode GetRandomEpisode()
        {
            Episode rndEpisode;
            int rndVal = rnd.Next(cartoons.Count);
            TvShow rndShow = cartoons[rndVal];
            rndVal = rnd.Next(rndShow.Seasons.Length);
            Season rndSeason = rndShow.Seasons[rndVal];
            rndVal = rnd.Next(rndSeason.Episodes.Length);
            rndEpisode = rndSeason.Episodes[rndVal];
            return rndEpisode;
        }

        private void ShowNameLabel_Click(object sender, MouseButtonEventArgs e)
        {
            int[] seasons = ResetSeasonDialog.Show(tvShow);
            if (seasons.Length == 0) return;
            ResetSeasons(tvShow, seasons);
        }

        private void ResetSeasons(TvShow tvShow, int[] seasons)
        {
            bool fill = false;
            if (seasons[seasons.Length - 1] == Int32.MaxValue) // fill
            {
                fill = true;
            }

            if (seasons[0] == 0)
            {
                tvShow.LastEpisode = null;
                for (int j = 0; j < tvShow.Seasons.Length; j++)
                {
                    Season currSeason = tvShow.Seasons[j];
                    for (int k = 0; k < currSeason.Episodes.Length; k++)
                    {
                        Episode currEpisode = currSeason.Episodes[k];
                        if (fill)
                        {
                            currEpisode.SavedTime = tvShow.RunningTime * 60000;
                        }
                        else
                        {
                            currEpisode.SavedTime = 0;
                        }
                    }
                }
                tvShow.CurrSeason = 1;
                tvShow.LastEpisode = null;
            }
            else
            {
                for (int i = 0; i < seasons.Length - 1; i++)
                {
                    int seasonIndex = seasons[i] - 1;
                    Season currSeason = tvShow.Seasons[seasonIndex];
                    for (int j = 0; j < currSeason.Episodes.Length; j++)
                    {
                        Episode currEpisode = currSeason.Episodes[j];
                        if (fill)
                        {
                            if (currEpisode.Length != 0)
                            {
                                currEpisode.SavedTime = currEpisode.Length;
                            }
                            else
                            {
                                currEpisode.SavedTime = tvShow.RunningTime * 60000;
                            }
                        }
                        else
                        {
                            currEpisode.SavedTime = 0;
                        }
                    }
                }
                tvShow.CurrSeason = fill ? seasons[0] + 1 : seasons[seasons.Length - 2];
                tvShow.LastEpisode = tvShow.Seasons[tvShow.CurrSeason - 1].Episodes[0];
            }
            Update(tvShow.CurrSeason);
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            scrollViewerOffset = e.VerticalOffset;
            if (e.VerticalOffset == 0)
            {
                closeButton.Visibility = Visibility.Visible;
            }
            else
            {
                closeButton.Visibility = Visibility.Hidden;
            }

            if (MainWindow.gui.scrollViewerAdjust)
            {
                MainWindow.gui.scrollViewerAdjust = false;
                double offsetPadding = e.VerticalChange > 0 ? 300 : -300;
                scrollViewer.ScrollToVerticalOffset(e.VerticalOffset + offsetPadding);
            }
        }

        private void TvShowWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewerOffset - 300);
            }
            else
            {

                scrollViewer.ScrollToVerticalOffset(scrollViewerOffset + 300);
            }
        }
    }
}