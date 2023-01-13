using System.Windows;

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
            Cache.ProcessRootDirectories();
            // create model
            // set data context -> load gui
            // stop loader
        }
    }
}
