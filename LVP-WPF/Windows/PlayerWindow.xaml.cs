using CommunityToolkit.Mvvm.ComponentModel;
using LibVLCSharp.Shared;
using LVP_WPF.Models;
using LVP_WPF.Services;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace LVP_WPF.Windows
{
    [ObservableObject]
    public partial class PlayerWindow : Window
    {
        static private Media currMedia;
        static private TvShowWindow? tvShowWindow;
        private const string VlcFontStyle = "--freetype-font=Segoe UI";
        private const string VlcFontSize = "--freetype-fontsize=48";
        static internal LibVLC libVLC = new LibVLC(VlcFontStyle, VlcFontSize);
        private MediaPlayer mediaPlayer;
        private DispatcherTimer pollingTimer;
        InactivityTimer inactivityTimer;
        private bool skipClosing = false;
        private bool sliderMouseDown = false;
        private double prevSliderValue;
        // Environment.TickCount of the most recent programmatic seek
        // (SeekRelative / JumpToEdge). Used by Slider_ValueChanged to
        // distinguish "user clicked the slider track" (a real seek intent)
        // from "TimeChanged echoed our own seek back at the binding" (a
        // recursive call that deadlocks LibVLC when playing).
        private int lastProgrammaticSeekTick = 0;
        private System.Windows.Media.SolidColorBrush playHoverBackground = (System.Windows.Media.SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFrom("#FF26A0DA");
        private System.Windows.Media.SolidColorBrush playHoverBorderBrush = (System.Windows.Media.SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFrom("#3c7fb1");

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
            mediaPlayer = new MediaPlayer(libVLC);
            videoView.MediaPlayer = mediaPlayer;
            SliderValue = 0;
            SliderMax = 1;
            prevSliderValue = 0;
#if DEBUG
            this.WindowStyle = WindowStyle.SingleBorderWindow;
            this.AllowsTransparency = false;
#endif

        }

        internal static void InitializeLibVlcCore()
        {
            Core.Initialize();
        }

        private void PlayerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            mediaPlayer.TimeChanged += MediaPlayer_TimeChanged;
            mediaPlayer.LengthChanged += MediaPlayer_LengthChanged;
            mediaPlayer.EncounteredError += MediaPlayer_EncounteredError;
            mediaPlayer.EndReached += MediaPlayer_EndReached;
            mediaPlayer.EnableMouseInput = false;
            mediaPlayer.EnableKeyInput = false;

            pollingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            pollingTimer.Tick += PollingTimer_Tick;
            inactivityTimer = new InactivityTimer(TimeSpan.FromHours(2));
            inactivityTimer.Inactivity += InactivityDetected;

            LibVLCSharp.Shared.Media currVLCMedia = CreateMedia(currMedia);
            Log.Information("Play: {Media}", currMedia.Path);

            bool res = mediaPlayer.Play(currVLCMedia);
            if (!res)
            {
                NotificationDialog.Show("Error", "Media player failed to start.");
            }

            if (currMedia is Episode episode)
            {
                if (PlaybackSession.IsHistoryWatch)
                {
                    ShowHistoryWatchBanner(episode);
                }

                if (episode.SavedTime != 0 && episode.SavedTime < episode.Length)
                {
                    mediaPlayer.SeekTo(TimeSpan.FromMilliseconds(episode.SavedTime));
                }
            }

            MainWindow.gui.playerWindow = this;
            MainWindow.gui.playerCloseButton = this.closeButton;

            // Register the playback row's buttons (left-to-right) so LayoutPoint's
            // joystick / IR remote Left/Right walks through them. The IR remote's
            // dedicated "fastforward"/"rewind"/"forward"/"backward" commands still
            // work in parallel - this just adds a navigable cursor surface.
            TcpSerialListener.layoutPoint.playerControlList.Clear();
            TcpSerialListener.layoutPoint.playerControlList.Add(this.backwardButton);
            TcpSerialListener.layoutPoint.playerControlList.Add(this.rewindButton);
            TcpSerialListener.layoutPoint.playerControlList.Add(this.playButton);
            TcpSerialListener.layoutPoint.playerControlList.Add(this.fastForwardButton);
            TcpSerialListener.layoutPoint.playerControlList.Add(this.forwardButton);

            TcpSerialListener.layoutPoint.Select("PlayerWindow");
            ComInterop.SetCursorPos(CursorConfig.HideCursorX, CursorConfig.HideCursorY);

            // overlayGrid starts Visible (XAML default) so the user gets brief
            // visual confirmation the controls exist. Start the polling timer
            // here so it auto-hides after a few seconds; without this the
            // overlay would stay pinned forever until some other input fired.
            pollingTimer.Start();
        }

        // Used by LayoutPoint and the IR remote dispatch when any input arrives
        // while the player is open. Forces the timeline / button overlay back
        // on so the user gets visual feedback. The auto-hide timer only rearms
        // when actually playing - when paused we want the controls to stay
        // pinned indefinitely (same convention as TogglePlayPause's pause path).
        //
        // The Start() is scheduled at Background priority because callers
        // often warp the cursor onto a button right after (FocusPlayerControl /
        // MovePlayerPoint -> CenterMouseOverControl), which fires the OS
        // MouseEnter event synchronously - and Control_MouseEnter stops the
        // polling timer. Without the deferred Start, the auto-hide we just
        // armed would be cancelled the moment the cursor lands on its target,
        // leaving the overlay pinned indefinitely.
        internal void WakeOverlay()
        {
            Dispatcher.Invoke(() => overlayGrid.Visibility = Visibility.Visible);
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
            {
                if (pollingTimer == null) return;
                pollingTimer.Stop();
                if (mediaPlayer != null && mediaPlayer.IsPlaying)
                {
                    pollingTimer.Start();
                }
            });
        }

        private void PlayerWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            timelineSlider.ValueChanged -= Slider_ValueChanged;
            // DispatcherTimer.Stop() is a no-op when already stopped, and
            // setting IsEnabled=false after Stop() is redundant - Stop does both.
            pollingTimer?.Stop();
            pollingTimer = null;

            if (PlaybackSession.IsHistoryWatch)
            {
                if (MainWindow.model.HistoryIndex == MainWindow.model.HistoryList.Count)
                {
                    MainWindow.model.HistoryIndex = 0;
                    MainWindow.model.HistoryEpisode = null;
                }
            }
            else if (!PlaybackSession.IsCartoonShuffle && !skipClosing)
            {
                if (currMedia is Episode episode)
                {
                    TvShow tvShow = TvShowWindow.tvShow;
                    int? seasonId = tvShow.FindSeasonIdOf(episode);

                    long endTime = mediaPlayer.Time;
                    if (endTime > episode.Length)
                    {
                        endTime = episode.Length;
                    }

                    if (endTime > 0 && seasonId.HasValue)
                    {
                        episode.SavedTime = endTime;
                        if (seasonId.Value != -1)  // -1 means the Extras pseudo-season; don't promote that to LastEpisode
                        {
                            tvShow.CurrSeason = seasonId.Value;
                            tvShow.LastEpisode = episode;
                        }
                    }
                    UpdateProgressBar(episode);
                }
            }

            if (mediaPlayer.IsPlaying)
            {
                mediaPlayer.Stop();
            }
            mediaPlayer.Dispose();
            inactivityTimer.Dispose();
        }

        private static void UpdateProgressBar(Episode episode)
        {
            tvShowWindow.Dispatcher.BeginInvoke(() =>
            {
                foreach (EpisodeWindowBox epBox in tvShowWindow.EpisodeListView.Items)
                {
                    if (epBox.Id != episode.Id) continue;
                    epBox.Progress = (int)episode.SavedTime;
                    epBox.Total = (int)episode.Length;
                    break;
                }
            });
        }

        /// <summary>
        /// Show the "what's playing next during history watch" overlay text
        /// for 5 seconds, then fade it out. Used both at initial playback start
        /// and when MediaPlayer_EndReached advances to the next history entry.
        /// </summary>
        private void ShowHistoryWatchBanner(Episode episode)
        {
            hwGrid.Dispatcher.BeginInvoke(() =>
            {
                hwTxtBlock.Text = $"{episode.Date:MMMM dd, yyyy}\n{episode.Name}";
                hwGrid.Visibility = Visibility.Visible;
            });
            Task.Delay(5000).ContinueWith(t =>
            {
                hwGrid.Dispatcher.BeginInvoke(() => { hwGrid.Visibility = Visibility.Hidden; });
            });
        }

        private void MediaPlayer_EndReached(object? sender, EventArgs e)
        {
            if (PlaybackSession.IsHistoryWatch)
            {
                MainWindow.model.HistoryIndex++;
                if (MainWindow.model.HistoryIndex == MainWindow.model.HistoryList.Count)
                {
                    TcpSerialListener.layoutPoint.CloseCurrWindow();
                }
                MainWindow.model.HistoryEpisode = MainWindow.model.HistoryList[MainWindow.model.HistoryIndex];
                PlayMediaOnVlcThread(MainWindow.model.HistoryEpisode);
                ShowHistoryWatchBanner(MainWindow.model.HistoryEpisode);
                return;
            }

            if (PlaybackSession.IsCartoonShuffle)
            {
                PlaybackSession.CartoonShuffleIndex++;
                if (PlaybackSession.CartoonShuffleIndex == PlaybackSession.CartoonShuffleLimit)
                {
                    skipClosing = true;
                    TcpSerialListener.layoutPoint.CloseCurrWindow();
                }

                PlayMediaOnVlcThread(PlaybackSession.CartoonShuffleQueue[PlaybackSession.CartoonShuffleIndex]);
                return;
            }

            if (currMedia is Episode episode)
            {
                if (episode.Id < 0)
                {
                    skipClosing = true;
                    TcpSerialListener.layoutPoint.CloseCurrWindow();
                    return;
                }

                episode.SavedTime = episode.Length;
                UpdateProgressBar(episode);

                TvShow tvShow = TvShowWindow.tvShow;
                Episode? nextEpisode = tvShow.GetNextEpisode(episode, out bool seasonChanged);
                if (nextEpisode == null)
                {
                    // End of show (current was last episode of last non-Extras season).
                    skipClosing = true;
                    TcpSerialListener.layoutPoint.CloseCurrWindow();
                    return;
                }

                if (seasonChanged)
                {
                    int newSeasonId = tvShow.FindSeasonIdOf(nextEpisode) ?? tvShow.CurrSeason;
                    Log.Information("{TvShowName} season change to {NewSeason}", tvShow.Name, newSeasonId);
                    tvShow.CurrSeason = newSeasonId;
                    tvShowWindow.Dispatcher.BeginInvoke(() =>
                    {
                        tvShowWindow.UpdateTvWindowSeasonChange(tvShow.CurrSeason);
                    });
                }

                PlayMediaOnVlcThread(nextEpisode);
            }
            else //if Movie
            {
                skipClosing = true;
                TcpSerialListener.layoutPoint.CloseCurrWindow();
            }
        }

        /// <summary>
        /// Hand off the next media item to LibVLC on the threadpool. The pool
        /// hop is required because Play() blocks for a beat while VLC builds
        /// the demuxer chain, and we don't want to stall the UI thread during
        /// auto-advance between episodes.
        /// </summary>
        private void PlayMediaOnVlcThread(Media m)
        {
            currMedia = m;
            LibVLCSharp.Shared.Media next = CreateMedia(m);
            Log.Information("Play: {Media}", m.Path);
            ThreadPool.QueueUserWorkItem(_ => mediaPlayer.Play(next));
        }

        private void MediaPlayer_EncounteredError(object? sender, EventArgs e)
        {
            Log.Error("VLC ERROR: {Error}", e.ToString());
        }

        private void MediaPlayer_LengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e)
        {
            SliderMax = mediaPlayer.Length;
            if (currMedia is Episode episode)
            {
                episode.Length = mediaPlayer.Length;
            }
        }

        private void MediaPlayer_TimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
        {
            if (!sliderMouseDown)
            {
                SliderValue = mediaPlayer.Time;
            }
            else
            {
                sliderMouseDown = false;
            }
        }

        private LibVLCSharp.Shared.Media CreateMedia(Media m)
        {
            // Add application and vlc .exe to Graphics Settings with High Performance NVIDIA GPU preference
            LibVLCSharp.Shared.Media media = new LibVLCSharp.Shared.Media(libVLC, m.Path, FromType.FromPath);
            media.AddOption(":avcodec-hw=auto");
            media.AddOption(":no-mkv-preload-local-dir");

            bool useSrtFile = SubtitleConfig.HasSrtFile && SubtitleConfig.EnableSubtitles;
            if (useSrtFile)
            {
                string dir = System.IO.Path.GetDirectoryName(m.Path) ?? "";
                string name = System.IO.Path.GetFileNameWithoutExtension(m.Path);
                string srtPath = System.IO.Path.Combine(dir, $"{name}.srt");
                mediaPlayer.AddSlave(MediaSlaveType.Subtitle, $"file:///{srtPath}", true);
            }
            else
            {
                media.AddOption($":sub-track={SubtitleConfig.Track}");
            }
            return media;
        }

        private void Control_MouseEnter(object sender, EventArgs e)
        {
            pollingTimer?.Stop();
        }

        private void Control_MouseLeave(object sender, EventArgs e)
        {
            pollingTimer?.Start();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            closeButton.MouseLeave -= Control_MouseLeave;
            this.Close();
            TcpSerialListener.layoutPoint.NotifyWindowClosedFromUI();
        }

        // Mouse-clickable mirrors of the IR remote's "backward" / "rewind" /
        // "fastforward" / "forward" commands. Same underlying SeekRelative /
        // JumpToEdge methods; the IR-remote dispatch in IrSerialReader and
        // these handlers route through the same place.
        private void BackwardButton_Click(object sender, RoutedEventArgs e) => JumpToEdge(toStart: true);
        private void RewindButton_Click(object sender, RoutedEventArgs e) => SeekRelative(rewind: true);
        private void FastForwardButton_Click(object sender, RoutedEventArgs e) => SeekRelative(rewind: false);
        private void ForwardButton_Click(object sender, RoutedEventArgs e) => JumpToEdge(toStart: false);

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (mediaPlayer.IsPlaying)
            {
                ApplyPausedVisuals();
                mediaPlayer.Pause();
                pollingTimer.Stop();
            }
            else
            {
                ApplyPlayingVisuals();
                mediaPlayer.Play();
                pollingTimer.Start();
            }
        }

        // Paint the play button + glyph for the "currently paused" state. The
        // hover-colored background reads as "this is the button you'll click
        // to resume" and the ❚❚ glyph confirms it.
        private void ApplyPausedVisuals()
        {
            playButton.Background = playHoverBackground;
            playButton.BorderBrush = playHoverBorderBrush;
            buttonText.Text = "❚❚";
            buttonText.Margin = new Thickness(1, -3, 0, 0);
            buttonText.FontSize = 28;
        }

        // Paint the play button + glyph for the "currently playing" state -
        // transparent background fades the control out of the way during
        // playback; the ▶️ glyph confirms it.
        private void ApplyPlayingVisuals()
        {
            playButton.Background = System.Windows.Media.Brushes.Transparent;
            playButton.BorderBrush = System.Windows.Media.Brushes.White;
            buttonText.Text = "▶️";
            buttonText.Margin = new Thickness(6, -4, 0, 0);
            buttonText.FontSize = 30;
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
                        TimeLabel = $"{currTime:hh\\:mm\\:ss}/{lengthTime:hh\\:mm\\:ss}";
                    }
                    else
                    {
                        TimeLabel = $"{currTime:mm\\:ss}/{lengthTime:mm\\:ss}";
                    }

                    if (Math.Abs(SliderValue - prevSliderValue) > 3000 && prevSliderValue != 0)
                    {
                        // Distinguish "user clicked the slider track" (real seek
                        // intent, big delta) from "TimeChanged echoed our own
                        // SeekRelative/JumpToEdge back at the binding" (same big
                        // delta, but recursive). The recursive case calls SeekTo
                        // again on the UI thread while LibVLC is still processing
                        // the first seek - deadlocks when playing.
                        int sinceProgrammatic = Environment.TickCount - lastProgrammaticSeekTick;
                        if (sinceProgrammatic >= 0 && sinceProgrammatic < 2000)
                        {
                            // Echo: just refresh prev and return.
                            prevSliderValue = SliderValue;
                            return;
                        }
                        sliderMouseDown = true;
                        TimeSpan seekTime = TimeSpan.FromMilliseconds(SliderValue);
                        mediaPlayer.SeekTo(seekTime);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Slider_ValueChanged: {Value}", ex.Message);
                }
                prevSliderValue = SliderValue;
            }
        }

        internal void TogglePlayPause()
        {
            if (mediaPlayer == null) return;

            // The previous version ended each branch with DoMouseClick() +
            // SetCursorPos(). Those were defensive cursor-parking calls from
            // before joystick nav existed. They now actively misfire: after
            // the IR-remote dispatch's FocusPlayerControl warps the cursor
            // onto the play button, DoMouseClick fires PlayButton_Click as
            // a SECOND click on the same press - toggling state back and
            // leaving the user stuck. Cursor positioning is now owned by
            // LayoutPoint.FocusPlayerControl; both side effects are gone.
            if (mediaPlayer.IsPlaying)
            {
                playButton.Dispatcher.Invoke(() =>
                {
                    ApplyPausedVisuals();
                    overlayGrid.Visibility = Visibility.Visible;
                });
                mediaPlayer.Pause();
                pollingTimer.Stop();
            }
            else
            {
                playButton.Dispatcher.Invoke(() =>
                {
                    ApplyPlayingVisuals();
                    overlayGrid.Visibility = Visibility.Hidden;
                });
                mediaPlayer.Play();
                pollingTimer.Start();
            }
        }

        internal void JumpToEdge(bool toStart)
        {
            if (mediaPlayer == null) return;
            lastProgrammaticSeekTick = Environment.TickCount;
            long target = toStart ? 0 : mediaPlayer.Length - 1;
            mediaPlayer.SeekTo(TimeSpan.FromMilliseconds(target));
        }

        internal void SeekRelative(bool rewind)
        {
            if (mediaPlayer == null) return;
            lastProgrammaticSeekTick = Environment.TickCount;

            const int seekStepMs = 30 * 1000;
            long current = mediaPlayer.Time;
            long length = mediaPlayer.Length;
            long target = rewind ? current - seekStepMs : current + seekStepMs;
            target = Math.Clamp(target, 0, length);
            mediaPlayer.SeekTo(TimeSpan.FromMilliseconds(target));
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

        private async void InactivityDetected(object sender, EventArgs e)
        {
            if (mediaPlayer.IsPlaying) return;

            // Double check after 10s in case the player is mid-transition
            // to the next episode/cartoon and wasn't strictly playing for a moment.
            await Task.Delay(10000);
            if (mediaPlayer.IsPlaying) return;

            this.Dispatcher.Invoke(() => { this.Close(); });
            foreach (Window w in Application.Current.Windows)
            {
                if (w is TvShowWindow) w.Close();
            }

            await Task.Delay(1000);
            Log.Information("Inactivity shutdown player");
            Application.Current.Shutdown();
        }
    }
}
