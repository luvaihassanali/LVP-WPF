using CommunityToolkit.Mvvm.ComponentModel;
using LibVLCSharp.Shared;
using LVP_WPF.Services;
using LVP_WPF.Util;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace LVP_WPF.Windows
{
    [ObservableObject]
    public partial class MovieWindow : Window
    {
        private static Movie movie;

        public static void Show(Movie m)
        {
            SubtitleConfig.Track = Int32.MaxValue;
            SubtitleConfig.HasSrtFile = false;
            movie = m;

            TimeSpan temp = TimeSpan.FromMinutes(movie.RunningTime);
            string hour = temp.Hours > 1 ? "hours " : "hour ";
            MovieWindow window = new MovieWindow
            {
                MovieName = $"{movie.Name} ({movie.Date.GetValueOrDefault().Year})",
                RunningTime = $"Running time: {temp.Hours} {hour} {temp.Minutes} minutes",
                Description = movie.Overview, //.Length > 1011 ? $"{movie.Overview.Substring(0, 1011)}..." : movie.Overview;
                Backdrop = ImageLoader.LoadBackdrop(movie.Backdrop),
                Overlay = ImageLoader.Load("Resources\\play.png", 960)
            };
            window.ShowDialog();
        }

        [ObservableProperty]
        private string movieName;
        [ObservableProperty]
        private string runningTime;
        [ObservableProperty]
        private string description;
        [ObservableProperty]
        private BitmapImage backdrop;
        [ObservableProperty]
        private BitmapImage overlay;
        private ScrollViewer langScrollViewer = null;
        private double scrollViewerOffset = 0;
        private bool srtFileExists = false;

        public MovieWindow()
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
            PlayerWindow.Show(movie);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            TcpSerialListener.layoutPoint.NotifyWindowClosedFromUI();
        }

        private void MovieWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Height = (int)SystemParameters.PrimaryScreenHeight;
            GetLanguageInfo(movie);
            MainWindow.gui.tvMovieCloseButton = this.closeButton;
            TcpSerialListener.layoutPoint.movieBackdrop = this.movieBackdrop;
            if (subTrackComboBox.Items.Count > 1)
            {
                TcpSerialListener.layoutPoint.movieLangComboBox = this.subTrackComboBox;

                subTrackComboBox.IsDropDownOpen = true;
                for (int i = 0; i < subTrackComboBox.Items.Count; i++)
                {
                    ComboBoxItem item = (ComboBoxItem)subTrackComboBox.ItemContainerGenerator.ContainerFromIndex(i);
                    Point pos = item.PointToScreen(new Point(0d, 0d));
                    TcpSerialListener.layoutPoint.langComboBoxItems.Add(item);
                    TcpSerialListener.layoutPoint.langComboBoxItemPts.Add(pos);
                }
                subTrackComboBox.IsDropDownOpen = false;
            }
            TcpSerialListener.layoutPoint.Select("MovieWindow", true);
        }

        private void GetLanguageInfo(Movie movie)
        {
            LibVLCSharp.Shared.Media media = new LibVLCSharp.Shared.Media(PlayerWindow.libVLC, movie.Path, FromType.FromPath);
            Task.Run(async () => { await media.Parse(MediaParseOptions.ParseLocal); }).Wait();

            subTrackComboBox.Items.Add("Subtitles (none)");
            foreach (MediaTrack track in media.Tracks)
            {
                switch (track.TrackType)
                {
                    //case TrackType.Audio:
                    //case TrackType.Video:
                    case TrackType.Text:
                        subTrackComboBox.Items.Add(track.Description);
                        break;
                }
            }

            if (subTrackComboBox.Items.Count > 1)
            {
                subTrackComboBox.Visibility = Visibility.Visible;
                subTrackComboBox.SelectedIndex = 0;
                return;
            }

            string[] pathParts = movie.Path.Split("\\");
            string path = "";
            for (int i = 0; i < pathParts.Length - 1; i++)
            {
                path += $"{pathParts[i]}\\";
            }
            string name = pathParts[pathParts.Length - 1].Split('.')[0];

            string[] movieFiles = Directory.GetFiles(path);
            if (movieFiles.Length == 1)
            {
                return;
            }


            srtFileExists = true;
            subTrackComboBox.Items.Add("English");
            subTrackComboBox.Visibility = Visibility.Visible;
            subTrackComboBox.SelectedIndex = 0;

            subTrackComboBox.IsDropDownOpen = true;
            for (int i = 0; i < subTrackComboBox.Items.Count; i++)
            {
                ComboBoxItem item = (ComboBoxItem)subTrackComboBox.ItemContainerGenerator.ContainerFromIndex(i);
                TcpSerialListener.layoutPoint.langComboBoxItems.Add(item);
            }

            langScrollViewer = (ScrollViewer)subTrackComboBox.Template.FindName("DropDownSV", subTrackComboBox);
            langScrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            MainWindow.gui.langScrollViewer = langScrollViewer;
            subTrackComboBox.IsDropDownOpen = false;

        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            scrollViewerOffset = e.VerticalOffset;
            closeButton.Visibility = e.VerticalOffset == 0 ? Visibility.Visible : Visibility.Hidden;
            ScrollHelper.ApplyAdjust(langScrollViewer, e);
        }

        private void SubTrackComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (subTrackComboBox.SelectedIndex == 0)
            {
                SubtitleConfig.Track = Int32.MaxValue;
                SubtitleConfig.HasSrtFile = false;
            }
            else
            {
                if (srtFileExists)
                {
                    SubtitleConfig.HasSrtFile = true;
                    return;
                }
                SubtitleConfig.Track = subTrackComboBox.SelectedIndex - 1;
            }
        }

        private async void LangComboBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (subTrackComboBox.IsDropDownOpen)
            {
                await Task.Delay(100);
            }
            TcpSerialListener.layoutPoint.Select("languageDropdown");
        }

        private void LangComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
        }
    }
}
