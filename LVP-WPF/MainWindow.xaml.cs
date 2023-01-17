using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
            DisplayControls();

            progressBar.Visibility = Visibility.Collapsed;
            coffeeGif.Visibility = Visibility.Collapsed;
            coffeeGif.Source = null;

            _ = Task.Run(() =>
              {
                  AssignControlContext();
              });
        }

        internal void AssignControlContext()
        {
            Dispatcher.Invoke(() =>
            {
                for (int i = 0; i < model.Movies.Length; i++)
                {
                    gui.Movies.Add(new MainWindowBox { Id = model.Movies[i].Id, Title = model.Movies[i].Name, Image = LoadImage(model.Movies[i].Poster) });
                }

                for (int i = 0; i < model.TvShows.Length; i++)
                {
                    if (model.TvShows[i].Cartoon)
                    {
                        gui.Cartoons.Add(new MainWindowBox { Id = model.TvShows[i].Id, Title = model.TvShows[i].Name, Image = LoadImage(model.TvShows[i].Poster) });
                    }
                    else
                    {
                        gui.TvShows.Add(new MainWindowBox { Id = model.TvShows[i].Id, Title = model.TvShows[i].Name, Image = LoadImage(model.TvShows[i].Poster) });
                    }
                }
            });
        }

        internal void DisplayControls()
        {
            mainGrid.Visibility = Visibility.Visible;
            tvHeader.Visibility = Visibility.Visible;
            movieHeader.Visibility = Visibility.Visible;
            cartoonsHeader.Visibility = Visibility.Visible;
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
