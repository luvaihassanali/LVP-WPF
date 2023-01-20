using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LVP_WPF.Windows
{
    /// <summary>
    /// Interaction logic for TvShowWindow.xaml
    /// </summary>

    [ObservableObject]
    public partial class TvShowWindow : Window
    {
        static private TvShow tvShow;

        public static void Show(TvShow t)
        {
            tvShow = t;
            TvShowWindow window = new TvShowWindow();
            window.Caption = tvShow.Name + " (" + tvShow.Date.GetValueOrDefault().Year + ")";
            window.Description = tvShow.Overview.Length > 377 ? tvShow.Overview.Substring(0, 377) + "..." : tvShow.Overview;
            window.Backdrop = Cache.LoadImage(tvShow.Backdrop, 960);
            window.seasonButton.Content = "Season " + tvShow.CurrSeason.ToString();
            window.ShowDialog();
        }

        [ObservableProperty]
        private string caption;
        [ObservableProperty]
        private string runningTime;
        [ObservableProperty]
        private string description;
        [ObservableProperty]
        private BitmapImage backdrop;

        public TvShowWindow()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void Button_Season_Click(object sender, RoutedEventArgs e)
        {
            mainGrid.Opacity = 0.1;
            SeasonWindow.Show(tvShow);
            mainGrid.Opacity = 1.0;
        }
    }
}
