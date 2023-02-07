using LVP_WPF.Windows;
using Microsoft.Win32;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace LVP_WPF
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll", EntryPoint = "SystemParametersInfo")]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, uint pvParam, uint fWinIni);
        private const int SPI_SETCURSORS = 0x0057;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDCHANGE = 0x02;

        static public MainModel model;
        static public GuiModel gui;
        static public TcpSerialListener tcpWorker;
        static private bool mouseHubKilled;
        private InactivityTimer inactivityTimer;
        private double scrollViewerOffset = 0;

        public MainWindow()
        {
            TcpSerialListener.SetCursorPos(500, 2000);
            InitializeCustomCursor();
            InitializeComponent();
            gui = new GuiModel();
            DataContext = gui;

            Process[] mouseHubProcess = Process.GetProcessesByName("MouseHub");
            if (mouseHubProcess.Length != 0)
            {
                mouseHubProcess[0].Kill();
                mouseHubKilled = true;
            }
#if DEBUG
            this.WindowStyle = WindowStyle.SingleBorderWindow;
#endif
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await Cache.Initialize(progressBar);
            await Task.Run(() => { AssignControlContext(); });

            Panel.SetZIndex(loadGrid, -1);
            progressBar.Visibility = Visibility.Collapsed;
            coffeeGif.Source = null;
            inactivityTimer = new InactivityTimer(TimeSpan.FromMinutes(30)); //(TimeSpan.FromSeconds(5));
            inactivityTimer.Inactivity += InactivityDetected;

            gui.mainCloseButton = this.closeButton;
            gui.mainScrollViewer = this.scrollViewer;
            gui.mainGrid = this.mainGrid;

            if (Cache.update)
            {
                await Task.Delay(100);
            }
            else
            {
                await Task.Delay(10);
            }

            tcpWorker = new TcpSerialListener(gui);
            tcpWorker.StartThread();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            Cache.SaveData();
            RestoreSystemCursor();
            inactivityTimer.Dispose();

            if (tcpWorker != null)
            {
                tcpWorker.StopThread();
            }

            if (mouseHubKilled)
            {
                string path = AppDomain.CurrentDomain.BaseDirectory;
#if DEBUG
                path = path.Replace("bin\\Debug\\", "Utilities\\MouseHub\\MouseHub\\bin\\Debug\\MouseHub.exe");
#else
                path = ConfigurationManager.AppSettings["MouseHubPath"] + "MouseHub.exe";
                if (path.Contains("%USERPROFILE%")) { path = path.Replace("%USERPROFILE%", Environment.GetEnvironmentVariable("USERPROFILE")); }
#endif
                Process.Start(path);
            }
        }

        internal void AssignControlContext()
        {
            for (int i = 0; i < model.Movies.Length; i++)
            {
                string img = model.Movies[i].Poster == null ? "Resources\\noPrev.png" : model.Movies[i].Poster;
                MovieListView.Dispatcher.Invoke(() =>
                {
                    gui.Movies.Add(new MainWindowBox { Id = model.Movies[i].Id, Title = model.Movies[i].Name, Image = Cache.LoadImage(img, 300) });
                });
            }

            for (int i = 0; i < model.TvShows.Length; i++)
            {
                if (model.TvShows[i].Cartoon)
                {
                    string img = model.TvShows[i].Poster == null ? "Resources\\noPrev.png" : model.TvShows[i].Poster;
                    CartoonsListView.Dispatcher.Invoke(() =>
                    {
                        gui.Cartoons.Add(new MainWindowBox { Id = model.TvShows[i].Id, Title = model.TvShows[i].Name, Image = Cache.LoadImage(img, 300) });
                    });
                    TvShowWindow.cartoons.Add(model.TvShows[i]);
                }
                else
                {
                    string img = model.TvShows[i].Poster == null ? "Resources\\noPrev.png" : model.TvShows[i].Poster;
                    TvShowListView.Dispatcher.Invoke(() =>
                    {
                        gui.TvShows.Add(new MainWindowBox { Id = model.TvShows[i].Id, Title = model.TvShows[i].Name, Image = Cache.LoadImage(img, 300) });
                    });
                }
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

        private void InactivityDetected(object sender, EventArgs e)
        {
            if (gui.isPlaying) return;
            this.Close();
        }

        private void ListView_Click(object sender, RoutedEventArgs e)
        {
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
        }

        private void MainWindow_Fade(double direction)
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
            mainGrid.BeginAnimation(OpacityProperty, da);
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

        private void RestoreSystemCursor()
        {
            string[] keys = Properties.Resources.keys_backup.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            foreach (string key in keys)
            {
                string[] keyValuePair = key.Split('=');
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Cursors\", keyValuePair[0], keyValuePair[1]);
            }
            SystemParametersInfo(SPI_SETCURSORS, 0, 0, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            SystemParametersInfo(0x2029, 0, 32, 0x01);
        }

        private void InitializeCustomCursor()
        {
            string cursorPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
            string[] keys = Properties.Resources.keys_custom.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            foreach (string key in keys)
            {
                string[] keyValuePair = key.Split('=');
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Cursors\", keyValuePair[0], cursorPath + keyValuePair[1]);
            }
            SystemParametersInfo(SPI_SETCURSORS, 0, 0, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            SystemParametersInfo(0x2029, 0, 128, 0x01);
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
    }
}
