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
    /// Interaction logic for SeasonWindow.xaml
    /// </summary>
    
    public partial class SeasonWindow : Window
    {
        public static void Show(TvShow tvShow)
        {
            SeasonWindow seasonWindow = new SeasonWindow();
            SeasonWindowBox[] seasonBoxes = new SeasonWindowBox[tvShow.Seasons.Length];
            for (int i = 0; i < tvShow.Seasons.Length; i++)
            {
                seasonBoxes[i] = new SeasonWindowBox
                {
                    Id = tvShow.Seasons[i].Id,
                    Image = Cache.LoadImage(tvShow.Seasons[i].Poster, 150)
                };
            }
            seasonWindow.SeasonBox.ItemsSource = seasonBoxes;
            seasonWindow.ShowDialog();
        }

        public SeasonWindow()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void ListView_Click(object sender, RoutedEventArgs e)
        {
            SeasonWindowBox item = (SeasonWindowBox)(sender as ListView).SelectedItem;
        }
    }
}
