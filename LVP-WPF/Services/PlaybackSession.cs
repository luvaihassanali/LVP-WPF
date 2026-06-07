using System;
using System.Collections.Generic;

namespace LVP_WPF.Services
{
    public enum PlaybackMode { Normal, CartoonShuffle, HistoryWatch }

    /// <summary>
    /// Session state for the two special playback modes - shuffle through
    /// random cartoon episodes (S hotkey) or play through all watched
    /// episodes by date (W hotkey). Static for the same reason TvShowWindow's
    /// old fields were: only one player is ever open at a time so a single
    /// global state suffices.
    ///
    /// Was previously six `static internal` fields on TvShowWindow
    /// (historyWatch, cartoonShuffle, cartoonIndex, cartoonLimit,
    /// cartoonShuffleList) - this consolidates them with named transitions.
    /// </summary>
    public static class PlaybackSession
    {
        public static PlaybackMode Mode { get; private set; } = PlaybackMode.Normal;

        public static bool IsCartoonShuffle => Mode == PlaybackMode.CartoonShuffle;
        public static bool IsHistoryWatch => Mode == PlaybackMode.HistoryWatch;

        /// <summary>The pre-rolled queue of random cartoon episodes for shuffle mode.</summary>
        public static List<Episode> CartoonShuffleQueue { get; } = new List<Episode>();

        /// <summary>Cursor into <see cref="CartoonShuffleQueue"/>; advanced after each episode.</summary>
        public static int CartoonShuffleIndex { get; set; }

        /// <summary>How many episodes the current shuffle session was sized to play.</summary>
        public static int CartoonShuffleLimit { get; private set; }

        private static readonly Random _random = new Random();

        public static void StartCartoonShuffle(int limit, IReadOnlyList<TvShow> availableShows)
        {
            Mode = PlaybackMode.CartoonShuffle;
            CartoonShuffleQueue.Clear();
            CartoonShuffleLimit = limit;
            CartoonShuffleIndex = 0;
            for (int i = 0; i < limit; i++)
            {
                CartoonShuffleQueue.Add(PickRandomEpisode(availableShows));
            }
        }

        /// <summary>
        /// Walks down show -> season -> episode picking uniformly at random
        /// at each level. Note: not actually uniform over all episodes;
        /// shows with fewer episodes get over-represented relative to ones
        /// with more. Preserved from the original behavior.
        /// </summary>
        private static Episode PickRandomEpisode(IReadOnlyList<TvShow> shows)
        {
            TvShow show = shows[_random.Next(shows.Count)];
            Season season = show.Seasons[_random.Next(show.Seasons.Length)];
            return season.Episodes[_random.Next(season.Episodes.Length)];
        }

        public static void StartHistoryWatch()
        {
            Mode = PlaybackMode.HistoryWatch;
        }

        /// <summary>Clear the mode back to Normal. Called when the player closes.</summary>
        public static void End()
        {
            Mode = PlaybackMode.Normal;
            CartoonShuffleQueue.Clear();
        }
    }
}
