using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace LVP_WPF
{
    public partial class MainWindow : Window
    {
        static public MainModel model;
        static public GuiModel gui;

        public MainWindow()
        {
            InitializeComponent();
            gui = new GuiModel();
            DataContext = gui;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Cache.Initialize(progressBar);
            progressBar.Visibility = Visibility.Collapsed;
            coffeeGif.Visibility = Visibility.Collapsed;
            coffeeGif.Source = null;
            DisplayControls();
        }

        private void Coffee_Gif_Ended(object sender, EventArgs e)
        {
            coffeeGif.Position = TimeSpan.FromMilliseconds(1);
        }

        internal void DisplayControls()
        {
            mainGrid.Visibility = Visibility.Visible;
            //tvHeader.Dispatcher.Invoke(() => { tvHeader.Visibility = Visibility.Visible; });
            //movieHeader.Dispatcher.Invoke(() => { movieHeader.Visibility = Visibility.Visible; });
            //cartoonsHeader.Dispatcher.Invoke(() => { cartoonsHeader.Visibility = Visibility.Visible; });
            tvHeader.Visibility = Visibility.Visible;
            /*this.MovieBox.ItemsSource = new MainWindowBox[]
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
            };*/
        }

        int index = 1;
        private BitmapImage LoadImage(string filename)
        {
            return new BitmapImage(new Uri("C:\\Users\\luv\\Desktop\\lvp-temp\\tmp\\" + (index++).ToString() + ".jpg"));
        }
    }
}
