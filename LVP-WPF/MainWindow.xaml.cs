using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace LVP_WPF
{
    public partial class MainWindow : Window
    {
        static public MainModel mainModel;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // start loader
            Cache.ProcessRootDirectories();
            // create model
            // set data context -> load gui
            // stop loader
            this.MovieBox.ItemsSource = new MainMovieBox[]
            {
                new MainMovieBox{Title="Movie 1", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Movie 2", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Movie 3", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Movie 4", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Movie 5", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Movie 6", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Movie 7", Image=LoadImage("image.jpg")}
            };
            this.TvBox.ItemsSource = new MainMovieBox[]
            {
                new MainMovieBox{Title="TV 1", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="TV 2", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="TV 3", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="TV 4", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="TV 5", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="TV 6", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="TV 7", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="TV 8", Image=LoadImage("image.jpg")},
            };
            this.CartoonBox.ItemsSource = new MainMovieBox[]
            {
                new MainMovieBox{Title="Cartoon 1", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Cartoon 2", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Cartoon 3", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Cartoon 4", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Cartoon 5", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Cartoon 6", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Cartoon 7", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Cartoon 8", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Cartoon 1", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Cartoon 2", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Cartoon 3", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Cartoon 4", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Cartoon 5", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Cartoon 6", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Cartoon 7", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Cartoon 8", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Cartoon 7", Image=LoadImage("image.jpg")},
                new MainMovieBox{Title="Cartoon 8", Image=LoadImage("image.jpg")}
            };
        }

        int index = 1;
        private BitmapImage LoadImage(string filename)
        {
            return new BitmapImage(new Uri("C:\\Users\\luv\\Desktop\\lvp-temp\\" + (index++).ToString() + ".jpg"));
        }
    }
}
