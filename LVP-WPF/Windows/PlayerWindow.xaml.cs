﻿using CommunityToolkit.Mvvm.ComponentModel;
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace LVP_WPF.Windows
{
    /// <summary>
    /// Interaction logic for PlayerWindow.xaml
    /// </summary>

    [ObservableObject]
    public partial class PlayerWindow : Window
    {
        private static Media currMedia;
        private LibVLC libVLC;
        private MediaPlayer mediaPlayer;
        private DispatcherTimer pollingTimer;
        private bool skipClosing = false;
        private bool sliderMouseDown = false;

        public static void Show(Media m)
        {
            PlayerWindow window = new PlayerWindow();
            currMedia = m;
            window.ShowDialog();
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
            videoView.Loaded += VideoView_Loaded;
        }

        void VideoView_Loaded(object sender, RoutedEventArgs e)
        {
            Core.Initialize();
            libVLC = new LibVLC();
            mediaPlayer = new MediaPlayer(libVLC);
            videoView.MediaPlayer = mediaPlayer;

            mediaPlayer.TimeChanged += MediaPlayer_TimeChanged;
            mediaPlayer.LengthChanged += MediaPlayer_LengthChanged;
            mediaPlayer.EncounteredError += MediaPlayer_EncounteredError;
            mediaPlayer.EndReached += MediaPlayer_EndReached;
            mediaPlayer.EnableMouseInput = false;
            mediaPlayer.EnableKeyInput = false;

            LibVLCSharp.Shared.Media currVLCMedia = CreateMedia(currMedia);
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

            pollingTimer = new DispatcherTimer();
            pollingTimer.Interval = TimeSpan.FromSeconds(3);
            pollingTimer.Tick += PollingTimer_Tick;
            pollingTimer.Start();
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
                    this.Close();
                }

                currMedia = TvShowWindow.cartoonShuffleList[TvShowWindow.cartoonIndex];
                LibVLCSharp.Shared.Media next = CreateMedia(currMedia);
                ThreadPool.QueueUserWorkItem(_ => mediaPlayer.Play(next));
                return;
            }

            if (currMedia as Episode != null)
            {
                Episode episode = (Episode)currMedia;
                episode.SavedTime = episode.Length;
                //To-do: update progress bar
                if (episode.Id == -1)
                {
                    skipClosing = true;
                    this.Close();
                }

                TvShow tvShow = TvShowWindow.CurrentTvShow;
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
                                if (i == tvShow.Seasons.Length - 2 && tvShow.Seasons[tvShow.Seasons.Length - 1].Id == -1 ||
                                    i == tvShow.Seasons.Length - 1)
                                {
                                    skipClosing = true;
                                    this.Close();
                                }
                                else
                                {
                                    // season change
                                    season = tvShow.Seasons[i + 1];
                                    tvShow.CurrSeason = season.Id;
                                    episode = season.Episodes[0];
                                    LibVLCSharp.Shared.Media next = CreateMedia(episode);
                                    ThreadPool.QueueUserWorkItem(_ => mediaPlayer.Play(next));

                                    foreach (Window window in Application.Current.Windows)
                                    {
                                        if (window.Title.Equals("TvShowWindow"))
                                        {
                                            TvShowWindow tvShowWindow = (TvShowWindow)window;
                                            tvShowWindow.Update(tvShow.CurrSeason);
                                            return;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                episode = season.Episodes[j + 1];
                                LibVLCSharp.Shared.Media next = CreateMedia(episode);
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
                this.Close();
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

        private void Window_Closed(object sender, EventArgs e)
        {
            if (pollingTimer != null)
            {
                if (pollingTimer.IsEnabled) pollingTimer.Stop();
                pollingTimer.IsEnabled = false;
                pollingTimer = null;
            }

            if (!skipClosing)
            {
                if (currMedia as Episode != null)
                {
                    Episode episode = (Episode)currMedia;
                    TvShow tvShow = TvShowWindow.CurrentTvShow;
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
                }
            }

            mediaPlayer.Dispose();
            libVLC.Dispose();
        }

        private void PollingTimer_Tick(object? sender, EventArgs e)
        {
            overlayGrid.Visibility = Visibility.Hidden;
            pollingTimer.Stop();
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
            if (mediaPlayer.IsPlaying) mediaPlayer.Stop();
            this.Close();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (mediaPlayer.IsPlaying)
            {
                buttonText.Text = "❚❚";
                buttonText.Margin = new Thickness(1, -3, 0, 0);
                buttonText.FontSize = 28;
                mediaPlayer.Pause();
                pollingTimer.Stop();
            }
            else
            {
                buttonText.Text = "▶️";
                buttonText.Margin = new Thickness(6, -4, 0, 0);
                buttonText.FontSize = 30;
                mediaPlayer.Play();
                pollingTimer.Start();
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
        }

        internal void Pause()
        {

        }

        private void InitializeIdleTimer()
        {
            /*idleTimer = new System.Threading.Timer(mt_ =>
            {
                if (mediaPlayer.IsPlaying)
                {
                    return;
                }
                TimeSpan t = TimeSpan.FromHours(2); //TimeSpan.FromSeconds(10);
                if (MainForm.GetIdleTime() > t.TotalMilliseconds)
                {
                    MainForm.Log("Reached 2 hours of idle player time");
                    this.Invoke(new MethodInvoker(delegate
                    {
                        this.Close();
                    }));
                }
            }, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));  //TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));*/
        }
    }
}