using LVP_WPF.Windows;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LVP_WPF
{
    public partial class MainWindow : Window
    {
        static public MainModel model;
        static public GuiModel gui;
        static private bool mouseHubKilled;
        private InactivityTimer inactivityTimer;
        private TcpSerialListener worker = null;

        public MainWindow()
        {
            InitializeComponent();
            gui = new GuiModel();
            DataContext = gui;

            Process[] mouseHubProcess = Process.GetProcessesByName("MouseHub");
            if (mouseHubProcess.Length != 0)
            {
                mouseHubProcess[0].Kill();
                mouseHubKilled = true;
            }

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
            
            gui.CloseButtons[0] = this.closeButton;
            gui.MainGrid = this.mainGrid;
            worker = new TcpSerialListener(gui);
            worker.StartThread();
        }

        private void InactivityDetected(object sender, EventArgs e)
        {
            if (gui.IsPlaying) return;
            this.Close();
        }

        private void ListView_Click(object sender, RoutedEventArgs e)
        {
            mainGrid.Opacity = 0.1;
            MainWindowBox item = (MainWindowBox)(sender as ListView).SelectedItem;
            if (item != null)
            {
                var mediaItem = gui.MediaDict[item.Id];
                if (mediaItem is Movie)
                {
                    MovieWindow.Show((Movie)mediaItem);
                }
                else
                {
                    TvShowWindow.Show((TvShow)mediaItem);
                }
            }
            mainGrid.Opacity = 1.0;
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

        private void Coffee_Gif_Ended(object sender, EventArgs e)
        {
            coffeeGif.Position = TimeSpan.FromMilliseconds(1);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            //To-do: change to only on mouse over
            if (e.VerticalOffset == 0)
            {
                closeButton.Visibility = Visibility.Visible;
            }
            else
            {
                closeButton.Visibility = Visibility.Hidden;
            }
        }

        private void CartoonsHeader_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TvShowWindow.PlayRandomCartoons();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            inactivityTimer.Dispose();

            if (mouseHubKilled)
            {
                string path = AppDomain.CurrentDomain.BaseDirectory;
#if DEBUG
                path = path.Replace("bin\\Debug\\", "Utilities\\MouseHub\\MouseHub\\bin\\Debug\\MouseHub.exe");
#else
                path = path.Replace("bin\\Release\\", "Utilities\\MouseHub\\MouseHub\\bin\\Release\\MouseHub.exe");
#endif
                Process.Start(path);
            }
        }
    }
}
