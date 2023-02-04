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
            seasonIndex = tvShow.CurrSeason;
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
                    img = tvShow.Seasons[i].Poster == null ? "Resources\\noPrev.png" : tvShow.Seasons[i].Poster;
                }
                seasonBoxes[i] = new SeasonWindowBox
                {
                    Id = tvShow.Seasons[i].Id,
                    Image = Cache.LoadImage(img, 150)
                };
            }
            seasons = seasonBoxes;
            seasonWindow.SeasonListView.ItemsSource = seasons;
            seasonWindow.ShowDialog();
            return seasonIndex;
        }

        public SeasonWindow()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void SeasonListView_Click(object sender, RoutedEventArgs e)
        {
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
                MainWindow.tcpWorker.layoutPoint.seasonControlList.Add(img);
            }
            scrollViewer = (ScrollViewer)GuiModel.GetScrollViewer(SeasonListView);
            scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            MainWindow.gui.seasonScrollViewer = scrollViewer;
            MainWindow.tcpWorker.layoutPoint.Select("SeasonWindow");
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (MainWindow.gui.scrollViewerAdjust)
            {
                MainWindow.gui.scrollViewerAdjust = false;
                double offsetPadding = e.VerticalChange > 0 ? 300 : -300;
                scrollViewer.ScrollToVerticalOffset(e.VerticalOffset + offsetPadding);
            }
        }
    }
}
