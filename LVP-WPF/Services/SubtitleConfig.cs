using System;

namespace LVP_WPF.Services
{
    /// <summary>
    /// What the player needs to know about subtitles when it spins up a
    /// LibVLCSharp.Shared.Media: which VLC subtitle track to enable,
    /// whether an external .srt file is available, and whether the user
    /// has the per-show subtitle toggle on.
    ///
    /// Set by MovieWindow / TvShowWindow before they call PlayerWindow.Show;
    /// read once by PlayerWindow.CreateMedia. Previously three separate
    /// static fields scattered across PlayerWindow.subtitleTrack /
    /// PlayerWindow.subtitleFile / TvShowWindow.subtitleSwitch.
    /// </summary>
    public static class SubtitleConfig
    {
        /// <summary>VLC subtitle track index. Int32.MaxValue means "off / no track."</summary>
        public static int Track { get; set; } = Int32.MaxValue;

        /// <summary>True if an external .srt file is available alongside the media.</summary>
        public static bool HasSrtFile { get; set; }

        /// <summary>
        /// User-controlled toggle in TvShowWindow ("subtitles on/off").
        /// When false, the player skips the .srt slave even if HasSrtFile is true.
        /// </summary>
        public static bool EnableSubtitles { get; set; } = true;
    }
}
