using System;
using System.Windows;
using System.Windows.Controls;

namespace LVP_WPF.Windows
{
    /// <summary>
    /// Interaction logic for SeasonWindow.xaml
    /// </summary>

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
                    img = tvShow.Seasons[i].Poster == null ? ("Resources\\no-preview-seasons\\" + (i + 1).ToString() + ".png") : tvShow.Seasons[i].Poster;
                }
                seasonBoxes[i] = new SeasonWindowBox
                {
                    Id = tvShow.Seasons[i].Id,
                    Image = Cache.LoadImage(img, 200)
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
                Image img = GuiModel.GetChildrenByType(container, typeof(Image), "seasonImage") as Image;
                TcpSerialListener.layoutPoint.seasonControlList.Add(img);
            }
            scrollViewer = (ScrollViewer)GuiModel.GetScrollViewer(SeasonListView);
            scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            MainWindow.gui.seasonScrollViewer = scrollViewer;
            TcpSerialListener.layoutPoint.seasonIndex = seasonIndex;
            TcpSerialListener.layoutPoint.Select("SeasonWindow");
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            scrollViewerOffset = e.VerticalOffset;
            if (MainWindow.gui.scrollViewerAdjust)
            {
                MainWindow.gui.scrollViewerAdjust = false;
                double offsetPadding = e.VerticalChange > 0 ? 300 : -300;
                scrollViewer.ScrollToVerticalOffset(e.VerticalOffset + offsetPadding);
            }
        }

        private void Window_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
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

        private void SeasonListView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;
        }
    }
}
