﻿using CommunityToolkit.Mvvm.ComponentModel;
using LVP_WPF.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LVP_WPF
{
    [ObservableObject]
    public partial class GuiModel
    {
        [ObservableProperty]
        private int progressBarValue;
        [ObservableProperty]
        private int progressBarMax;
        [ObservableProperty]
        ObservableCollection<MainWindowBox> movies;
        [ObservableProperty]
        ObservableCollection<MainWindowBox> tvShows;
        [ObservableProperty]
        ObservableCollection<MainWindowBox> cartoons;

        private bool isPlaying;
        static private bool loggingEnabled;
        static private string logPath;
        private Button[] closeButtons;
        private Dictionary<int, Media> mediaDict;
        private Grid mainGrid;
        private PlayerWindow playerWindow;

        public GuiModel()
        {
            progressBarValue = 5;
            progressBarMax = 100;
            movies = new ObservableCollection<MainWindowBox>();
            tvShows = new ObservableCollection<MainWindowBox>();
            cartoons = new ObservableCollection<MainWindowBox>();

            loggingEnabled = bool.Parse(ConfigurationManager.AppSettings["LoggingEnabled"]);
            logPath = ConfigurationManager.AppSettings["LogPath"] + "LVP-WPF.log";
            if (logPath.Contains("%USERPROFILE%"))
            {
                logPath = logPath.Replace("%USERPROFILE%", Environment.GetEnvironmentVariable("USERPROFILE"));
            }

            CloseButtons = new Button[3];
            MediaDict = new Dictionary<int, Media>();
            IsPlaying = false;
        }

        public bool IsPlaying
        {
            get { return isPlaying; }
            set { isPlaying = value; }
        }

        public Dictionary<int, Media> MediaDict
        {
            get => mediaDict;
            set => mediaDict = value;
        }

        public Grid MainGrid
        {
            get => mainGrid;
            set => mainGrid = value;
        }

        public PlayerWindow PlayerWindow
        {
            get { return playerWindow; }
            set { playerWindow = value; }
        }

        public Button[] CloseButtons
        {
            get { return closeButtons; }
            set { closeButtons = value; }
        }

        public static void Log(string message)
        {
            if (loggingEnabled)
            {
                using (StreamWriter sw = File.AppendText(logPath))
                {
                    sw.WriteLine("{0} - {1}: {2}", DateTime.Now.ToString("dd-MM-yy HH:mm:ss.fff"), (new StackTrace()).GetFrame(1).GetMethod().Name, message);
                    Debug.WriteLine("{0} - {1}: {2}", DateTime.Now.ToString("dd-MM-yy HH:mm:ss.fff"), (new StackTrace()).GetFrame(1).GetMethod().Name, message);
                }
            }
        }
    }

    public partial class MainWindowBox
    {
        private int id;
        private string title;
        private BitmapImage image;

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


    [ObservableObject]
    public partial class EpisodeWindowBox
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