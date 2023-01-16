using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace LVP_WPF
{
    public partial class MainWindow : Window
    {
        static public MainModel model;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //To-do: start loader
            await Cache.Initialize();

            this.MovieBox.ItemsSource = new MainWindowBox[]
            {
                new MainWindowBox{Title="Movie 1", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Movie 2", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Movie 3", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Movie 4", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Movie 5", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Movie 6", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Movie 7", Image=LoadImage("image.jpg")}
            };
            this.TvBox.ItemsSource = new MainWindowBox[]
            {
                new MainWindowBox{Title="TV 1", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="TV 2", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="TV 3", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="TV 4", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="TV 5", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="TV 6", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="TV 7", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="TV 8", Image=LoadImage("image.jpg")},
            };
            this.CartoonBox.ItemsSource = new MainWindowBox[]
            {
                new MainWindowBox{Title="Cartoon 1", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Cartoon 2", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Cartoon 3", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Cartoon 4", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Cartoon 5", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Cartoon 6", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Cartoon 7", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Cartoon 8", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Cartoon 1", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Cartoon 2", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Cartoon 3", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Cartoon 4", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Cartoon 5", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Cartoon 6", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Cartoon 7", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Cartoon 8", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Cartoon 7", Image=LoadImage("image.jpg")},
                new MainWindowBox{Title="Cartoon 8", Image=LoadImage("image.jpg")}
            };
            // stop loader
        }

        int index = 1;
        private BitmapImage LoadImage(string filename)
        {
            return new BitmapImage(new Uri("C:\\Users\\luv\\Desktop\\lvp-temp\\" + (index++).ToString() + ".jpg"));
        }
    }
}
