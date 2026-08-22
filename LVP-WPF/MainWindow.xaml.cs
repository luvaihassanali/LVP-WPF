using LVP_WPF.Models;
using LVP_WPF.Services;
using LVP_WPF.Util;
using LVP_WPF.Windows;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LVP_WPF
{
    public partial class MainWindow : Window
    {
        static public MainModel model;
        static public GuiModel gui;
        static public TcpSerialListener tcpWorker;
        static internal MediaLibrary library;
        static private bool mouseHubKilled;
        private InactivityTimer inactivityTimer;
        private double scrollViewerOffset = 0;

        public MainWindow()
        {
            InitializeComponent();
            AppConfig.Initialize();
            gui = new GuiModel();
            DataContext = gui;
#if DEBUG
            this.WindowStyle = WindowStyle.SingleBorderWindow;
            this.AllowsTransparency = false;
#endif

            // Kill any running MouseHub BEFORE the window's Loaded handler
            // gets a chance to spin up TcpSerialListener and try to open the
            // serial port. Both processes share the same COM port; if
            // MouseHub still has it when LVP's listener starts, we get a
            // duplicate-open failure. Originally this lived in the ctor
            // (commit 6c3011e), then "update mouse hub" (29c3ce9) moved it
            // into Window_ContentRendered alongside the cursor-positioning -
            // but ContentRendered fires AFTER Loaded starts and races with
            // TcpSerialListener.StartThread(). Synchronous kill here, before
            // Loaded ever runs.
            //
            // WaitForExit gives Windows a beat to release the COM port
            // handle - Kill() returns immediately but the handle table
            // cleanup is asynchronous in the kernel. 2s is far more than
            // enough; failures fall through to the warning below.
            try
            {
                foreach (Process p in Process.GetProcessesByName("MouseHub"))
                {
                    p.Kill();
                    if (!p.WaitForExit(2000))
                    {
                        Log.Warning("MouseHub did not exit within 2s after Kill()");
                    }
                    mouseHubKilled = true;
                }
            }
            catch (Exception ex)
            {
                // Process can exit between GetProcessesByName and Kill;
                // also fails with Win32 access-denied if MouseHub was
                // started elevated. Either way, continue startup - if the
                // port really is held, TcpSerialListener will surface that
                // separately.
                Log.Warning("Failed to kill MouseHub: {Message}", ex.Message);
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Stopwatch mwSw = Stopwatch.StartNew();
            Serilog.Log.Information("MW.Loaded: START");

            await Task.Run(() =>
            {
#if RELEASE
                CursorManager.InitializeCustomCursor();
#endif
            });
            Serilog.Log.Information("MW.Loaded: cursor init done {Ms}ms", mwSw.ElapsedMilliseconds);

            // Show the progress bar during the regular load (not just the
            // rebuild path). Indeterminate while we don't know totals yet
            // (scan / JSON load / compare); flipped to determinate before
            // AssignControlContext, which has known counts and ticks per tile.
            progressBar.IsIndeterminate = true;
            progressBar.Visibility = Visibility.Visible;

            long beforeInit = mwSw.ElapsedMilliseconds;
            library = new MediaLibrary(new MediaRepository("media.json"));
            WpfLoadProgress loadProgress = new WpfLoadProgress(progressBar, logTxtBox);
            await library.Initialize(loadProgress);
            Serilog.Log.Information("MW.Loaded: library.Initialize await done at {Ms}ms (took {InitMs}ms)",
                mwSw.ElapsedMilliseconds, mwSw.ElapsedMilliseconds - beforeInit);
            if (model == null)
            {
                return;
            }

            // Switch to determinate now that we know the total work for the
            // tile-population phase: every TV show + cartoon + movie becomes
            // one tile. AssignControlContext increments ProgressBarValue per
            // tile added.
            int totalTiles = model.TvShows.Length + model.Movies.Length;
            gui.ProgressBarMax = totalTiles;
            gui.ProgressBarValue = 0;
            progressBar.IsIndeterminate = false;

            await AssignControlContext();

            await this.Dispatcher.BeginInvoke(() =>
            {
                // These get switched to Visible by WpfLoadProgress.ShowRebuildIndicators
                // when a TMDB rebuild was needed; if no rebuild ran they're still Hidden.
                // Setting to Hidden is a no-op when already Hidden.
                progressBar.Visibility = Visibility.Hidden;
                coffeeGif.Visibility = Visibility.Hidden;
                // coffeeGif is now an Image driven by WpfAnimatedGif - hiding
                // it stops it being painted. The storyboard keeps running in
                // memory but doesn't render; cheap enough to leave alone.
                // No more MediaElement Source = null teardown needed.

                gui.mainCloseButton = this.closeButton;
                gui.mainScrollViewer = this.scrollViewer;
                gui.mainGrid = this.mainGrid;
                gui.historyButton = this.historyButton;
                gui.shuffleButton = this.shuffleButton;
                tcpWorker = new TcpSerialListener(gui);
                tcpWorker.StartThread();
            });

            // Inactivity shutdown timer. Resets on any mouse/keyboard/stylus
            // input event via InactivityTimer.PreNotifyInput. Skipped when
            // playback is active (see InactivityDetected: gui.isPlaying gate).
            //
            // DEBUG builds use a much shorter 2-minute timeout so the shutdown
            // path is testable in a development session without waiting half
            // an hour. Production gets the full 30 minutes.
#if DEBUG
            TimeSpan inactivityTimeout = TimeSpan.FromMinutes(2);
#else
            TimeSpan inactivityTimeout = TimeSpan.FromMinutes(30);
#endif
            inactivityTimer = new InactivityTimer(inactivityTimeout);
            inactivityTimer.Inactivity += InactivityDetected;
            PlayerWindow.InitializeLibVlcCore();
            MainWindow_Fade(1.0);
            // Final drain + stop the timer before the load grid disappears.
            // Any dialog-auto-log lines that fired late will still land in
            // the TextBox before it gets hidden.
            loadProgress.StopLogDrain();
            loadGrid.Visibility = Visibility.Hidden;
            if (AppConfig.ShowSnow)
            {
                snow.Visibility = Visibility.Visible;
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            // Cursor positioning lives here (not in the ctor) because we
            // need the window to actually be on screen first - SetCursorPos
            // before paint can land the cursor at the old window position.
            // MouseHub kill USED to be here too but moved back to the ctor;
            // see ctor comment for the race condition that fixed.
            _ = Task.Run(() =>
            {
                // Top-right corner of coffeeGif, computed from screen-center + gif half-dims.
                // coffee.gif is 498x431 native; rendered 1:1 centered in the loadGrid.
                ComInterop.SetCursorPos(CursorConfig.CenterX + 249, CursorConfig.CenterY - 216);
            });
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            inactivityTimer?.Dispose();
            library?.SaveData();
            CursorManager.RestoreSystemCursor();
            tcpWorker?.StopThread();
            PlayerWindow.libVLC.Dispose();

            if (mouseHubKilled)
            {
#if DEBUG
                // Dev build: hop over to the sibling MouseHub project's matching
                // bin folder. BaseDirectory looks like
                //   ...\LVP-WPF\bin\Debug\<tfm>\
                // so derive the TFM and the project root from that path instead
                // of hard-coding "net10.0-windows" - any future TFM bump won't
                // break this line.
                string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar);
                string tfm = System.IO.Path.GetFileName(baseDir);
                string projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, "..", "..", ".."));
                string path = System.IO.Path.Combine(projectRoot, "Hubs", "MouseHub", "MouseHub", "bin", "Debug", tfm, "MouseHub.exe");
#else
                string path = Environment.ExpandEnvironmentVariables($"{AppConfig.MouseHubPath}MouseHub.exe");
#endif
                Process.Start(path);
            }
        }

        internal async Task AssignControlContext()
        {
            Stopwatch totalSw = Stopwatch.StartNew();

            // Apply runtime cartoon overrides before partitioning. Both
            // directions of override are needed AT THIS POINT (not just in
            // MediaEnricher) because MediaEnricher only runs on the
            // needsRebuild path - shows already in media.json bypass
            // enrichment entirely on subsequent launches. Applying here
            // makes the flags respond to config changes without a rebuild.
            //
            //   CartoonExceptions: TMDB genre=Animation but treat as regular TV
            //                      (e.g. anime that's aimed at adults).
            //   ForceCartoons:     TMDB genre != Animation but treat as cartoon
            //                      (e.g. That's So Raven / Smart Guy - live-
            //                      action, but the user wants them in the
            //                      cartoons partition + shuffle pool).
            foreach (TvShow show in model.TvShows)
            {
                if (show.Cartoon && AppConfig.CartoonExceptions.Contains(show.Name))
                {
                    show.Cartoon = false;
                }
                if (AppConfig.ForceCartoons.Contains(show.Name))
                {
                    show.Cartoon = true;
                }
            }

            // Partition once instead of double-iterating model.TvShows with
            // Cartoon checks in two of the three old loops.
            TvShow[] tvShows  = model.TvShows.Where(s => !s.Cartoon).ToArray();
            TvShow[] cartoons = model.TvShows.Where(s =>  s.Cartoon).ToArray();

            // Preserve the side effect from the old cartoon loop - cartoons
            // are also tracked in a flat list used by the S-hotkey / IR-remote
            // "play random cartoons" marathon.
            foreach (TvShow c in cartoons) TvShowWindow.cartoons.Add(c);

            await LoadCategoryAsync(tvShows, gui.TvShows, "TvShows", show => new MainWindowBox
            {
                Id = show.Id,
                Title = show.Name,
                Image = ImageLoader.LoadPoster(show.Poster),
                Flags = show.MultiLang ? ImageLoader.LoadFlags(show.Path) : null
            });

            await LoadCategoryAsync(cartoons, gui.Cartoons, "Cartoons", show => new MainWindowBox
            {
                Id = show.Id,
                Title = show.Name,
                Image = ImageLoader.LoadPoster(show.Poster)
            });

            await LoadCategoryAsync(model.Movies, gui.Movies, "Movies", movie => new MainWindowBox
            {
                Id = movie.Id,
                Title = movie.Name,
                Image = ImageLoader.LoadPoster(movie.Poster)
            });

            Serilog.Log.Information("AssignControlContext total: {Ms}ms", totalSw.ElapsedMilliseconds);
        }

        // Load a category of tiles by decoding posters in parallel (CPU-bound
        // JPEG decode scales across all cores) then batch-adding a chunk to
        // the bound ObservableCollection in a single dispatcher hop.
        //
        // Replaces the previous per-tile pattern:
        //     await Task.Run(decode)            // 1 worker
        //     await BeginInvoke(collection.Add) // 1 dispatcher hop
        //     await Task.Delay(1)               // ~15ms on Windows!
        //
        // On a ~200-tile library this went from several seconds to a few
        // hundred ms. The biggest wins are (a) parallel decode across N cores
        // and (b) removing the Task.Delay(1) which on Windows is actually
        // ~15.6ms per call thanks to the default timer resolution -> 3s of
        // pure waiting at 200 tiles.
        //
        // Order is preserved: Parallel.For writes into an indexed array and
        // the UI add iterates that array in order.
        // BitmapImage.Freeze() inside ImageLoader.Load makes the decoded
        // instances immutable and safe to hand to the UI thread from any
        // worker.
        private async Task LoadCategoryAsync<T>(
            T[] items,
            ObservableCollection<MainWindowBox> target,
            string label,
            Func<T, MainWindowBox> factory)
        {
            if (items.Length == 0) return;

            Stopwatch sw = Stopwatch.StartNew();

            // CHUNK trades visible-progress smoothness vs dispatcher overhead.
            // 16 is roughly one row of tiles on screen and keeps the progress
            // bar advancing in human-noticeable steps.
            const int CHUNK = 16;

            for (int i = 0; i < items.Length; i += CHUNK)
            {
                int start = i;
                int count = Math.Min(CHUNK, items.Length - start);
                MainWindowBox[] boxes = new MainWindowBox[count];

                // Decode this chunk's posters in parallel on the thread pool.
                await Task.Run(() => Parallel.For(0, count, k =>
                {
                    boxes[k] = factory(items[start + k]);
                }));

                // One dispatcher hop adds the whole chunk. Background priority
                // lets layout/render passes interleave with the inserts so the
                // coffee spinner keeps animating smoothly.
                await Dispatcher.BeginInvoke(() =>
                {
                    for (int k = 0; k < count; k++) target.Add(boxes[k]);
                }, DispatcherPriority.Background);

                gui.ProgressBarValue += count;
            }

            Serilog.Log.Information("LoadCategory {Label}: {Count} tiles in {Ms}ms",
                label, items.Length, sw.ElapsedMilliseconds);
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            // Guard against stacking sessions - mouse clicks can race the
            // same way IR presses can. See IrSerialReader case "cartoons"
            // for the full explanation (multiple PlayerWindows only one
            // tracked -> orphaned audio decoders on exit).
            if (TcpSerialListener.layoutPoint?.playerWindowActive == true)
            {
                Serilog.Log.Warning("ShuffleButton_Click IGNORED: player already open");
                return;
            }
            // Same dispatch shape as the IR remote's "cartoons" command and the
            // S hotkey in App.GlobalKeyUp - run the marathon on an STA pump
            // thread so its modal PlayerWindow can own the message loop.
            TcpSerialListener.StaThreadWrapper(() => TvShowWindow.PlayRandomCartoons());
        }

        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (gui == null || model == null || model.HistoryList.Count == 0) return;
            if (TcpSerialListener.layoutPoint?.playerWindowActive == true)
            {
                Serilog.Log.Warning("HistoryButton_Click IGNORED: player already open");
                return;
            }
            TcpSerialListener.StaThreadWrapper(() => TvShowWindow.PlayHistoryList());
        }


        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ListView_Click(object sender, RoutedEventArgs e)
        {
            loadGrid.Visibility = Visibility.Visible;
            MainWindow_Fade(0.1);
            MainWindowBox item = (MainWindowBox)(sender as ListView).SelectedItem;
            if (item != null)
            {
                Media? mediaItem = gui.mediaDict[item.Id];
                if (mediaItem is Movie movie)
                {
                    MovieWindow.Show(movie);
                }
                else
                {
                    TvShowWindow.Show((TvShow)mediaItem);
                }
            }
            MainWindow_Fade(1.0);
            loadGrid.Visibility = Visibility.Hidden;
        }

        private void MainWindow_Fade(double direction)
            => this.Dispatcher.BeginInvoke(() => FadeHelper.Fade(mainGrid, fadeOut: direction == 0.1));

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            scrollViewerOffset = e.VerticalOffset;
            closeButton.Visibility = e.VerticalOffset == 0 ? Visibility.Visible : Visibility.Hidden;
            ScrollHelper.ApplyAdjust(scrollViewer, e);
        }

        private void MainWindow_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
            => ScrollHelper.StepFromWheel(scrollViewer, scrollViewerOffset, e);

        private async void InactivityDetected(object sender, EventArgs e)
        {
            // Runs on the UI dispatcher thread (DispatcherTimer-backed event).
            // The async/await keeps the continuation on the UI thread too,
            // so direct WPF mutation (Window.Close, Application.Current.Shutdown)
            // is safe without further marshalling.
            if (gui.isPlaying)
            {
                // Debug-level: this is the no-op branch of inactivity handling
                // - fires routinely during long playback sessions and adds
                // nothing actionable. The shutdown branch below stays at
                // Information because that one matters.
                Log.Debug("InactivityDetected: playback active, ignoring");
                return;
            }

            int closed = 0;
            foreach (Window w in Application.Current.Windows)
            {
                if (w is TvShowWindow)
                {
                    w.Close();
                    closed++;
                }
            }
            Log.Information("InactivityDetected: closing app ({Closed} TvShowWindow(s) closed first, 1s grace then Shutdown)", closed);
            await Task.Delay(1000);
            Log.Information("Inactivity shutdown");
            Application.Current.Shutdown();
        }
    }
}
