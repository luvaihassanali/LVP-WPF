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

        /// <summary>
        /// Display label for the tile (e.g. "E07"). Returns empty string
        /// for non-positive Ids so Extras (which use negative Ids from
        /// LibraryScanner._extrasIdx) and any pre-enrichment episodes
        /// (Id==0) don't show a meaningless "E00" or "E-1".
        ///
        /// Surfacing the episode number in the tile is what lets users
        /// notice missing episodes - e.g. SNL S08 with E18 missing now
        /// visibly reads E17, E19, E20 instead of three tiles in a row
        /// that look sequential.
        ///
        /// Id is set once via object initializer in CreateEpisodeListItems
        /// and never changes after, so a get-only computed property is
        /// sufficient - no INotifyPropertyChanged plumbing needed.
        /// </summary>
        public string Label => Id > 0 ? $"E{Id:D2}" : "";
    }
}
