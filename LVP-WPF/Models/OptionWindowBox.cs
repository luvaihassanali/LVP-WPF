namespace LVP_WPF.Models
{
    /// <summary>
    /// DTO for an entry in the TMDB disambiguation dialog (when a search
    /// returns multiple candidates) and for the rows in the
    /// reset-season dialog.
    /// </summary>
    public class OptionWindowBox
    {
        public string Description { get; set; }
        public string Name { get; set; }
        public int Id { get; set; }
    }
}
