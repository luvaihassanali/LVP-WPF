using CommunityToolkit.Mvvm.ComponentModel;
using LVP_WPF.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace LVP_WPF
{
    public partial class GuiModel : ObservableObject
    {
        public const int OVERVIEW_MAX_LEN = 370;

        [ObservableProperty]
        private int progressBarValue = 1;
        [ObservableProperty]
        private int progressBarMax = 100;
        [ObservableProperty]
        ObservableCollection<MainWindowBox> movies = new ObservableCollection<MainWindowBox>();
        [ObservableProperty]
        ObservableCollection<MainWindowBox> tvShows = new ObservableCollection<MainWindowBox>();
        [ObservableProperty]
        ObservableCollection<MainWindowBox> cartoons = new ObservableCollection<MainWindowBox>();

        static public bool hideCursor = false;
        static public int hideCursorX = 35;
        static public int hideCursorY = 1100;
        static public int centerX = 960;
        static public int centerY = 540;
        static public string fontSize;
        static public string fontStyle;
        public bool isPlaying = false;
        public bool scrollViewerAdjust = false;
        public Button mainCloseButton;
        public Button tvMovieCloseButton;
        public Button playerCloseButton;
        public Dictionary<int, Media> mediaDict = new Dictionary<int, Media>();
        public Grid mainGrid;
        public PlayerWindow playerWindow;
        public ScrollViewer mainScrollViewer;
        public ScrollViewer episodeScrollViewer;
        public ScrollViewer seasonScrollViewer;
        public ScrollViewer langScrollViewer;

        public GuiModel(string? h)
        {
            if (h != null)
            {
                hideCursor = bool.Parse(h);
            }
            else
            {
                hideCursor = false;
            }

            fontSize = "--freetype-fontsize=48";
            fontStyle = "--freetype-font=Segoe UI";
        }

    }

    public partial class MainWindowBox
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public BitmapImage Image { get; set; }
        public BitmapImage[] Flags { get; set; }
    }

    public class OptionWindowBox
    {
        public string Description { get; set; }
        public string Name { get; set; }
        public int Id { get; set; }
    }

    public class SeasonWindowBox
    {
        public int Id { get; set; }
        public BitmapImage Image { get; set; }
    }

    public partial class EpisodeWindowBox : ObservableObject
    {
        [ObservableProperty]
        private int progress;
        [ObservableProperty]
        private int total;
        [ObservableProperty]
        private double opacity;

        public int Id { get; set; }
        public string Description { get; set; }
        public string Name { get; set; }
        public BitmapImage Image { get; set; }
        public BitmapImage Overlay { get; set; }
    }
}