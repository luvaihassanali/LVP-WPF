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
    /// Interaction logic for MovieWindow.xaml
    /// </summary>
    
    [ObservableObject]
    public partial class MovieWindow : Window
    {
        public static void Show(Movie movie)
        {
            MovieWindow window = new MovieWindow();
            window.Caption = movie.Name;
            TimeSpan temp = TimeSpan.FromMinutes(movie.RunningTime);
            string hour = temp.Hours > 1 ? "hours " : "hour ";
            window.RunningTime = "Running time: " + temp.Hours + " " + hour + temp.Minutes + " minutes";
            window.Description = movie.Overview.Length > 1011 ? movie.Overview.Substring(0, 1011) + "..." : movie.Overview;
            window.Backdrop = Cache.LoadImage(movie.Backdrop, 960);
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

        public MovieWindow()
        {
            DataContext = this;
            InitializeComponent();
        }
    }
}
