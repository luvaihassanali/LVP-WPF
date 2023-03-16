using CommunityToolkit.Mvvm.ComponentModel;
using LibVLCSharp.Shared;
using LVP_WPF.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
            window.Description = tvShow.Overview.Length > GuiModel.OVERVIEW_MAX_LEN ? tvShow.Overview.Substring(0, GuiModel.OVERVIEW_MAX_LEN) + "..." : tvShow.Overview;
            string img = tvShow.Backdrop == null ? "Resources\\noPrevWide.png" : tvShow.Backdrop;
            window.Backdrop = Cache.LoadImage(img, 960);
            window.seasonButton.Content = tvShow.CurrSeason == -1 ? "Extras" : "Season " + tvShow.CurrSeason.ToString();
            int index = tvShow.CurrSeason == -1 ? tvShow.Seasons.Length - 1 : tvShow.CurrSeason - 1;
            Episode[] episodes = tvShow.Seasons[index].Episodes;
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
            GetLanguageInfo(tvShow);
            TcpSerialListener.layoutPoint.tvControlList.Add(this.tvBackdrop);
            if (tvShow.MultiLang)
            {
                TcpSerialListener.layoutPoint.tvControlList.Add(this.langComboBox);
            }
            TcpSerialListener.layoutPoint.tvControlList.Add(this.seasonButton);
            MainWindow.gui.episodeScrollViewer = this.scrollViewer;
            MainWindow.gui.tvMovieCloseButton = this.closeButton;
            GenerateEpisodeItemContainers();
            TcpSerialListener.layoutPoint.Select("TvShowWindow");
        }

        private async Task GenerateEpisodeItemContainers()
        {
            await Task.Delay(500); // wait for content
            TcpSerialListener.layoutPoint.tvControlList.Clear();
            TcpSerialListener.layoutPoint.tvControlList.Add(this.tvBackdrop);
            if (tvShow.MultiLang)
            {
                TcpSerialListener.layoutPoint.tvControlList.Add(this.langComboBox);
            }
            TcpSerialListener.layoutPoint.tvControlList.Add(this.seasonButton);

            ItemContainerGenerator generator = EpisodeListView.ItemContainerGenerator;
            for (int j = 0; j < episodes.Length; j++)
            {
                ListViewItem container = (ListViewItem)generator.ContainerFromItem(episodes[j]);
                Image img = GuiModel.GetChildrenByType(container, typeof(Image), "episodeImage") as Image;
                TcpSerialListener.layoutPoint.tvControlList.Add(img);
            }
        }

        private void GetLanguageInfo(TvShow tvShow)
        {
            if (!tvShow.MultiLang) return;
            langComboBox.Visibility = Visibility.Visible;
            TcpSerialListener.layoutPoint.langComboBoxItems.Clear();

            string lang = "";
            if (tvShow.Name.Contains("("))
            {
                string item = tvShow.Name.Split("(")[1];
                item = item.Split(")")[0];
                langComboBox.Items.Add(item);
                lang = item;
                PlayerWindow.subtitleFile = true;
            }
            else
            {
                langComboBox.Items.Add("English");
                lang = "English";
            }

            foreach (string name in tvShow.MultiLangName)
            {
                if (!name.Contains("("))
                {
                    langComboBox.Items.Add("English");
                }
                else
                {
                    langComboBox.Items.Add(name.Replace(tvShow.Name, "").Replace("(", "").Replace(")", ""));
                }
            }
            langComboBox.SelectedValue = lang;
            langComboBox.SelectionChanged += LangComboBox_SelectionChanged;

            langComboBox.IsDropDownOpen = true;
            for (int i = 0; i < langComboBox.Items.Count; i++)
            {
                ComboBoxItem item = (ComboBoxItem)langComboBox.ItemContainerGenerator.ContainerFromIndex(i);
                Point pos = item.PointToScreen(new Point(0d, 0d));
                TcpSerialListener.layoutPoint.langComboBoxItems.Add(item);
                TcpSerialListener.layoutPoint.langComboBoxItemPts.Add(pos);
            }

            ScrollViewer langScrollViewer = (ScrollViewer)langComboBox.Template.FindName("DropDownSV", langComboBox);
            langScrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            MainWindow.gui.langScrollViewer = langScrollViewer;
            langComboBox.IsDropDownOpen = false;
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
                    description = episodes[i].Overview.Length > GuiModel.OVERVIEW_MAX_LEN ? episodes[i].Overview.Substring(0, GuiModel.OVERVIEW_MAX_LEN) + "..." : episodes[i].Overview;
                }
                else
                {
                    description = episodes[i].Name;
                }

                if (episodes[i].Name.Contains("#"))
                {
                    episodes[i].Name = episodes[i].Name.Replace("#", " & ");
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
            int index = tvShow.CurrSeason == -1 ? tvShow.Seasons.Length - 1 : tvShow.CurrSeason - 1;
            Episode[] episodes = tvShow.Seasons[index].Episodes;
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

        private async void SeasonButton_Click(object sender, RoutedEventArgs e)
        {
            loadGrid.Visibility = Visibility.Visible;
            TvShowWindow_Fade(0.1);
            int prevIndex = tvShow.CurrSeason;
            int seasonIndex = SeasonWindow.Show(tvShow);
            if (seasonIndex != 0 && seasonIndex != prevIndex)
            {
                tvShow.CurrSeason = seasonIndex;
                UpdateTvWindowSeasonChange(seasonIndex);
                await GenerateEpisodeItemContainers();
            }
            else
            {
                await Task.Delay(100);
            }
            TvShowWindow_Fade(1.0);
            loadGrid.Visibility = Visibility.Hidden;
        }

        private void TvShowWindow_Fade(double direction)
        {
            DoubleAnimation da = new DoubleAnimation();
            if (direction == 0.1)
            {
                da.From = 1.0;
                da.To = 0.1;
            }
            else
            {
                da.From = 0.1;
                da.To = 1.0;
            }
            da.Duration = new Duration(TimeSpan.FromMilliseconds(250));
            da.AutoReverse = false;
            da.RepeatBehavior = new RepeatBehavior(1);
            mainGrid.BeginAnimation(OpacityProperty, da);
        }

        internal void UpdateTvWindowSeasonChange(int seasonIndex)
        {
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
                PlayerWindow.Show(tvShow.Seasons[tvShow.CurrSeason - 1].Episodes[0], this);
            }
            else
            {
                foreach (Episode episode in tvShow.Seasons[tvShow.CurrSeason - 1].Episodes)
                {
                    if (episode.Compare(tvShow.LastEpisode))
                    {
                        PlayerWindow.Show(tvShow.LastEpisode, this);
                        return;
                    }
                }
                PlayerWindow.Show(tvShow.Seasons[tvShow.CurrSeason - 1].Episodes[0], this);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            if (!TcpSerialListener.layoutPoint.incomingSerialMsg)
            {
                TcpSerialListener.layoutPoint.CloseCurrWindow(false);
            }
            else
            {
                TcpSerialListener.layoutPoint.incomingSerialMsg = false;
            }
            TcpSerialListener.layoutPoint.langComboBoxItems.Clear();
            TcpSerialListener.layoutPoint.langComboBoxItemPts.Clear();
        }

        internal static void PlayRandomCartoons()
        {
            cartoonIndex = 0;
            cartoonShuffleList.Clear();
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

        private async void ResetSeasons(TvShow tvShow, int[] seasons)
        {
            loadGrid.Visibility = Visibility.Visible;
            TvShowWindow_Fade(0.1);
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
                    int seasonIndex = tvShow.CurrSeason == -1 ? tvShow.Seasons.Length - 1 : tvShow.CurrSeason - 1;
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
                tvShow.LastEpisode = null;
            }
            UpdateTvWindowSeasonChange(tvShow.CurrSeason);
            await GenerateEpisodeItemContainers();
            scrollViewer.ScrollToHome();
            TvShowWindow_Fade(1.0);
            loadGrid.Visibility = Visibility.Hidden;
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

        private async void LangComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            loadGrid.Visibility = Visibility.Visible;
            TvShowWindow_Fade(0.1);
            if (langComboBox.SelectedIndex == 0)
            {
                PlayerWindow.subtitleFile = false;
            }
            else
            {
                PlayerWindow.subtitleFile = true;
            }

            if (!tvShow.Name.Contains(langComboBox.SelectedValue.ToString()))
            {
                Cache.SwitchMultiLangTvIndex(tvShow, langComboBox.SelectedValue.ToString());
                this.ShowName = tvShow.Name.Contains("(") ? tvShow.Name : tvShow.Name + " (" + tvShow.Date.GetValueOrDefault().Year + ")";
                this.Description = tvShow.Overview.Length > GuiModel.OVERVIEW_MAX_LEN ? tvShow.Overview.Substring(0, GuiModel.OVERVIEW_MAX_LEN) + "..." : tvShow.Overview;
                UpdateTvWindowSeasonChange(tvShow.CurrSeason);
                await GenerateEpisodeItemContainers();
            }
            else
            {
                await Task.Delay(100);
            }
            TvShowWindow_Fade(1.0);
            loadGrid.Visibility = Visibility.Hidden;
        }

        private async void LangComboBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (langComboBox.IsDropDownOpen)
            {
                await Task.Delay(100);
            }
            TcpSerialListener.layoutPoint.Select("languageDropdown");
        }

        private void LangComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
        }

        private void EpisodeListView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
        }
    }
}