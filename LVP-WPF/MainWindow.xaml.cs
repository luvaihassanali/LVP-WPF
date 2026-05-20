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
            await Task.Run(() =>
            {
#if RELEASE
                CursorManager.InitializeCustomCursor();
#endif
            });

            library = new MediaLibrary(new MediaRepository("media.json"));
            await library.Initialize(new WpfLoadProgress(progressBar, coffeeGif, logTxtBox));
            if (model == null)
            {
                return;
            }

            await AssignControlContext();

            await this.Dispatcher.BeginInvoke(() =>
            {
                // These get switched to Visible by WpfLoadProgress.ShowRebuildIndicators
                // when a TMDB rebuild was needed; if no rebuild ran they're still Hidden.
                // Setting to Hidden is a no-op when already Hidden.
                progressBar.Visibility = Visibility.Hidden;
                coffeeGif.Visibility = Visibility.Hidden;
                logTxtBox.Visibility = Visibility.Hidden;
                coffeeGif.Source = null;

                gui.mainCloseButton = this.closeButton;
                gui.mainScrollViewer = this.scrollViewer;
                gui.mainGrid = this.mainGrid;
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
                ComInterop.SetCursorPos(CursorConfig.CenterX, CursorConfig.CenterY);
                Process[] mouseHubProcess = Process.GetProcessesByName("MouseHub");
                if (mouseHubProcess.Length != 0)
                {
                    mouseHubProcess[0].Kill();
                    mouseHubKilled = true;
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
                string path = AppDomain.CurrentDomain.BaseDirectory;
#if DEBUG
                // Dev build: walk over to the sibling MouseHub project's bin folder.
                // The old substitution string had stale "net6.0-windows" (we're now
                // on net10) and referenced a "Utilities\" folder that was renamed
                // to "Hubs\" long ago - both fixed here.
                path = path.Replace("bin\\Debug\\net10.0-windows\\", "Hubs\\MouseHub\\MouseHub\\bin\\Debug\\net10.0-windows\\MouseHub.exe");
#else
                path = Environment.ExpandEnvironmentVariables($"{AppConfig.MouseHubPath}MouseHub.exe");
#endif
                Process.Start(path);
            }
        }

        internal async Task AssignControlContext()
        {
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
                await AddTileAsync(gui.TvShows, new MainWindowBox
                {
                    Id = show.Id,
                    Title = show.Name,
                    Image = ImageLoader.LoadPoster(show.Poster),
                    Flags = ImageLoader.LoadFlags(show.Path)
                });
            }

            foreach (TvShow show in model.TvShows)
            {
                if (!show.Cartoon) continue;
                await AddTileAsync(gui.Cartoons, new MainWindowBox
                {
                    Id = show.Id,
                    Title = show.Name,
                    Image = ImageLoader.LoadPoster(show.Poster)
                });
                TvShowWindow.cartoons.Add(show);
            }

            foreach (Movie movie in model.Movies)
            {
                await AddTileAsync(gui.Movies, new MainWindowBox
                {
                    Id = movie.Id,
                    Title = movie.Name,
                    Image = ImageLoader.LoadPoster(movie.Poster)
                });
            }
        }

        // Hand off ObservableCollection mutation to the UI thread (the bound
        // ListView lives there) and yield briefly so layout can catch up
        // before the next tile starts loading.
        private async Task AddTileAsync(System.Collections.ObjectModel.ObservableCollection<MainWindowBox> collection, MainWindowBox box)
        {
            await this.Dispatcher.BeginInvoke(() => collection.Add(box));
            await Task.Delay(1);
        }

        private void CartoonsHeader_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TvShowWindow.PlayRandomCartoons();
        }

        private void Coffee_Gif_Ended(object sender, EventArgs e)
        {
            coffeeGif.Position = TimeSpan.FromMilliseconds(1);
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
