using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Imaging;

namespace LVP_WPF
{
    [ObservableObject]
    public partial class GuiModel
    {
        [ObservableProperty]
        private string loadLabel;
        [ObservableProperty]
        private int progressBarValue;

        public GuiModel()
        {
            progressBarValue = 0;
        }
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
