﻿using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private Dictionary<int, Media> mediaDict;

        public GuiModel()
        {
            progressBarValue = 5;
            progressBarMax = 100;
            movies = new ObservableCollection<MainWindowBox>();
            tvShows = new ObservableCollection<MainWindowBox>();
            cartoons = new ObservableCollection<MainWindowBox>();
            mediaDict = new Dictionary<int, Media>();
        }

        public Dictionary<int, Media> MediaDict
        {
            get => mediaDict;
            set => mediaDict = value;
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
