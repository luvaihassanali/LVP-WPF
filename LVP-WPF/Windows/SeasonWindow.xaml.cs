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
            SeasonWindowBox item = (SeasonWindowBox)(sender as ListView).SelectedItem;
            if (item == null) return;
            seasonIndex = item.Id;
            // Defer BOTH the layout-state cleanup AND the window Close to
            // after the WPF click chain completes. The handler is wired to
            // PreviewMouseLeftButtonUp (tunneling phase); WPF still has the
            // bubbling MouseLeftButtonUp + ListView's internal MouseUp
            // processing to run on the same event. Closing the window
            // synchronously here destroys the HWND mid-chain and downstream
            // handlers in PresentationCore throw Win32Exception "Invalid
            // window handle".
            //
            // Critically, Select("") must also be deferred and run in the
            // SAME dispatcher item as Close. If Select runs immediately,
            // layoutpoint flips to "tvShow active" while the SeasonWindow is
            // still visually open and the modal pump is still active - any
            // IR-remote / joystick input during that window routes to the
            // wrong nav target and the app gets confused (the "layout point
            // goes back to tv form while season form still open" symptom).
            Dispatcher.BeginInvoke(new Action(() =>
            {
                TcpSerialListener.layoutPoint.Select(String.Empty);
                Close();
            }), System.Windows.Threading.DispatcherPriority.Background);
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
