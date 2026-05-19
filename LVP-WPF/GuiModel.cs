using CommunityToolkit.Mvvm.ComponentModel;
using LVP_WPF.Models;
using LVP_WPF.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace LVP_WPF
{
    /// <summary>
    /// Bindable view-model state for MainWindow and the cross-window control
    /// references (close buttons, scroll viewers, etc.) that LayoutPoint pokes
    /// at while moving the cursor between windows.
    ///
    /// Non-view-model concerns that used to live here have been split out:
    ///   - Static helpers   -> Util/WpfTreeHelpers.cs
    ///   - Cursor swap      -> Services/CursorManager.cs
    ///   - Cursor X/Y/flag  -> Services/CursorConfig.cs
    ///   - Item-model DTOs  -> Models/*.cs
    ///   - VLC font opts    -> PlayerWindow private const
    ///   - OVERVIEW_MAX_LEN -> TvShowWindow private const
    /// </summary>
    public partial class GuiModel : ObservableObject
    {
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
    }
}