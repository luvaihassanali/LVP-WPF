using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

        private void ListView_Click(object sender, RoutedEventArgs e)
        {
            MainWindowBox item = (MainWindowBox)(sender as ListView).SelectedItem;
            if (item != null)
            {
                var mediaItem = model.MediaDict[item.Id];
                if (mediaItem is Movie)
                {
                    Trace.WriteLine("Movie");
                }
                else
                {
                    Trace.WriteLine("TV");
                }
            }
        }

        internal void AssignControlContext()
        {
            for (int i = 0; i < model.Movies.Length; i++)
            {
                MovieBox.Dispatcher.Invoke(() =>
                {
                    gui.Movies.Add(new MainWindowBox { Id = model.Movies[i].Id, Title = model.Movies[i].Name, Image = LoadImage(model.Movies[i].Poster, 300) });
                });
            }

            for (int i = 0; i < model.TvShows.Length; i++)
            {
                if (model.TvShows[i].Cartoon)
                {
                    CartoonBox.Dispatcher.Invoke(() =>
                    {
                        gui.Cartoons.Add(new MainWindowBox { Id = model.TvShows[i].Id, Title = model.TvShows[i].Name, Image = LoadImage(model.TvShows[i].Poster, 300) });
                    });
                }
                else
                {
                    TvShowBox.Dispatcher.Invoke(() =>
                    {
                        gui.TvShows.Add(new MainWindowBox { Id = model.TvShows[i].Id, Title = model.TvShows[i].Name, Image = LoadImage(model.TvShows[i].Poster, 300) });
                    });
                }
            }
        }

        private BitmapImage LoadImage(string filename, int pixelWidth)
        {
            //To-do: resize images to see if helps memory
            string path = AppDomain.CurrentDomain.BaseDirectory;
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path + filename);
            image.DecodePixelWidth = pixelWidth;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private void Coffee_Gif_Ended(object sender, EventArgs e)
        {
            coffeeGif.Position = TimeSpan.FromMilliseconds(1);
        }
    }
}
