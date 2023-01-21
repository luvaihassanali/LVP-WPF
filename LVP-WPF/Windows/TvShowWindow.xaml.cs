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
        static private TvShowWindow tw;

        public static void Show(TvShow t)
        {
            tvShow = t;
            TvShowWindow window = new TvShowWindow();
            window.ShowName = tvShow.Name + " (" + tvShow.Date.GetValueOrDefault().Year + ")";
            window.Description = tvShow.Overview.Length > 377 ? tvShow.Overview.Substring(0, 377) + "..." : tvShow.Overview;
            window.Backdrop = Cache.LoadImage(tvShow.Backdrop, 960);
            window.seasonButton.Content = "Season " + tvShow.CurrSeason.ToString();
            Episode[] episodes = tvShow.Seasons[tvShow.CurrSeason - 1].Episodes;
            window.Overlay = Cache.LoadImage("Resources/play.png", 960);
            window.EpisodeBox.ItemsSource = CreateEpisodeBoxes(episodes);
            tw = window;
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

        private void Image_MouseEnter(object sender, MouseEventArgs e)
        {
            tw.PlayOverlay.Opacity = 1.0;
        }

        private void Image_MouseLeave(object sender, MouseEventArgs e)
        {
            tw.PlayOverlay.Opacity = 0;
        }

        private void Play_Click(object sender, MouseButtonEventArgs e)
        {
            Trace.WriteLine("click");
        }

        private void SmallPlay_Click(object sender, MouseButtonEventArgs e)
        {
            Trace.WriteLine("small click");
        }

        private void ListView_MouseMove(object sender, MouseEventArgs e)
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
            tvShow.CurrSeason = seasonIndex;
            tw.seasonButton.Content = "Season " + tvShow.CurrSeason.ToString();
            tw.EpisodeBox.ItemsSource = null;
            Episode[] episodes = tvShow.Seasons[tvShow.CurrSeason - 1].Episodes;
            tw.EpisodeBox.ItemsSource = CreateEpisodeBoxes(episodes);
        }

        static private EpisodeWindowBox[] CreateEpisodeBoxes(Episode[] episodes)
        {
            EpisodeWindowBox[] episodeBoxes = new EpisodeWindowBox[episodes.Length];
            for (int i = 0; i < episodes.Length; i++)
            {
                episodeBoxes[i] = new EpisodeWindowBox
                {
                    Id = episodes[i].Id,
                    Name = episodes[i].Name,
                    Description = episodes[i].Overview,
                    Image = Cache.LoadImage(episodes[i].Backdrop, 300),
                    Progress = (int)episodes[i].SavedTime,
                    Total = (int)episodes[i].Length,
                    Overlay = Cache.LoadImage("Resources/play.png", 960),
                    Opacity = 0
                };
            }
            return episodeBoxes;
        }

        private void Button_Season_Click(object sender, RoutedEventArgs e)
        {
            mainGrid.Opacity = 0.1;
            int seasonIndex = SeasonWindow.Show(tvShow);
            if (seasonIndex != -1)
            {
                Trace.WriteLine("szn: " + seasonIndex);
                Update(seasonIndex);
            }
            mainGrid.Opacity = 1.0;
        }

        private void ListView_MouseLeave(object sender, MouseEventArgs e)
        {

            for (int i = 0; i < EpisodeBox.Items.Count; i++)
            {
                EpisodeWindowBox ep = (EpisodeWindowBox)EpisodeBox.Items[i];
                ep.Opacity = 0.0;
            }
        }
    }
}
