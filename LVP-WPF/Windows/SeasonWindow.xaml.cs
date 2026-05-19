using LVP_WPF.Models;
using LVP_WPF.Services;
using LVP_WPF.Util;
using System;
using System.Windows;
using System.Windows.Controls;

namespace LVP_WPF.Windows
{
    public partial class SeasonWindow : Window
    {
        private static int seasonIndex = 0;
        private static SeasonWindowBox[] seasons;
        private static ScrollViewer scrollViewer;

        public static int Show(TvShow tvShow)
        {
            seasonIndex = tvShow.CurrSeason == -1 ? tvShow.Seasons.Length - 1 : tvShow.CurrSeason - 1;
            SeasonWindow seasonWindow = new SeasonWindow();
            SeasonWindowBox[] seasonBoxes = new SeasonWindowBox[tvShow.Seasons.Length];
            for (int i = 0; i < tvShow.Seasons.Length; i++)
            {
                string img;
                if (tvShow.Seasons[i].Id == -1)
                {
                    img = "Resources\\extras.png";
                }
                else
                {
                    img = tvShow.Seasons[i].Poster == null ? $"Resources\\no-preview-seasons\\{i + 1}.png" : tvShow.Seasons[i].Poster;
                }
                seasonBoxes[i] = new SeasonWindowBox
                {
                    Id = tvShow.Seasons[i].Id,
                    Image = ImageLoader.Load(img, 200)
                };
            }
            seasons = seasonBoxes;
            seasonWindow.SeasonListView.ItemsSource = seasons;
            seasonWindow.ShowDialog();
            return seasonIndex;
        }

        private double scrollViewerOffset = 0;

        public SeasonWindow()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void SeasonListView_Click(object sender, RoutedEventArgs e)
        {
            TcpSerialListener.layoutPoint.Select(String.Empty);
            SeasonWindowBox item = (SeasonWindowBox)(sender as ListView).SelectedItem;
            seasonIndex = item.Id;
            this.Close();
        }

        private void SeasonWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ItemContainerGenerator generator = SeasonListView.ItemContainerGenerator;
            for (int j = 0; j < seasons.Length; j++)
            {
                ListViewItem container = (ListViewItem)generator.ContainerFromItem(seasons[j]);
                Image img = WpfTreeHelpers.GetChildrenByType(container, typeof(Image), "seasonImage") as Image;
                TcpSerialListener.layoutPoint.seasonControlList.Add(img);
            }
            scrollViewer = (ScrollViewer)WpfTreeHelpers.GetScrollViewer(SeasonListView);
            scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            MainWindow.gui.seasonScrollViewer = scrollViewer;
            TcpSerialListener.layoutPoint.seasonIndex = seasonIndex;
            TcpSerialListener.layoutPoint.Select("SeasonWindow");
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            scrollViewerOffset = e.VerticalOffset;
            ScrollHelper.ApplyAdjust(scrollViewer, e);
        }

        private void Window_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
            => ScrollHelper.StepFromWheel(scrollViewer, scrollViewerOffset, e);

        private void SeasonListView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;
        }
    }
}
