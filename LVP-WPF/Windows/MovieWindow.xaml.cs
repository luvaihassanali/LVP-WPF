using CommunityToolkit.Mvvm.ComponentModel;
using LibVLCSharp.Shared;
using LVP_WPF.Services;
using LVP_WPF.Util;
using Serilog;
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
            Log.Information("MovieWindow.Show: '{Movie}' ({Year})", m.Name, m.Date.GetValueOrDefault().Year);
            SubtitleConfig.Track = Int32.MaxValue;
            SubtitleConfig.HasSrtFile = false;
            movie = m;

            TimeSpan temp = TimeSpan.FromMinutes(movie.RunningTime);
            string hourUnit = temp.Hours == 1 ? "hour" : "hours";
            string minuteUnit = temp.Minutes == 1 ? "minute" : "minutes";
            MovieWindow window = new MovieWindow
            {
                MovieName = $"{movie.Name} ({movie.Date.GetValueOrDefault().Year})",
                RunningTime = $"Running time: {temp.Hours} {hourUnit} {temp.Minutes} {minuteUnit}",
                Description = movie.Overview,
                Backdrop = ImageLoader.LoadBackdrop(movie.Backdrop),
                Overlay = ImageLoader.PlayOverlay
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
                TcpSerialListener.layoutPoint.CaptureComboBoxItems(subTrackComboBox, capturePositions: true);
                subTrackComboBox.IsDropDownOpen = false;
            }
            TcpSerialListener.layoutPoint.Select("MovieWindow", true);
        }

        private void GetLanguageInfo(Movie movie)
        {
            LibVLCSharp.Shared.Media media = new LibVLCSharp.Shared.Media(PlayerWindow.libVLC, movie.Path, FromType.FromPath);
            try
            {
                Task.Run(async () => { await media.Parse(MediaParseOptions.ParseLocal); }).Wait();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "MovieWindow.GetLanguageInfo: media.Parse failed for {Path}", movie.Path);
            }

            subTrackComboBox.Items.Add("Subtitles (none)");
            int embeddedSubs = 0;
            foreach (MediaTrack track in media.Tracks)
            {
                switch (track.TrackType)
                {
                    //case TrackType.Audio:
                    //case TrackType.Video:
                    case TrackType.Text:
                        subTrackComboBox.Items.Add(track.Description);
                        embeddedSubs++;
                        break;
                }
            }
            Log.Information("MovieWindow.GetLanguageInfo: parsed '{Movie}' - {Embedded} embedded subtitle track(s)",
                movie.Name, embeddedSubs);

            if (subTrackComboBox.Items.Count > 1)
            {
                subTrackComboBox.Visibility = Visibility.Visible;
                subTrackComboBox.SelectedIndex = 0;
                return;
            }

            string dir = Path.GetDirectoryName(movie.Path) ?? "";
            string[] movieFiles = Directory.GetFiles(dir);
            // If the directory has only one file (the movie itself), there's
            // no companion .srt; nothing to enable.
            if (movieFiles.Length == 1)
            {
                Log.Debug("MovieWindow.GetLanguageInfo: no embedded subs and no sidecar .srt - subtitle UI hidden");
                return;
            }


            srtFileExists = true;
            subTrackComboBox.Items.Add("English");
            subTrackComboBox.Visibility = Visibility.Visible;
            subTrackComboBox.SelectedIndex = 0;

            subTrackComboBox.IsDropDownOpen = true;
            TcpSerialListener.layoutPoint.CaptureComboBoxItems(subTrackComboBox, capturePositions: false);

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
                Log.Information("MovieWindow: subtitle disabled ('Subtitles (none)' selected)");
                SubtitleConfig.Track = Int32.MaxValue;
                SubtitleConfig.HasSrtFile = false;
            }
            else
            {
                if (srtFileExists)
                {
                    Log.Information("MovieWindow: subtitle enabled (sidecar .srt file)");
                    SubtitleConfig.HasSrtFile = true;
                    return;
                }
                SubtitleConfig.Track = subTrackComboBox.SelectedIndex - 1;
                Log.Information("MovieWindow: subtitle enabled (embedded track {Track})", SubtitleConfig.Track);
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
