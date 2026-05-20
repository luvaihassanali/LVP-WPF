using CommunityToolkit.Mvvm.ComponentModel;
using LVP_WPF.Dialogs;
using LVP_WPF.Models;
using LVP_WPF.Services;
using LVP_WPF.Util;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace LVP_WPF.Windows
{
    [ObservableObject]
    public partial class TvShowWindow : Window
    {
        private const int OverviewMaxLen = 370;

        /// <summary>
        /// Trim a TMDB Overview string for display. Anything longer than
        /// OverviewMaxLen gets cut to that length with an ellipsis appended.
        /// </summary>
        private static string TruncateOverview(string overview)
            => overview.Length > OverviewMaxLen ? $"{overview.Substring(0, OverviewMaxLen)}..." : overview;

        static internal TvShow tvShow;
        static internal EpisodeWindowBox[] episodes;
        static internal List<TvShow> cartoons = new List<TvShow>();

        public static void Show(TvShow t)
        {
            tvShow = t;
            TvShowWindow window = new TvShowWindow();
            window.ShowName = tvShow.Name.Contains("(") ? tvShow.Name.Split(" (")[0] : tvShow.Name;
            window.ShowName += $" ({tvShow.Date.GetValueOrDefault().Year})";
            window.Description = TruncateOverview(tvShow.Overview);
            window.Backdrop = ImageLoader.LoadBackdrop(tvShow.Backdrop);
            window.seasonButton.Content = tvShow.CurrSeason == -1 ? "Extras" : $"Season {tvShow.CurrSeason}";
            int index = tvShow.CurrSeason == -1 ? tvShow.Seasons.Length - 1 : tvShow.CurrSeason - 1;
            Episode[] episodes = tvShow.Seasons[index].Episodes;
            window.Overlay = ImageLoader.PlayOverlay;
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
        private bool langChanged = false;

        public TvShowWindow()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void TvShowWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Height = (int)SystemParameters.PrimaryScreenHeight;
            GetLanguageInfo(tvShow);
            TcpSerialListener.layoutPoint.tvControlList.Add(this.tvBackdrop);
            if (tvShow.MultiLang)
            {
                TcpSerialListener.layoutPoint.tvControlList.Add(this.langComboBox);
            }
            TcpSerialListener.layoutPoint.tvControlList.Add(this.seasonButton);
            MainWindow.gui.episodeScrollViewer = this.scrollViewer;
            MainWindow.gui.tvMovieCloseButton = this.closeButton;
            _ = GenerateEpisodeItemContainers();
            TcpSerialListener.layoutPoint.Select("TvShowWindow");
        }

        private async void TvShowWindow_Closing(object sender, CancelEventArgs e)
        {
            if (langChanged)
            {
                SwitchMultiLangTvIndex(tvShow, "English");
                await Task.Delay(1000);
            }
        }

        private async Task GenerateEpisodeItemContainers()
        {
            await Task.Delay(500); // wait for content
            TcpSerialListener.layoutPoint.tvControlList.Clear();
            TcpSerialListener.layoutPoint.tvControlList.Add(this.tvBackdrop);
            if (tvShow.MultiLang)
            {
                TcpSerialListener.layoutPoint.tvControlList.Add(this.langComboBox);
                bool toggleAtIndex1 = TcpSerialListener.layoutPoint.tvControlList[1] is ToggleButton;
                if (this.langComboBox.SelectedIndex != 0 && !toggleAtIndex1)
                {
                    TcpSerialListener.layoutPoint.tvControlList.Insert(1, toggleButton);
                }
                else if (this.langComboBox.SelectedIndex == 0 && toggleAtIndex1)
                {
                    TcpSerialListener.layoutPoint.tvControlList.RemoveAt(1);
                }
            }
            TcpSerialListener.layoutPoint.tvControlList.Add(this.seasonButton);

            ItemContainerGenerator generator = EpisodeListView.ItemContainerGenerator;
            for (int j = 0; j < episodes.Length; j++)
            {
                ListViewItem container = (ListViewItem)generator.ContainerFromItem(episodes[j]);
                Image img = WpfTreeHelpers.GetChildrenByType(container, typeof(Image), "episodeImage") as Image;
                TcpSerialListener.layoutPoint.tvControlList.Add(img);
            }
        }

        // Reset SavedTime on a batch of episodes. fill==false clears progress
        // to 0; fill==true marks each episode as "watched" - using its
        // measured Length when available + preferExactLength, otherwise
        // falling back to the show's nominal RunningTime.
        private static void ResetEpisodeProgress(Episode[] episodes, bool fill, int runningTimeMs, bool preferExactLength)
        {
            foreach (Episode ep in episodes)
            {
                if (!fill) { ep.SavedTime = 0; continue; }
                ep.SavedTime = (preferExactLength && ep.Length != 0) ? ep.Length : runningTimeMs;
            }
        }

        // Returns the language tag from a "Show Name (Language)" string, or
        // "English" if no parenthesized tag is present. Previously inlined
        // twice with subtly different parsers; the second site left a stray
        // leading space (Replace("name", "") doesn't strip the trailing space).
        private static string ExtractLanguageOrDefault(string showOrLangName)
        {
            int open = showOrLangName.IndexOf('(');
            if (open < 0) return "English";
            int close = showOrLangName.IndexOf(')', open + 1);
            return close > open
                ? showOrLangName.Substring(open + 1, close - open - 1)
                : "English";
        }

        private void GetLanguageInfo(TvShow tvShow)
        {
            if (!tvShow.MultiLang)
            {
                return;
            }

            toggleButton.IsChecked = true;
            langComboBox.Visibility = Visibility.Visible;
            TcpSerialListener.layoutPoint.langComboBoxItems.Clear();

            string lang = ExtractLanguageOrDefault(tvShow.Name);
            langComboBox.Items.Add(lang);
            if (tvShow.Name.Contains("("))
            {
                SubtitleConfig.HasSrtFile = true;
            }

            foreach (string name in tvShow.MultiLangName)
            {
                langComboBox.Items.Add(ExtractLanguageOrDefault(name));
            }
            langComboBox.SelectedValue = lang;
            langComboBox.SelectionChanged += LangComboBox_SelectionChanged;

            langComboBox.IsDropDownOpen = true;
            TcpSerialListener.layoutPoint.CaptureComboBoxItems(langComboBox, capturePositions: true);

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
                string description;
                if (episodes[i].Overview != null)
                {
                    description = TruncateOverview(episodes[i].Overview);
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
                    Image = ImageLoader.LoadBackdrop(episodes[i].Backdrop, 300),
                    Progress = (int)episodes[i].SavedTime,
                    Total = (int)total,
                    Overlay = ImageLoader.PlayOverlay,
                    Opacity = 0
                };
            }
            return episodeBoxes;
        }

        private void EpisodeListView_MouseMove(object sender, MouseEventArgs e)
        {
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(EpisodeListView, Mouse.GetPosition(EpisodeListView));
            if (hitTestResult == null)
            {
                return;
            }

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

            foreach (EpisodeWindowBox ep in EpisodeListView.Items)
            {
                if (ep != episodeWindowBox) ep.Opacity = 0.0;
            }
        }

        private void EpisodeListView_MouseLeave(object sender, MouseEventArgs e)
        {
            foreach (EpisodeWindowBox ep in EpisodeListView.Items)
            {
                ep.Opacity = 0.0;
            }
        }

        private void EpisodeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            EpisodeWindowBox item = (EpisodeWindowBox)(sender as ListView).SelectedItem;
            if (item == null)
            {
                return;
            }

            int index = item.Id < 0 ? tvShow.Seasons.Length - 1 : tvShow.CurrSeason - 1;
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
            => FadeHelper.Fade(mainGrid, fadeOut: direction == 0.1);

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
                this.seasonButton.Content = $"Season {seasonIndex}";
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
            // Resume LastEpisode if it lives in the currently selected season;
            // otherwise fall back to the first episode of that season.
            Episode[] currentSeason = tvShow.Seasons[tvShow.CurrSeason - 1].Episodes;
            Episode toPlay = currentSeason[0];
            if (tvShow.LastEpisode != null)
            {
                foreach (Episode ep in currentSeason)
                {
                    if (ep.Compare(tvShow.LastEpisode))
                    {
                        toPlay = tvShow.LastEpisode;
                        break;
                    }
                }
            }
            PlayerWindow.Show(toPlay, this);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            TcpSerialListener.layoutPoint.NotifyWindowClosedFromUI();
            TcpSerialListener.layoutPoint.langComboBoxItems.Clear();
            TcpSerialListener.layoutPoint.langComboBoxItemPts.Clear();
        }

        private void ShowNameLabel_Click(object sender, MouseButtonEventArgs e)
        {
            int[] seasons = ResetSeasonDialog.Show(tvShow);
            if (seasons.Length == 0)
            {
                return;
            }

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

            int runningTimeMs = tvShow.RunningTime * 60000;
            if (seasons[0] == 0)
            {
                // Reset every season. Episodes have no measured Length context
                // here so fill uniformly to the show's RunningTime.
                foreach (Season s in tvShow.Seasons)
                {
                    ResetEpisodeProgress(s.Episodes, fill, runningTimeMs, preferExactLength: false);
                }
                tvShow.CurrSeason = 1;
            }
            else
            {
                // Reset only the current season. The original outer "for i in
                // 1..seasons.Length" loop was dead repetition - its body never
                // read i and the inner ops are idempotent.
                int seasonIndex = tvShow.CurrSeason == -1 ? tvShow.Seasons.Length - 1 : tvShow.CurrSeason - 1;
                ResetEpisodeProgress(tvShow.Seasons[seasonIndex].Episodes, fill, runningTimeMs, preferExactLength: true);
                tvShow.CurrSeason = fill ? seasons[0] + 1 : seasons[seasons.Length - 2];
            }
            tvShow.LastEpisode = null;
            UpdateTvWindowSeasonChange(tvShow.CurrSeason);
            await GenerateEpisodeItemContainers();
            scrollViewer.ScrollToHome();
            TvShowWindow_Fade(1.0);
            loadGrid.Visibility = Visibility.Hidden;
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            scrollViewerOffset = e.VerticalOffset;
            closeButton.Visibility = e.VerticalOffset == 0 ? Visibility.Visible : Visibility.Hidden;
            ScrollHelper.ApplyAdjust(scrollViewer, e);
        }

        private void TvShowWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
            => ScrollHelper.StepFromWheel(scrollViewer, scrollViewerOffset, e);

        private async void LangComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            loadGrid.Visibility = Visibility.Visible;
            TvShowWindow_Fade(0.1);

            bool english = langComboBox.SelectedIndex == 0;
            bool toggleAtIndex1 = TcpSerialListener.layoutPoint.tvControlList[1] is ToggleButton;
            SubtitleConfig.HasSrtFile = !english;
            toggleButton.Visibility = english ? Visibility.Hidden : Visibility.Visible;
            if (english && toggleAtIndex1)
            {
                TcpSerialListener.layoutPoint.tvControlList.RemoveAt(1);
            }
            else if (!english && !toggleAtIndex1)
            {
                TcpSerialListener.layoutPoint.tvControlList.Insert(1, toggleButton);
            }

            if (!tvShow.Name.Contains(langComboBox.SelectedValue.ToString()))
            {
                SwitchMultiLangTvIndex(tvShow, langComboBox.SelectedValue.ToString());
                this.ShowName = tvShow.Name.Contains('(') ? tvShow.Name : $"{tvShow.Name} ({tvShow.Date.GetValueOrDefault().Year})";
                this.Description = TruncateOverview(tvShow.Overview);
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

        // To-do MultiLang: if 3+ langs then need to preserve same order as build cache (alphabetical?)
        internal void SwitchMultiLangTvIndex(TvShow tvShow, string lang)
        {
            int index = 0;
            lang = lang.Trim();

            for (int i = 0; i < tvShow.MultiLangSeasons.Count; i++)
            {
                if (lang.Equals("English") && tvShow.MultiLangName[i].Equals(tvShow.Name.Split(" (")[0]))
                {
                    langChanged = false;
                    index = i;
                    break;
                }
                else if (tvShow.MultiLangName[i].Contains(lang))
                {
                    langChanged = true;
                    index = i;
                    break;
                }
            }

            Log.Information("Switching language for {TvShowName} to {Lang}", tvShow.Name, lang);
            tvShow.SwapWithLanguageIndex(index);
        }

        private async void LangComboBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (langComboBox.IsDropDownOpen)
            {
                await Task.Delay(100);
            }
            TcpSerialListener.layoutPoint.Select("languageDropdown");
        }

        // Stop arrow/enter keys from triggering ComboBox/ListView's built-in
        // selection behavior - the joystick-driven nav owns that state.
        private void LangComboBox_PreviewKeyDown(object sender, KeyEventArgs e) => e.Handled = true;
        private void EpisodeListView_PreviewKeyDown(object sender, KeyEventArgs e) => e.Handled = true;

        // Both Checked and Unchecked just mirror IsChecked into the global
        // SubtitleConfig; one body covers both.
        private void toggleButton_Checked(object sender, RoutedEventArgs e) => SyncSubtitleEnabled();
        private void toggleButton_Unchecked(object sender, RoutedEventArgs e) => SyncSubtitleEnabled();
        private void SyncSubtitleEnabled() => SubtitleConfig.EnableSubtitles = toggleButton.IsChecked == true;

        internal static void PlayRandomCartoons()
        {
            PlaybackSession.StartCartoonShuffle(AppConfig.CartoonLimit, cartoons);
            TcpSerialListener.layoutPoint.playerWindowActive = true;
            PlayerWindow.Show(PlaybackSession.CartoonShuffleQueue[PlaybackSession.CartoonShuffleIndex]);
        }

        internal static void PlayHistoryList()
        {
            PlaybackSession.StartHistoryWatch();
            TcpSerialListener.layoutPoint.playerWindowActive = true;

            // First time through: pin to the start of the history list. After
            // that, the saved HistoryIndex is whatever the last MediaPlayer_EndReached
            // advanced to, so just resume from there.
            if (MainWindow.model.HistoryEpisode == null)
            {
                MainWindow.model.HistoryEpisode = MainWindow.model.HistoryList[0];
                MainWindow.model.HistoryIndex = 0;
            }

            PlayerWindow.Show(MainWindow.model.HistoryList[MainWindow.model.HistoryIndex]);
        }
    }
}