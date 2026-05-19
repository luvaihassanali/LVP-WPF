using System.Windows.Media.Imaging;

namespace LVP_WPF.Models
{
    /// <summary>
    /// DTO for the tiles on the main scrollable grid (TV shows, cartoons,
    /// movies). For multi-language TV shows, Flags carries up to 16 small
    /// flag images shown as overlays on top of the poster.
    /// </summary>
    public partial class MainWindowBox
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public BitmapImage Image { get; set; }
        public BitmapImage[] Flags { get; set; }
    }
}
