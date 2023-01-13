using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace LVP_WPF
{
    internal class GuiModel
    {
    }

    public class MainMovieBox
    {
        private string title;
        public string Title
        {
            get { return this.title; }
            set { this.title = value; }
        }

        private BitmapImage img;
        public BitmapImage Image
        {
            get { return this.img; }
            set { this.img = value; }
        }

    }
}
