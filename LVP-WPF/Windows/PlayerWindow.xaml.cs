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

        // Tracks user INTENT, not LibVLC's instantaneous state. The player
        // starts up calling Play(), so initial intent is "playing" -> false.
        // Flipped only by TogglePlayPause (the single user-initiated path
        // for pause/resume). PollingTimer_Tick uses this instead of
        // mediaPlayer.IsPlaying because IsPlaying reports false transiently
        // after a seek (LibVLC enters Buffering/Opening state for up to a
        // few seconds depending on file size and disk speed), which would
        // otherwise make Tick keep the overlay visible forever after every
        // F / R / End / Home press.
        private bool _userPaused = false;
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
            Log.Information("Play: {Media} ({Duration})", currMedia.Path, FormatMediaDuration(currMedia));

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

        // Shows the overlay and arms the 3-second auto-hide timer. Called
        // by LayoutPoint and the IR / keyboard transport commands so any
        // user input refreshes the controls. Whether the Tick actually
        // hides (vs. keeps the overlay pinned for the paused-state UX)
        // is decided inside PollingTimer_Tick via the _userPaused flag.
        internal void WakeOverlay()
        {
            Dispatcher.Invoke(() =>
            {
                if (pollingTimer == null) return;
                overlayGrid.Visibility = Visibility.Visible;
                pollingTimer.Stop();
                pollingTimer.Start();
                Log.Debug("WakeOverlay: overlay shown, auto-hide timer armed for 3s");
            });
        }

        private void PlayerWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Log.Information("PlayerWindow.Closing: isHistory={History}, isCartoonShuffle={Shuffle}, skipClosing={Skip}, currTime={Time}ms",
                PlaybackSession.IsHistoryWatch, PlaybackSession.IsCartoonShuffle, skipClosing,
                mediaPlayer?.Time ?? -1);
            timelineSlider.ValueChanged -= Slider_ValueChanged;
            // DispatcherTimer.Stop() is a no-op when already stopped, and
            // setting IsEnabled=false after Stop() is redundant - Stop does both.
            pollingTimer?.Stop();
            pollingTimer = null;

            if (PlaybackSession.IsHistoryWatch)
            {
                if (MainWindow.model.HistoryIndex == MainWindow.model.HistoryList.Count)
                {
                    Log.Information("PlayerWindow.Closing: history watch reached end, resetting HistoryIndex");
                    MainWindow.model.HistoryIndex = 0;
                    MainWindow.model.HistoryEpisode = null;
                }
            }
            else if (!PlaybackSession.IsCartoonShuffle && !skipClosing)
            {
                if (currMedia is Episode episode)
                {
                    // Defensive null guard: TvShowWindow.tvShow is null when
                    // the player was NOT opened through the TV-show flow
                    // (e.g., cartoon shuffle picks Episodes but never opens
                    // TvShowWindow). If the PlaybackSession mode gets
                    // cleared before Closing fires - e.g., during a broken
                    // close-ordering refactor - this branch is entered for
                    // a cartoon Episode and would NRE on FindSeasonIdOf,
                    // aborting the handler BEFORE mediaPlayer.Dispose()
                    // runs. Result: audio keeps playing after the window
                    // is gone, only manual kill fixes it. Bail out cleanly
                    // instead so the dispose path below always runs.
                    TvShow tvShow = TvShowWindow.tvShow;
                    if (tvShow == null)
                    {
                        Log.Warning("PlayerWindow.Closing: TvShowWindow.tvShow is null for episode '{Ep}' - skipping progress save",
                            episode.Name);
                    }
                    else
                    {
                        int? seasonId = tvShow.FindSeasonIdOf(episode);

                        long endTime = mediaPlayer.Time;
                        if (endTime > episode.Length)
                        {
                            endTime = episode.Length;
                        }

                        if (endTime > 0 && seasonId.HasValue)
                        {
                            Log.Information("PlayerWindow.Closing: saving progress for '{Show}' S{Sn}E{Ep} '{Title}' = {Time}ms / {Length}ms",
                                tvShow.Name, seasonId.Value, episode.Id, episode.Name, endTime, episode.Length);
                            episode.SavedTime = endTime;
                            if (seasonId.Value != -1)  // -1 means the Extras pseudo-season; don't promote that to LastEpisode
                            {
                                tvShow.CurrSeason = seasonId.Value;
                                tvShow.LastEpisode = episode;
                            }
                        }
                        else
                        {
                            Log.Debug("PlayerWindow.Closing: not saving progress (endTime={Time}, seasonId={SeasonId})",
                                endTime, seasonId);
                        }
                        UpdateProgressBar(episode);
                    }
                }
            }

            if (mediaPlayer.IsPlaying)
            {
                mediaPlayer.Stop();
            }
            mediaPlayer.Dispose();
            inactivityTimer.Dispose();
            Log.Information("PlayerWindow.Closing: complete (mediaPlayer disposed, inactivityTimer disposed)");
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

        // CloseCurrWindow can NOT be called directly from this event handler -
        // EndReached fires on a LibVLC worker thread, and the close path
        // ultimately calls mediaPlayer.Dispose(), which BLOCKS waiting for
        // LibVLC's worker callbacks (including the one currently firing this
        // event) to return. Calling synchronously from here produces a
        // deadlock cycle:
        //   LibVLC worker (in EndReached)
        //     -> CloseCurrWindow -> EndFeature -> featureDispatcher.Invoke(Close)
        //       -> Closing handler -> mediaPlayer.Dispose()
        //         -> waits for LibVLC workers to finish their callbacks
        //           -> the EndReached worker is blocked in the Invoke above
        // The symptom is the exact one the user reported: audio keeps playing
        // after the visual window goes away, IR locks up, manual kill needed.
        // Marshalling the close to the main UI dispatcher breaks the cycle:
        // EndReached returns immediately, the LibVLC worker resumes, and the
        // close then runs on a thread that isn't on the LibVLC callback stack.
        private static void DeferCloseCurrWindow()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                TcpSerialListener.layoutPoint.CloseCurrWindow();
            }));
        }

        private void MediaPlayer_EndReached(object? sender, EventArgs e)
        {
            Log.Information("MediaPlayer_EndReached: '{Media}' (isHistory={History}, isCartoonShuffle={Shuffle})",
                (currMedia as Episode)?.Name ?? (currMedia as Movie)?.Name ?? "<unknown>",
                PlaybackSession.IsHistoryWatch, PlaybackSession.IsCartoonShuffle);

            if (PlaybackSession.IsHistoryWatch)
            {
                MainWindow.model.HistoryIndex++;
                if (MainWindow.model.HistoryIndex == MainWindow.model.HistoryList.Count)
                {
                    Log.Information("HistoryWatch: reached end of history list ({Count}), closing player",
                        MainWindow.model.HistoryList.Count);
                    // Bail-out return was missing here: falling through to the
                    // HistoryList[HistoryIndex] line below would IndexOutOfRange
                    // when Index == Count. Latent bug, never tripped because
                    // close happens fast enough that nobody finishes a history
                    // run, but worth fixing alongside the deadlock.
                    DeferCloseCurrWindow();
                    return;
                }
                MainWindow.model.HistoryEpisode = MainWindow.model.HistoryList[MainWindow.model.HistoryIndex];
                Log.Information("HistoryWatch: advancing to [{Idx}/{Total}] '{Ep}'",
                    MainWindow.model.HistoryIndex + 1, MainWindow.model.HistoryList.Count,
                    MainWindow.model.HistoryEpisode.Name);
                PlayMediaOnVlcThread(MainWindow.model.HistoryEpisode);
                ShowHistoryWatchBanner(MainWindow.model.HistoryEpisode);
                return;
            }

            if (PlaybackSession.IsCartoonShuffle)
            {
                PlaybackSession.CartoonShuffleIndex++;
                if (PlaybackSession.CartoonShuffleIndex == PlaybackSession.CartoonShuffleLimit)
                {
                    Log.Information("CartoonShuffle: reached limit ({Limit}), closing player",
                        PlaybackSession.CartoonShuffleLimit);
                    skipClosing = true;
                    // Same missing-return bug as history above - falling
                    // through to CartoonShuffleQueue[CartoonShuffleIndex]
                    // would IndexOutOfRange when Index == Limit.
                    DeferCloseCurrWindow();
                    return;
                }

                Episode nextCartoon = PlaybackSession.CartoonShuffleQueue[PlaybackSession.CartoonShuffleIndex];
                Log.Information("CartoonShuffle: advancing to [{Idx}/{Limit}] '{Ep}'",
                    PlaybackSession.CartoonShuffleIndex + 1, PlaybackSession.CartoonShuffleLimit,
                    nextCartoon.Name);
                PlayMediaOnVlcThread(nextCartoon);
                return;
            }

            if (currMedia is Episode episode)
            {
                if (episode.Id < 0)
                {
                    Log.Information("EndReached: Extras episode finished (Id={Id}), closing player without advance",
                        episode.Id);
                    skipClosing = true;
                    DeferCloseCurrWindow();
                    return;
                }

                episode.SavedTime = episode.Length;
                UpdateProgressBar(episode);

                TvShow tvShow = TvShowWindow.tvShow;
                if (tvShow == null)
                {
                    // Defensive: this is only nullable if the player was
                    // opened from somewhere TvShowWindow didn't set up
                    // (or after a window-teardown race). Without the guard
                    // tvShow.GetNextEpisode below NREs into the unhandled-
                    // exception sink. Log clearly and close cleanly instead.
                    Log.Warning("EndReached: TvShowWindow.tvShow is null after episode '{Ep}', can't advance - closing player",
                        episode.Name);
                    skipClosing = true;
                    DeferCloseCurrWindow();
                    return;
                }

                Episode? nextEpisode = tvShow.GetNextEpisode(episode, out bool seasonChanged);
                if (nextEpisode == null)
                {
                    // GetNextEpisode already logged WHY at Information level
                    // (end-of-show / Extras-stop / empty-seasons / not-found).
                    // This line adds the player-side context: the player is
                    // about to close because of that decision.
                    Log.Information("EndReached: '{Show}' no next episode after '{Ep}' - closing player",
                        tvShow.Name, episode.Name);
                    skipClosing = true;
                    DeferCloseCurrWindow();
                    return;
                }

                if (seasonChanged)
                {
                    int newSeasonId = tvShow.FindSeasonIdOf(nextEpisode) ?? tvShow.CurrSeason;
                    Log.Information("EndReached: '{Show}' season transition mid-playback: '{FromEp}' (S{FromSn}) -> '{ToEp}' (S{ToSn})",
                        tvShow.Name, episode.Name, tvShow.CurrSeason, nextEpisode.Name, newSeasonId);
                    tvShow.CurrSeason = newSeasonId;
                    tvShowWindow.Dispatcher.BeginInvoke(() =>
                    {
                        tvShowWindow.UpdateTvWindowSeasonChange(tvShow.CurrSeason);
                    });
                }
                else
                {
                    Log.Information("EndReached: '{Show}' advancing within season: '{FromEp}' -> '{ToEp}'",
                        tvShow.Name, episode.Name, nextEpisode.Name);
                }

                PlayMediaOnVlcThread(nextEpisode);
            }
            else //if Movie
            {
                Log.Information("EndReached: movie finished, closing player");
                skipClosing = true;
                DeferCloseCurrWindow();
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
            Log.Information("Play: {Media} ({Duration})", m.Path, FormatMediaDuration(m));
            ThreadPool.QueueUserWorkItem(_ => mediaPlayer.Play(next));
        }

        // Pretty-prints the media's expected duration for the "Play:" log line.
        // Different source per subtype:
        //   Episode.Length     -> set by MediaPlayer_LengthChanged after the
        //                         first play of this episode, persisted in
        //                         media.json. 0 means never played yet, so we
        //                         can't show a length up front - happens on
        //                         cartoon shuffle picking a first-time episode.
        //   Movie.RunningTime  -> set from TMDB metadata at scan time, in
        //                         minutes. Always known if the movie was
        //                         enriched (the usual case).
        // Anything else -> "?" so the log line still parses.
        private static string FormatMediaDuration(Media m) => m switch
        {
            Episode ep when ep.Length > 0     => TimeSpan.FromMilliseconds(ep.Length).ToString(@"hh\:mm\:ss"),
            Movie mv  when mv.RunningTime > 0 => TimeSpan.FromMinutes(mv.RunningTime).ToString(@"hh\:mm\:ss"),
            _                                  => "?"
        };

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

        // Originally these stopped/started the auto-hide timer on hover so
        // a mouse user reading the timeline wouldn't have the overlay yank
        // out from under them. That semantic actively breaks IR / keyboard
        // usage: every transport command (F, R, End, Home, Space, etc.)
        // ends with FocusPlayerControl warping the cursor onto a button,
        // which synthesizes a MouseEnter. The old Stop() then killed the
        // auto-hide timer we'd just armed in WakeOverlay - overlay never
        // hid. Race-fixing via dispatcher priorities turned out to be
        // unreliable (the WPF queue/Win32-pump interleaving isn't strictly
        // ordered the way the docs imply when both are pending), so we
        // just drop the stop-on-hover behavior outright. Net effect: a
        // mouse user hovering a button will see the overlay tick away
        // after 3s, but a mouse move in the wake-zone re-shows it via
        // VideoView_MouseMove, which is acceptable - same behavior as
        // IR/keyboard. The handlers stay registered as no-ops because the
        // XAML wires them; removing them from XAML too would change all
        // five button definitions.
        private void Control_MouseEnter(object sender, EventArgs e)
        {
        }

        private void Control_MouseLeave(object sender, EventArgs e)
        {
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            closeButton.MouseLeave -= Control_MouseLeave;
            this.Close();
            TcpSerialListener.layoutPoint.NotifyWindowClosedFromUI();
        }

        // Mouse-clickable mirrors of the IR remote's "backward" / "rewind" /
        // "fastforward" / "forward" commands. Same underlying SeekRelative /
        // JumpToEdge methods, plus WakeOverlay() to refresh the auto-hide
        // timer.
        //
        // The WakeOverlay calls weren't here before, which left the overlay
        // visible indefinitely after a mouse click: Control_MouseEnter had
        // stopped the timer on hover, and nothing here restarted it -
        // cursor stayed on the button (no MouseLeave to retrigger it), so
        // overlay never auto-hid. IR remote handlers always called
        // WakeOverlay so the bug was mouse-click specific.
        private void BackwardButton_Click(object sender, RoutedEventArgs e)
        {
            JumpToEdge(toStart: true);
            WakeOverlay();
        }
        private void RewindButton_Click(object sender, RoutedEventArgs e)
        {
            SeekRelative(rewind: true);
            WakeOverlay();
        }
        private void FastForwardButton_Click(object sender, RoutedEventArgs e)
        {
            SeekRelative(rewind: false);
            WakeOverlay();
        }
        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            JumpToEdge(toStart: false);
            WakeOverlay();
        }

        // Delegate to TogglePlayPause so the GUI play button and the IR
        // remote's play/pause/stop commands share one code path:
        //   - mediaPlayer == null guard (prevents NRE during window close)
        //   - Dispatcher.Invoke wrap around UI + DispatcherTimer mutations
        //   - overlayGrid visibility sync (shown on pause, hidden on play)
        //   - Log line for every toggle
        // Previously this handler duplicated TogglePlayPause's logic without
        // the null check or the overlay update, drifting out of sync over time.
        private void PlayButton_Click(object sender, RoutedEventArgs e) => TogglePlayPause();

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
            if (mediaPlayer == null)
            {
                Log.Warning("TogglePlayPause: mediaPlayer is null, ignoring");
                return;
            }

            // The previous version ended each branch with DoMouseClick() +
            // SetCursorPos(). Those were defensive cursor-parking calls from
            // before joystick nav existed. They now actively misfire: after
            // the IR-remote dispatch's FocusPlayerControl warps the cursor
            // onto the play button, DoMouseClick fires PlayButton_Click as
            // a SECOND click on the same press - toggling state back and
            // leaving the user stuck. Cursor positioning is now owned by
            // LayoutPoint.FocusPlayerControl; both side effects are gone.
            //
            // Threading: pollingTimer is a DispatcherTimer - Start/Stop
            // requires the owning dispatcher's thread. Previously the
            // Stop/Start calls lived OUTSIDE the Dispatcher.Invoke and
            // threw InvalidOperationException ("calling thread cannot
            // access this object") when fired from the IR-remote serial
            // thread. Now everything that touches dispatcher-affine state
            // lives inside the single Invoke; mediaPlayer methods (LibVLC,
            // documented thread-safe) are inside too just for symmetry.
            bool isPlaying = mediaPlayer.IsPlaying;
            Log.Information("TogglePlayPause: {From} -> {To}",
                isPlaying ? "playing" : "paused",
                isPlaying ? "paused"  : "playing");

            if (isPlaying)
            {
                _userPaused = true;
                playButton.Dispatcher.Invoke(() =>
                {
                    ApplyPausedVisuals();
                    overlayGrid.Visibility = Visibility.Visible;
                    mediaPlayer.Pause();
                    pollingTimer.Stop();
                });
            }
            else
            {
                _userPaused = false;
                playButton.Dispatcher.Invoke(() =>
                {
                    ApplyPlayingVisuals();
                    overlayGrid.Visibility = Visibility.Hidden;
                    mediaPlayer.Play();
                    pollingTimer.Start();
                });
            }
        }

        internal void JumpToEdge(bool toStart)
        {
            if (mediaPlayer == null)
            {
                Log.Warning("JumpToEdge({ToStart}): mediaPlayer is null, ignoring", toStart);
                return;
            }
            // LibVLC's Length returns -1 (and Time returns -1) until media
            // metadata has finished loading after Play() - typically tens
            // to hundreds of milliseconds, but can spike on slow disks /
            // big remux files. JumpToEdge(false) without this guard would
            // compute target = -1 - 1 = -2, which crashes (or hangs) inside
            // libvlc_media_player_set_time depending on VLC version. Bail
            // out cleanly until length is known.
            long length = mediaPlayer.Length;
            if (length <= 0)
            {
                Log.Warning("JumpToEdge({ToStart}): length not yet known ({Length}ms), ignoring", toStart, length);
                return;
            }
            lastProgrammaticSeekTick = Environment.TickCount;
            long target = toStart ? 0 : length - 1;
            Log.Information("JumpToEdge: toStart={ToStart}, target={Target}ms (of {Length}ms)", toStart, target, length);
            mediaPlayer.SeekTo(TimeSpan.FromMilliseconds(target));
        }

        internal void SeekRelative(bool rewind)
        {
            if (mediaPlayer == null)
            {
                Log.Warning("SeekRelative({Rewind}): mediaPlayer is null, ignoring", rewind);
                return;
            }
            // Same Length=-1 race as JumpToEdge. Math.Clamp(target, 0, -1)
            // throws ArgumentException ("max (-1) is less than min (0)"),
            // which then bubbles out as a fatal app crash when called from
            // the rewind/FF Button click handler (no surrounding try/catch
            // - the IR path catches it inside IrSerialReader.InvokeOnPlayer,
            // but the GUI button click is direct). Bail before clamp.
            long length = mediaPlayer.Length;
            if (length <= 0)
            {
                Log.Warning("SeekRelative({Rewind}): length not yet known ({Length}ms), ignoring", rewind, length);
                return;
            }
            lastProgrammaticSeekTick = Environment.TickCount;

            const int seekStepMs = 30 * 1000;
            long current = mediaPlayer.Time;
            long target  = rewind ? current - seekStepMs : current + seekStepMs;
            target = Math.Clamp(target, 0L, length);
            Log.Information("SeekRelative: rewind={Rewind}, {Current}ms -> {Target}ms (of {Length}ms)",
                rewind, current, target, length);
            mediaPlayer.SeekTo(TimeSpan.FromMilliseconds(target));
        }

        // Last known cursor position seen by VideoView_MouseMove. Used to
        // filter out synthetic MouseMove events that fire when WPF
        // recomputes hit-testing (e.g., when overlayGrid hides, the cursor
        // that was over a button now hits VideoView - WPF posts a
        // synthetic WM_MOUSEMOVE at the SAME position to re-route, and
        // VideoView_MouseMove would otherwise immediately re-show the
        // overlay we just hid). Tracking the position lets us skip the
        // re-show when the cursor hasn't actually moved.
        private Point _lastVideoViewMousePos = new Point(-1, -1);

        private void VideoView_MouseMove(object sender, MouseEventArgs e)
        {
            Point p = Mouse.GetPosition(this);

            // Filter synthetic moves at the same position. Hit-test
            // recompute (triggered by Visibility/IsHitTestVisible changes)
            // posts a WM_MOUSEMOVE at the current cursor position; without
            // this guard, hiding the overlay immediately re-shows it
            // because the cursor happens to be parked on a button at the
            // bottom of the screen (which is in the wake-zone).
            if (p == _lastVideoViewMousePos) return;
            _lastVideoViewMousePos = p;

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
            pollingTimer.Stop();

            // Pause check uses the _userPaused INTENT flag (set by
            // TogglePlayPause), NOT mediaPlayer.IsPlaying. After a seek
            // (F / R / End / Home), LibVLC reports IsPlaying=false for as
            // long as it takes to fill the playback buffer at the new
            // position - can easily exceed our 3-second auto-hide window
            // for larger jumps on slower disks. Trusting IsPlaying here
            // would make every seek leave the overlay pinned.
            // _userPaused only flips when the USER actually pauses (Space /
            // remote play button), which is what the keep-visible policy
            // is actually trying to model.
            if (mediaPlayer == null || _userPaused)
            {
                Log.Debug("PollingTimer_Tick: user-paused or no mediaPlayer - keeping overlay visible");
                return;
            }

            Log.Debug("PollingTimer_Tick: auto-hiding overlay");
            overlayGrid.Visibility = Visibility.Hidden;

            // Capture the cursor position so the synthetic WM_MOUSEMOVE that
            // WPF posts when hit-test recomputes (the cursor was over a
            // button on overlayGrid that just became Hidden -> WPF re-routes
            // to VideoView underneath) doesn't immediately re-show via
            // VideoView_MouseMove. The dedup check in VideoView_MouseMove
            // compares the incoming position against this captured value -
            // identical position = synthetic move, skip the re-show.
            _lastVideoViewMousePos = Mouse.GetPosition(this);
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
