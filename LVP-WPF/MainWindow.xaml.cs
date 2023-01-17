using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LVP_WPF
{
    public partial class MainWindow : Window
    {
        static public MainModel model;
        static public GuiModel gui;

        public MainWindow()
        {
            InitializeComponent();
            gui = new GuiModel();
            DataContext = gui;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Cache.Initialize(progressBar);
            await Task.Run(() => { AssignControlContext(); });
            loadGrid.Visibility = Visibility.Collapsed;
            coffeeGif.Source = null;
        }

        internal void AssignControlContext()
        {
            for (int i = 0; i < model.Movies.Length; i++)
            {
                MovieBox.Dispatcher.Invoke(() =>
                {
                    gui.Movies.Add(new MainWindowBox { Id = model.Movies[i].Id, Title = model.Movies[i].Name, Image = LoadImage(model.Movies[i].Poster) });
                });
            }

            for (int i = 0; i < model.TvShows.Length; i++)
            {
                if (model.TvShows[i].Cartoon)
                {
                    CartoonBox.Dispatcher.Invoke(() =>
                    {
                        gui.Cartoons.Add(new MainWindowBox { Id = model.TvShows[i].Id, Title = model.TvShows[i].Name, Image = LoadImage(model.TvShows[i].Poster) });
                    });
                }
                else
                {
                    TvShowBox.Dispatcher.Invoke(() =>
                    {
                        gui.TvShows.Add(new MainWindowBox { Id = model.TvShows[i].Id, Title = model.TvShows[i].Name, Image = LoadImage(model.TvShows[i].Poster) });
                    });
                }
            }
        }

        private BitmapImage LoadImage(string filename)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory;
            return new BitmapImage(new Uri(path + filename));
        }

        private void Coffee_Gif_Ended(object sender, EventArgs e)
        {
            coffeeGif.Position = TimeSpan.FromMilliseconds(1);
        }
    }
}
