using CommunityToolkit.Mvvm.ComponentModel;
using LVP_WPF.Windows;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

        public static void DoEvents()
        {
            Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(delegate { }));
        }

        // https://stackoverflow.com/questions/37247724/find-controls-placed-inside-listview-wpf
        public static Visual? GetChildrenByType(Visual visualElement, Type typeElement, string nameElement)
        {
            if (visualElement == null) return null;
            if (visualElement.GetType() == typeElement)
            {
                FrameworkElement? fe = visualElement as FrameworkElement;
                if (fe != null)
                {
                    if (fe.Name == nameElement)
                    {
                        return fe;
                    }
                }
            }

            Visual? foundElement = null;
            if (visualElement is FrameworkElement)
            {
                ((FrameworkElement)visualElement).ApplyTemplate();
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(visualElement); i++)
            {
                Visual visual = (Visual)VisualTreeHelper.GetChild(visualElement, i);
                foundElement = GetChildrenByType(visual, typeElement, nameElement);
                if (foundElement != null) break;
            }
            return foundElement;
        }

        public static DependencyObject? GetScrollViewer(DependencyObject o)
        {
            if (o is ScrollViewer) return o;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                DependencyObject? child = VisualTreeHelper.GetChild(o, i);
                DependencyObject? result = GetScrollViewer(child);
                if (result == null) continue;
                else return result;
            }
            return null;
        }

        internal static void RestoreSystemCursor()
        {
            string[] keys = Properties.Resources.keys_backup.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            foreach (string key in keys)
            {
                string[] keyValuePair = key.Split('=');
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Cursors\", keyValuePair[0], keyValuePair[1]);
            }
            ComInterop.SystemParametersInfo(ComInterop.SPI_SETCURSORS, 0, 0, ComInterop.SPIF_UPDATEINIFILE | ComInterop.SPIF_SENDCHANGE);
            ComInterop.SystemParametersInfo(0x2029, 0, 32, 0x01);
        }

        internal static void InitializeCustomCursor()
        {
            string cursorPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
            string[] keys = Properties.Resources.keys_custom.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            foreach (string key in keys)
            {
                string[] keyValuePair = key.Split('=');
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Cursors\", keyValuePair[0], cursorPath + keyValuePair[1]);
            }
            ComInterop.SystemParametersInfo(ComInterop.SPI_SETCURSORS, 0, 0, ComInterop.SPIF_UPDATEINIFILE | ComInterop.SPIF_SENDCHANGE);
            ComInterop.SystemParametersInfo(0x2029, 0, 72, 0x01);
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