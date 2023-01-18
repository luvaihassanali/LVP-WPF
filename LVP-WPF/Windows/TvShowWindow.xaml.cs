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
        public static void Show(TvShow tvShow)
        {
            /*InputDialog dialog = new InputDialog();
             dialog.Caption = caption;
             dialog.Message = message;
             dialog.Topmost = true;
             dialog.ShowDialog();
             if ((bool)dialog.DialogResult)
             {
                 return true;
             }
             return false;*/
        }

        /*[ObservableProperty]
        private string caption;
        [ObservableProperty]
        private string message;*/

        public TvShowWindow()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void Button_Season_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
