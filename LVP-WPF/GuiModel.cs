using CommunityToolkit.Mvvm.ComponentModel;
using LVP_WPF.Windows;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LVP_WPF
{
    public partial class GuiModel : ObservableObject
    {
        [DllImport("user32.dll")]
        static extern void mouse_event(Int32 dwFlags, Int32 dx, Int32 dy, Int32 dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfo")]

        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, uint pvParam, uint fWinIni);
        private const int SPI_SETCURSORS = 0x0057;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDCHANGE = 0x02;
        private const int MOUSEEVENTF_MOVE = 0x0001;
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

        public GuiModel()
        {
            hideCursor = bool.Parse(ConfigurationManager.AppSettings["Esp8226HideCursor"]);
            fontSize = "--freetype-fontsize=" + ConfigurationManager.AppSettings["FontSize"];
            fontStyle = "--freetype-font=" + ConfigurationManager.AppSettings["FontStyle"];
        }

        public static void DoEvents()
        {
            Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(delegate { }));
        }

        // https://stackoverflow.com/questions/37247724/find-controls-placed-inside-listview-wpf
        public static Visual GetChildrenByType(Visual visualElement, Type typeElement, string nameElement)
        {
            if (visualElement == null) return null;
            if (visualElement.GetType() == typeElement)
            {
                FrameworkElement fe = visualElement as FrameworkElement;
                if (fe != null)
                {
                    if (fe.Name == nameElement)
                    {
                        return fe;
                    }
                }
            }

            Visual foundElement = null;
            if (visualElement is FrameworkElement)
            {
                (visualElement as FrameworkElement).ApplyTemplate();
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(visualElement); i++)
            {
                Visual visual = VisualTreeHelper.GetChild(visualElement, i) as Visual;
                foundElement = GetChildrenByType(visual, typeElement, nameElement);
                if (foundElement != null)
                    break;
            }
            return foundElement;
        }

        public static DependencyObject GetScrollViewer(DependencyObject o)
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
            SystemParametersInfo(SPI_SETCURSORS, 0, 0, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            SystemParametersInfo(0x2029, 0, 32, 0x01);
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
            SystemParametersInfo(SPI_SETCURSORS, 0, 0, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            SystemParametersInfo(0x2029, 0, 72, 0x01);
        }
    }

    public partial class MainWindowBox
    {
        private int id;
        private string title;
        private BitmapImage image;
        private BitmapImage[] flags;

        public int Id
        {
            get { return id; }
            set { id = value; }
        }

        public string Title
        {
            get { return title; }
            set { title = value; }
        }

        public BitmapImage Image
        {
            get { return image; }
            set { image = value; }
        }

        public BitmapImage[] Flags
        {
            get { return flags; }
            set { flags = value; }
        }
    }

    public class OptionWindowBox
    {
        private int id;
        private string description;
        private string name;

        public string Description
        {
            get { return description; }
            set { description = value; }
        }

        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        public int Id
        {
            get { return id; }
            set { id = value; }
        }
    }

    public class SeasonWindowBox
    {
        private int id;
        private BitmapImage image;

        public int Id
        {
            get { return id; }
            set { id = value; }
        }

        public BitmapImage Image
        {
            get { return image; }
            set { image = value; }
        }
    }

    public partial class EpisodeWindowBox : ObservableObject
    {
        private int id;
        private string description;
        private string name;
        private BitmapImage image;
        private BitmapImage overlay;

        [ObservableProperty]
        private int progress;
        [ObservableProperty]
        private int total;
        [ObservableProperty]
        private double opacity;

        public int Id
        {
            get { return id; }
            set { id = value; }
        }

        public string Description
        {
            get { return description; }
            set { description = value; }
        }

        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        public BitmapImage Image
        {
            get { return image; }
            set { image = value; }
        }

        public BitmapImage Overlay
        {
            get { return overlay; }
            set { overlay = value; }
        }
    }
}