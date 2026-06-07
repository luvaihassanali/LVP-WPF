using LVP_WPF.Models;
using LVP_WPF.Services;
using LVP_WPF.Util;
using LVP_WPF.Windows;
using Serilog;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

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
            //progressBar.IsIndeterminate = true;
            //progressBar.Visibility = Visibility.Visible;

            long beforeInit = mwSw.ElapsedMilliseconds;
            library = new MediaLibrary(new MediaRepository("media.json"));
            await library.Initialize(new WpfLoadProgress(progressBar));
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
            progressBar.Visibility = Visibility.Visible;

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

            inactivityTimer = new InactivityTimer(TimeSpan.FromMinutes(30));
            inactivityTimer.Inactivity += InactivityDetected;
            PlayerWindow.InitializeLibVlcCore();
            MainWindow_Fade(1.0);
            loadGrid.Visibility = Visibility.Hidden;
            if (AppConfig.ShowSnow)
            {
                snow.Visibility = Visibility.Visible;
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            _ = Task.Run(() =>
            {
                // Top-right corner of coffeeGif, computed from screen-center + gif half-dims.
                // coffee.gif is 498x431 native; rendered 1:1 centered in the loadGrid.
                ComInterop.SetCursorPos(CursorConfig.CenterX + 249, CursorConfig.CenterY - 216);

                Process[] mouseHubProcess = Process.GetProcessesByName("MouseHub");
                if (mouseHubProcess.Length == 0) return;
                try
                {
                    mouseHubProcess[0].Kill();
                    mouseHubKilled = true;
                }
                catch (Exception ex)
                {
                    // Process can exit between GetProcessesByName and Kill;
                    // also fails with Win32 access-denied if MouseHub was started elevated.
                    Log.Warning("Failed to kill MouseHub: {Message}", ex.Message);
                }
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

            // Apply runtime cartoon-exception overrides before partitioning.
            foreach (TvShow show in model.TvShows)
            {
                if (show.Cartoon && AppConfig.CartoonExceptions.Contains(show.Name))
                {
                    show.Cartoon = false;
                }
            }

            foreach (TvShow show in model.TvShows)
            {
                if (show.Cartoon) continue;
                // Off-thread decode: ImageLoader.LoadPoster does file I/O +
                // JPEG decode, and LoadFlags walks the show's directory to
                // collect language flag PNGs. Doing this synchronously on the
                // UI thread per-tile starves WPF's compositor and makes the
                // loader spinner + incoming tiles jitter. BitmapImage.Freeze()
                // (inside ImageLoader.Load) is what makes the result safe to
                // hand back to the UI thread from a worker.
                MainWindowBox box = await Task.Run(() => new MainWindowBox
                {
                    Id = show.Id,
                    Title = show.Name,
                    Image = ImageLoader.LoadPoster(show.Poster),
                    Flags = show.MultiLang ? ImageLoader.LoadFlags(show.Path) : null
                });
                await AddTileAsync(gui.TvShows, box);
            }

            foreach (TvShow show in model.TvShows)
            {
                if (!show.Cartoon) continue;
                MainWindowBox box = await Task.Run(() => new MainWindowBox
                {
                    Id = show.Id,
                    Title = show.Name,
                    Image = ImageLoader.LoadPoster(show.Poster)
                });
                await AddTileAsync(gui.Cartoons, box);
                TvShowWindow.cartoons.Add(show);
            }

            foreach (Movie movie in model.Movies)
            {
                MainWindowBox box = await Task.Run(() => new MainWindowBox
                {
                    Id = movie.Id,
                    Title = movie.Name,
                    Image = ImageLoader.LoadPoster(movie.Poster)
                });
                await AddTileAsync(gui.Movies, box);
            }

            Serilog.Log.Information("AssignControlContext: {Ms}ms", totalSw.ElapsedMilliseconds);
        }

        // Hand off ObservableCollection mutation to the UI thread (the bound
        // ListView lives there), tick the progress bar, and yield briefly so
        // layout can catch up before the next tile starts loading.
        private async Task AddTileAsync(System.Collections.ObjectModel.ObservableCollection<MainWindowBox> collection, MainWindowBox box)
        {
            await this.Dispatcher.BeginInvoke(() => collection.Add(box));
            gui.ProgressBarValue++;
            await Task.Delay(1);
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            // Same dispatch shape as the IR remote's "cartoons" command and the
            // S hotkey in App.GlobalKeyUp - run the marathon on an STA pump
            // thread so its modal PlayerWindow can own the message loop.
            TcpSerialListener.StaThreadWrapper(() => TvShowWindow.PlayRandomCartoons());
        }

        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (gui == null || model == null || model.HistoryList.Count == 0) return;
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
            if (gui.isPlaying)
            {
                return;
            }

            foreach (Window w in Application.Current.Windows)
            {
                if (w is TvShowWindow) w.Close();
            }
            await Task.Delay(1000);
            Log.Information("Inactivity shutdown");
            Application.Current.Shutdown();
        }
    }
}
