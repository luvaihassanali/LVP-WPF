using CommunityToolkit.Mvvm.ComponentModel;
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace LVP_WPF.Windows
{
    /// <summary>
    /// Interaction logic for MovieWindow.xaml
    /// </summary>

    [ObservableObject]
    public partial class MovieWindow : Window
    {
        private static Movie movie;

        public static async void Show(Movie m)
        {
            PlayerWindow.subtitleTrack = Int32.MaxValue;
            movie = m;
            MovieWindow window = new MovieWindow();
            window.MovieName = movie.Name + " (" + movie.Date.GetValueOrDefault().Year + ")";
            TimeSpan temp = TimeSpan.FromMinutes(movie.RunningTime);
            string hour = temp.Hours > 1 ? "hours " : "hour ";
            window.RunningTime = "Running time: " + temp.Hours + " " + hour + temp.Minutes + " minutes";
            window.Description = movie.Overview; //.Length > 1011 ? movie.Overview.Substring(0, 1011) + "..." : movie.Overview;
            string img = movie.Backdrop == null ? "Resources\\noPrevWide.png" : movie.Backdrop;
            window.Backdrop = Cache.LoadImage(img, 960);
            window.Overlay = Cache.LoadImage("Resources\\play.png", 960);
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

        public MovieWindow()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void GetLanguageInfo(Movie movie)
        {
            LibVLCSharp.Shared.Media media = new LibVLCSharp.Shared.Media(PlayerWindow.libVLC, movie.Path, FromType.FromPath);
            Task.Run(async () => { await media.Parse(MediaParseOptions.ParseLocal); }).Wait();

            subTrackComboBox.Items.Add("Subtitles (none)");
            foreach (var track in media.Tracks)
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
            }
            //To-do: check for srt
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
        }

        private void MovieWindow_Loaded(object sender, RoutedEventArgs e)
        {
            GetLanguageInfo(movie);
            MainWindow.gui.tvMovieCloseButton = this.closeButton;
            MainWindow.tcpWorker.layoutPoint.movieBackdrop = this.movieBackdrop;
            MainWindow.tcpWorker.layoutPoint.Select("MovieWindow", true);
        }

        private void subTrackComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (subTrackComboBox.SelectedIndex == 0)
            {
                PlayerWindow.subtitleTrack = Int32.MaxValue;
            } 
            else
            {
                PlayerWindow.subtitleTrack = subTrackComboBox.SelectedIndex - 1;
            }
        }
    }
}
