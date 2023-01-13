using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace LVP_WPF
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // start loader
            //Cache.ProcessRootDirectories();
            // create model
            // set data context -> load gui
            // stop loader
            this.TvBox.ItemsSource = new MovieData[]
            {
                new MovieData{Title="Movie 1", ImageData=LoadImage("image.jpg")},
                new MovieData{Title="Movie 2", ImageData=LoadImage("image.jpg")},
                new MovieData{Title="Movie 3", ImageData=LoadImage("image.jpg")},
                new MovieData{Title="Movie 4", ImageData=LoadImage("image.jpg")},
                new MovieData{Title="Movie 5", ImageData=LoadImage("image.jpg")},
                new MovieData{Title="Movie 6", ImageData=LoadImage("image.jpg")},
                new MovieData{Title="Movie 7", ImageData=LoadImage("image.jpg")}
            };
        }

        int index = 1;
        private BitmapImage LoadImage(string filename)
        {
            return new BitmapImage(new Uri("C:\\Users\\luv\\Desktop\\temp\\" + (index++).ToString() + ".jpg"));
        }
    }
}
