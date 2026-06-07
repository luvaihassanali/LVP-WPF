using System.Windows.Media.Imaging;

namespace LVP_WPF.Models
{
    /// <summary>DTO for tiles in the season-picker overlay.</summary>
    public class SeasonWindowBox
    {
        public int Id { get; set; }
        public BitmapImage Image { get; set; }
    }
}
