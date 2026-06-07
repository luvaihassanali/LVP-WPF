using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Imaging;

namespace LVP_WPF.Models
{
    /// <summary>
    /// DTO for episode tiles inside TvShowWindow. Carries the per-episode
    /// progress (current/total) so the watch-progress bar can render, plus
    /// a fading opacity hover overlay.
    /// </summary>
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
