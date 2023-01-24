﻿using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace LVP_WPF.Windows
{
    /// <summary>
    /// Interaction logic for MovieWindow.xaml
    /// </summary>

    [ObservableObject]
    public partial class MovieWindow : Window
    {
        private static Movie movie;

        public static void Show(Movie m)
        {
            MovieWindow window = new MovieWindow();
            window.MovieName = m.Name;
            TimeSpan temp = TimeSpan.FromMinutes(m.RunningTime);
            string hour = temp.Hours > 1 ? "hours " : "hour ";
            window.RunningTime = "Running time: " + temp.Hours + " " + hour + temp.Minutes + " minutes";
            window.Description = m.Overview.Length > 1011 ? m.Overview.Substring(0, 1011) + "..." : m.Overview;
            string img = m.Backdrop == null ? "Resources\\noPrevWide.png" : m.Backdrop;
            window.Backdrop = Cache.LoadImage(img, 960);
            window.Overlay = Cache.LoadImage("Resources\\play.png", 960);
            movie = m;
            window.ShowDialog();
        }

        [ObservableProperty]
        private string movieName;
        [ObservableProperty]
        private string runningTime;
        [ObservableProperty]
        private string description;
        [ObservableProperty]
        private BitmapImage backdrop;
        [ObservableProperty]
        private BitmapImage overlay;

        public MovieWindow()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void Backdrop_MouseEnter(object sender, MouseEventArgs e)
        {
            this.PlayOverlay.Opacity = 1.0;
        }

        private void Backdrop_MouseLeave(object sender, MouseEventArgs e)
        {
            this.PlayOverlay.Opacity = 0;
        }

        private void Play_Click(object sender, MouseButtonEventArgs e)
        {
            PlayerWindow.Show(movie);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
