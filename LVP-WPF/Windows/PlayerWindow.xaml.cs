using CommunityToolkit.Mvvm.ComponentModel;
using LibVLCSharp.Shared;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace LVP_WPF.Windows
{
    /// <summary>
    /// Interaction logic for PlayerWindow.xaml
    /// </summary>

    [ObservableObject]
    public partial class PlayerWindow : Window
    {
        static private Media currMedia;
        static private TvShowWindow? tvShowWindow;
        private LibVLC libVLC;
        private MediaPlayer mediaPlayer;
        private DispatcherTimer pollingTimer;
        InactivityTimer inactivityTimer;
        private bool skipClosing = false;
        private bool sliderMouseDown = false;

        public static void Show(Media m, TvShowWindow? tw = null)
        {
            PlayerWindow window = new PlayerWindow();
            currMedia = m;
            tvShowWindow = tw;
            MainWindow.gui.isPlaying = true;
            window.ShowDialog();
            MainWindow.gui.isPlaying = false;
        }

        [ObservableProperty]
        private string timeLabel;
        [ObservableProperty]
        private double sliderMax;
        [ObservableProperty]
        private double sliderValue;

        public PlayerWindow()
        {
            DataContext = this;
            InitializeComponent();
            Core.Initialize();
            libVLC = new LibVLC();
            mediaPlayer = new MediaPlayer(libVLC);
            videoView.MediaPlayer = mediaPlayer;
            SliderValue = 0;
            SliderMax = 1;
        }

        private void PlayerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            mediaPlayer.TimeChanged += MediaPlayer_TimeChanged;
            mediaPlayer.LengthChanged += MediaPlayer_LengthChanged;
            mediaPlayer.EncounteredError += MediaPlayer_EncounteredError;
            mediaPlayer.EndReached += MediaPlayer_EndReached;
            mediaPlayer.EnableMouseInput = false;
            mediaPlayer.EnableKeyInput = false;

            pollingTimer = new DispatcherTimer();
            pollingTimer.Interval = TimeSpan.FromSeconds(3);
            pollingTimer.Tick += PollingTimer_Tick;
            inactivityTimer = new InactivityTimer(TimeSpan.FromHours(2)); //(TimeSpan.FromSeconds(10));
            inactivityTimer.Inactivity += InactivityDetected;

            LibVLCSharp.Shared.Media currVLCMedia = CreateMedia(currMedia);
            GuiModel.Log("Play: " + currMedia.Path);
            bool res = mediaPlayer.Play(currVLCMedia);
            if (!res) NotificationDialog.Show("Error", "Media player failed to start.");

            if (currMedia as Episode != null)
            {
                Episode episode = (Episode)currMedia;
                if (episode.SavedTime != 0)
                {
                    mediaPlayer.SeekTo(TimeSpan.FromMilliseconds(episode.SavedTime));
                }
            }

            MainWindow.gui.playerWindow = this;
            MainWindow.gui.playerCloseButton = this.closeButton;
            TcpSerialListener.SetCursorPos(500, (int)(this.Height * 4));
            MainWindow.tcpWorker.layoutPoint.Select("PlayerWindow");
        }

        private void PlayerWindow_Closed(object sender, EventArgs e)
        {
            timelineSlider.ValueChanged -= Slider_ValueChanged;
            if (pollingTimer != null)
            {
                if (pollingTimer.IsEnabled) pollingTimer.Stop();
                pollingTimer.IsEnabled = false;
                pollingTimer = null;
            }
            inactivityTimer.Dispose();

            if (!TvShowWindow.cartoonShuffle && !skipClosing)
            {
                if (currMedia as Episode != null)
                {
                    Episode episode = (Episode)currMedia;
                    TvShow tvShow = TvShowWindow.tvShow;
                    int seasonIndex = 0;
                    bool found = false;
                    for (int i = 0; i < tvShow.Seasons.Length; i++)
                    {
                        Season season = tvShow.Seasons[i];
                        for (int j = 0; j < season.Episodes.Length; j++)
                        {
                            if (episode.Name.Equals(season.Episodes[j].Name))
                            {
                                seasonIndex = season.Id;
                                found = true;
                                break;
                            }
                        }
                        if (found) break;
                    }

                    long endTime = mediaPlayer.Time;
                    if (endTime > episode.Length) endTime = episode.Length;
                    if (endTime > 0)
                    {
                        if (seasonIndex == -1)
                        {
                            episode.SavedTime = endTime;
                        }
                        else
                        {
                            episode.SavedTime = endTime;
                            tvShow.CurrSeason = seasonIndex;
                            tvShow.LastEpisode = episode;
                        }
                    }
                    UpdateProgressBar(episode);
                }
            }
            if (mediaPlayer.IsPlaying) mediaPlayer.Stop();
            mediaPlayer.Dispose();
            libVLC.Dispose();
        }
       
        private void UpdateProgressBar(Episode episode)
        {
            tvShowWindow.Dispatcher.Invoke(() =>
            {
                for (int i = 0; i < tvShowWindow.EpisodeListView.Items.Count; i++)
                {
                    EpisodeWindowBox epBox = (EpisodeWindowBox)tvShowWindow.EpisodeListView.Items[i];
                    if (epBox.Id == episode.Id)
                    {
                        epBox.Progress = (int)episode.SavedTime;
                        epBox.Total = (int)episode.Length;
                        break;
                    }
                }
            });
        }

        private void MediaPlayer_EndReached(object? sender, EventArgs e)
        {
            SliderValue = 0;
            if (TvShowWindow.cartoonShuffle)
            {
                TvShowWindow.cartoonIndex++;
                if (TvShowWindow.cartoonIndex == TvShowWindow.cartoonLimit)
                {
                    skipClosing = true;
                    MainWindow.tcpWorker.layoutPoint.CloseCurrWindow();
                }

                currMedia = TvShowWindow.cartoonShuffleList[TvShowWindow.cartoonIndex];
                LibVLCSharp.Shared.Media next = CreateMedia(currMedia);
                GuiModel.Log("Playing " + currMedia.Path);
                ThreadPool.QueueUserWorkItem(_ => mediaPlayer.Play(next));
                return;
            }

            if (currMedia as Episode != null)
            {
                Episode episode = (Episode)currMedia;
                episode.SavedTime = episode.Length;
                UpdateProgressBar(episode);

                if (episode.Id == -1)
                {
                    skipClosing = true;
                    MainWindow.tcpWorker.layoutPoint.CloseCurrWindow();
                }

                TvShow tvShow = TvShowWindow.tvShow;
                for (int i = 0; i < tvShow.Seasons.Length; i++)
                {
                    Season season = tvShow.Seasons[i];
                    for (int j = 0; j < season.Episodes.Length; j++)
                    {
                        if (episode.Name.Equals(season.Episodes[j].Name))
                        {
                            if (j == season.Episodes.Length - 1)
                            {
                                // if last season (check for extras)
                                if (i == tvShow.Seasons.Length - 2 && tvShow.Seasons[tvShow.Seasons.Length - 1].Id == -1 || i == tvShow.Seasons.Length - 1)
                                {
                                    skipClosing = true;
                                    MainWindow.tcpWorker.layoutPoint.CloseCurrWindow();
                                }
                                else
                                {
                                    GuiModel.Log(tvShow.Name + " season change from " + (i) + " to " + (i + 1));
                                    season = tvShow.Seasons[i + 1];
                                    tvShow.CurrSeason = season.Id;
                                    currMedia = season.Episodes[0];
                                    LibVLCSharp.Shared.Media next = CreateMedia(currMedia);
                                    GuiModel.Log("Play: " + currMedia.Path);
                                    ThreadPool.QueueUserWorkItem(_ => mediaPlayer.Play(next));
                                    tvShowWindow.Dispatcher.Invoke(() => { tvShowWindow.Update(tvShow.CurrSeason); });
                                    return;
                                }
                            }
                            else
                            {
                                currMedia = season.Episodes[j + 1];
                                LibVLCSharp.Shared.Media next = CreateMedia(currMedia);
                                GuiModel.Log("Play: " + currMedia.Path);
                                ThreadPool.QueueUserWorkItem(_ => mediaPlayer.Play(next));
                                return;
                            }
                        }
                    }
                }

            }
            else //if Movie
            {
                skipClosing = true;
                MainWindow.tcpWorker.layoutPoint.CloseCurrWindow();
            }
        }

        private void MediaPlayer_EncounteredError(object? sender, EventArgs e)
        {
            //To-do: logging
            throw new NotImplementedException();
        }

        private void MediaPlayer_LengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e)
        {
            SliderMax = mediaPlayer.Length;
            if (currMedia as Episode != null)
            {
                Episode episode = (Episode)currMedia;
                episode.Length = mediaPlayer.Length;
            }
        }

        private void MediaPlayer_TimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
        {
            SliderValue = mediaPlayer.Time;
        }

        private LibVLCSharp.Shared.Media CreateMedia(Media m)
        {
            //To-do: Add application and vlc .exe to Graphics Settings with High Performance NVIDIA GPU preference
            LibVLCSharp.Shared.Media media = new LibVLCSharp.Shared.Media(libVLC, m.Path, FromType.FromPath);
            media.AddOption(":avcodec-hw=auto");
            media.AddOption(":no-mkv-preload-local-dir");
            string subtitleTrackOption = String.Format(":sub-track-id={0}", Int32.MaxValue);
            if (m as Movie != null)
            {
                Movie movie = (Movie)m;
                if (movie.Subtitles)
                {
                    subtitleTrackOption = String.Format(":sub-track-id={0}", movie.SubtitleTrack);
                }
            }
            media.AddOption(subtitleTrackOption);
            return media;
        }

        private void Control_MouseEnter(object sender, EventArgs e)
        {
            pollingTimer.Stop();
        }

        private void Control_MouseLeave(object sender, EventArgs e)
        {
            pollingTimer.Start();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            closeButton.MouseLeave -= Control_MouseLeave;
            this.Close();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (mediaPlayer.IsPlaying)
            {
                PlayButton_SetSymbol(0);
                mediaPlayer.Pause();
                pollingTimer.Stop();
            }
            else
            {
                PlayButton_SetSymbol(1);
                mediaPlayer.Play();
                pollingTimer.Start();
            }
        }

        private void PlayButton_SetSymbol(int symbol)
        {
            switch (symbol)
            {
                case 0:
                    buttonText.Text = "❚❚";
                    buttonText.Margin = new Thickness(1, -3, 0, 0);
                    buttonText.FontSize = 28;
                    break;
                case 1:
                    buttonText.Text = "▶️";
                    buttonText.Margin = new Thickness(6, -4, 0, 0);
                    buttonText.FontSize = 30;
                    break;
            }
        }

        private void Slider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            sliderMouseDown = false;
            TimeSpan ts = TimeSpan.FromMilliseconds(SliderValue);
            mediaPlayer.SeekTo(ts);
        }

        private void Slider_MouseDown(object sender, MouseButtonEventArgs e)
        {
            sliderMouseDown = true;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (mediaPlayer != null)
            {
                try
                {
                    TimeSpan lengthTime = TimeSpan.FromMilliseconds(mediaPlayer.Length);
                    TimeSpan currTime = TimeSpan.FromMilliseconds(mediaPlayer.Time);

                    if (lengthTime.TotalMilliseconds > 3600000) // 1 hour
                    {
                        TimeLabel = currTime.ToString(@"hh\:mm\:ss") + "/" + lengthTime.ToString(@"hh\:mm\:ss");
                    }
                    else
                    {
                        TimeLabel = currTime.ToString(@"mm\:ss") + "/" + lengthTime.ToString(@"mm\:ss");
                    }

                    if (sliderMouseDown)
                    {
                        TimeSpan seekTime = TimeSpan.FromMilliseconds(SliderValue);
                        if (seekTime.TotalMilliseconds > lengthTime.TotalMilliseconds) SliderValue = lengthTime.TotalMilliseconds;
                        mediaPlayer.SeekTo(seekTime);
                    }
                }
                catch (Exception ex)
                {
                    GuiModel.Log("Slider_ValueChanged: " + ex.Message);
                }
            }
        }

        internal void PlayPause_TcpSerialListener()
        {
            if (mediaPlayer != null)
            {
                if (mediaPlayer.IsPlaying)
                {
                    overlayGrid.Dispatcher.Invoke(() => { overlayGrid.Visibility = Visibility.Visible; });
                    buttonText.Dispatcher.Invoke(() => { PlayButton_SetSymbol(0); });
                    mediaPlayer.Pause();
                }
                else
                {
                    overlayGrid.Dispatcher.Invoke(() => { overlayGrid.Visibility = Visibility.Hidden; });
                    buttonText.Dispatcher.Invoke(() => { PlayButton_SetSymbol(1); });
                    mediaPlayer.Play();
                }
            }
        }

        internal void Stop_TcpSerialListener()
        {
            if (mediaPlayer != null)
            {
                this.Dispatcher.Invoke(() => { this.Close(); });
            }
        }

        internal void Seek_TcpSerialListener(bool rewind)
        {
            if (mediaPlayer != null)
            {
                TimeSpan lengthTime = TimeSpan.FromMilliseconds(mediaPlayer.Length);
                TimeSpan currTime = TimeSpan.FromMilliseconds(mediaPlayer.Time);
                long thirtyMs = 300000;

                if (rewind)
                {
                    if (currTime.TotalMilliseconds < thirtyMs)
                    {
                        mediaPlayer.SeekTo(TimeSpan.FromMilliseconds(0));
                    }
                    else
                    {
                        mediaPlayer.SeekTo(TimeSpan.FromMilliseconds(mediaPlayer.Time + thirtyMs));
                    }
                }
                else
                {
                    if (currTime.TotalMilliseconds + thirtyMs > lengthTime.TotalMilliseconds)
                    {
                        mediaPlayer.SeekTo(TimeSpan.FromMilliseconds(lengthTime.TotalMilliseconds));
                    }
                    else
                    {
                        mediaPlayer.SeekTo(TimeSpan.FromMilliseconds(mediaPlayer.Time - thirtyMs));
                    }
                }
            }
        }

        private void VideoView_MouseMove(object sender, MouseEventArgs e)
        {
            Point p = Mouse.GetPosition(this);
            if (p.Y > this.Height - 100 || p.Y < 100)
            {
                if (!pollingTimer.IsEnabled)
                {
                    pollingTimer.IsEnabled = true;
                    pollingTimer.Start();
                }
                overlayGrid.Visibility = Visibility.Visible;
            }
        }

        private void PollingTimer_Tick(object? sender, EventArgs e)
        {
            overlayGrid.Visibility = Visibility.Hidden;
            pollingTimer.Stop();
        }

        private void InactivityDetected(object sender, EventArgs e)
        {
            if (mediaPlayer.IsPlaying) return;
            this.Close();
        }
    }
}
