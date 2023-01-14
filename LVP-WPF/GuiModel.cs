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

    public class MainWindowBox
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

    public class OptionWindowBox
    {
        private string name;
        public string Name
        {
            get { return this.name; }
            set { this.name = value; }
        }

        private string description;
        public string Description
        {
            get { return this.description; }
            set { this.description = value; }
        }

        private int id;
        public int Id
        {
            get { return this.id; }
            set { this.id = value; }
        }
    }
}
