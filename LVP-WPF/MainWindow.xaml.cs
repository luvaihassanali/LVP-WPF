using LVP_WPF.Windows;
using Serilog;
using System;
using System.Configuration;
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
        static private bool mouseHubKilled;
        private InactivityTimer inactivityTimer;
        private double scrollViewerOffset = 0;

        public MainWindow()
        {
            InitializeComponent();
            gui = new GuiModel(ConfigurationManager.AppSettings["Esp8226HideCursor"]);
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
                GuiModel.InitializeCustomCursor();
#endif
                ComInterop.SetCursorPos(GuiModel.centerX, GuiModel.centerY);
                Process[] mouseHubProcess = Process.GetProcessesByName("MouseHub");
                if (mouseHubProcess.Length != 0)
                {
                    mouseHubProcess[0].Kill();
                    mouseHubKilled = true;
                }
            });

            await Cache.Initialize(progressBar, coffeeGif);
            if (model == null) return;
            await AssignControlContext();

            await this.Dispatcher.BeginInvoke(() =>
            {
                if (progressBar.Visibility == Visibility.Visible) progressBar.Visibility = Visibility.Hidden;
                if (coffeeGif.Visibility == Visibility.Visible) coffeeGif.Visibility = Visibility.Hidden;
                coffeeGif.Source = null;
                gui.mainCloseButton = this.closeButton;
                gui.mainScrollViewer = this.scrollViewer;
                gui.mainGrid = this.mainGrid;
                tcpWorker = new TcpSerialListener(gui);
                tcpWorker.StartThread();
            });

            inactivityTimer = new InactivityTimer(TimeSpan.FromMinutes(30));
            inactivityTimer.Inactivity += InactivityDetected;
            PlayerWindow.InitiaizeLibVlcCore();
            MainWindow_Fade(1.0);
            loadGrid.Visibility = Visibility.Hidden;
            if (bool.Parse(ConfigurationManager.AppSettings["Snow"])) snow.Visibility = Visibility.Visible;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            inactivityTimer.Dispose();
            Cache.SaveData();
            GuiModel.RestoreSystemCursor();

            if (tcpWorker != null)
            {
                tcpWorker.StopThread();
            }

            PlayerWindow.libVLC.Dispose();

            if (mouseHubKilled)
            {
                string path = AppDomain.CurrentDomain.BaseDirectory;
#if DEBUG
                path = path.Replace("bin\\Debug\\net6.0-windows\\", "Utilities\\MouseHub\\MouseHub\\bin\\Debug\\MouseHub.exe");
#else
                path = ConfigurationManager.AppSettings["MouseHubPath"] + "MouseHub.exe";
                if (path.Contains("%USERPROFILE%")) { path = path.Replace("%USERPROFILE%", Environment.GetEnvironmentVariable("USERPROFILE")); }
#endif
                Process.Start(path);
            }
        }

        internal async Task AssignControlContext()
        {
            string cartoonExceptionStr = ConfigurationManager.AppSettings["CartoonExceptions"];
            string[] cartoonExceptions = cartoonExceptionStr.Split(";");
            TimeSpan delay = new TimeSpan(1);

            for (int i = 0; i < model.TvShows.Length; i++)
            {
                if (model.TvShows[i].Cartoon && cartoonExceptions.Contains(model.TvShows[i].Name))
                {
                    model.TvShows[i].Cartoon = false;
                }

                if (!model.TvShows[i].Cartoon)
                {
                    string img = model.TvShows[i].Poster == null ? "Resources\\noPrev.png" : model.TvShows[i].Poster;
                    await TvShowListView.Dispatcher.BeginInvoke(() =>
                    {
                        gui.TvShows.Add(new MainWindowBox { Id = model.TvShows[i].Id, Title = model.TvShows[i].Name, Image = Cache.LoadImage(img, 300), Flags = Cache.LoadFlags(model.TvShows[i].Path) });
                    });
                    await Task.Delay(1);
                }
            }

            for (int i = 0; i < model.TvShows.Length; i++)
            {
                if (model.TvShows[i].Cartoon)
                {
                    string img = model.TvShows[i].Poster == null ? "Resources\\noPrev.png" : model.TvShows[i].Poster;
                    await CartoonsListView.Dispatcher.BeginInvoke(() =>
                    {
                        gui.Cartoons.Add(new MainWindowBox { Id = model.TvShows[i].Id, Title = model.TvShows[i].Name, Image = Cache.LoadImage(img, 300) });
                    });
                    await Task.Delay(1);
                    TvShowWindow.cartoons.Add(model.TvShows[i]);
                }
            }

            for (int i = 0; i < model.Movies.Length; i++)
            {
                string img = model.Movies[i].Poster == null ? "Resources\\noPrev.png" : model.Movies[i].Poster;
                await MovieListView.Dispatcher.BeginInvoke(() =>
                {
                    gui.Movies.Add(new MainWindowBox { Id = model.Movies[i].Id, Title = model.Movies[i].Name, Image = Cache.LoadImage(img, 300) });
                });
                await Task.Delay(1);
            }
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
                var mediaItem = gui.mediaDict[item.Id];
                if (mediaItem is Movie)
                {
                    MovieWindow.Show((Movie)mediaItem);
                }
                else
                {
                    TvShowWindow.Show((TvShow)mediaItem);
                }
            }
            MainWindow_Fade(1.0);
            loadGrid.Visibility = Visibility.Hidden;
        }

        private async void MainWindow_Fade(double direction)
        {
            DoubleAnimation da = new DoubleAnimation();
            if (direction == 0.1)
            {
                da.From = 1.0;
                da.To = 0.1;
            } 
            else
            {
                da.From = 0.1;
                da.To = 1.0;
            }
            da.Duration = new Duration(TimeSpan.FromMilliseconds(250));
            da.AutoReverse = false;
            da.RepeatBehavior = new RepeatBehavior(1); 
            this.Dispatcher.BeginInvoke(() =>
            {
                mainGrid.BeginAnimation(OpacityProperty, da);
            });
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            scrollViewerOffset = e.VerticalOffset;
            if (e.VerticalOffset == 0)
            {
                closeButton.Visibility = Visibility.Visible;
            }
            else
            {
                closeButton.Visibility = Visibility.Hidden;
            }

            if (gui.scrollViewerAdjust)
            {
                gui.scrollViewerAdjust = false;
                double offsetPadding = e.VerticalChange > 0 ? 300 : -300;
                scrollViewer.ScrollToVerticalOffset(e.VerticalOffset + offsetPadding);
            }
        }

        private void MainWindow_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewerOffset - 300);
            }
            else
            {

                scrollViewer.ScrollToVerticalOffset(scrollViewerOffset + 300);
            }
        }

        private async void InactivityDetected(object sender, EventArgs e)
        {
            if (gui.isPlaying) return;
            foreach (Window w in Application.Current.Windows)
            {
                if (w as TvShowWindow != null) w.Close();
            }
            await Task.Delay(1000);
            Log.Information("Inactivity shutdown");
            Application.Current.Shutdown();
        }
    }
}
