using Serilog;
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
            Log.Information("PlaybackSession.StartCartoonShuffle: limit={Limit}, availableShows={ShowCount}",
                limit, availableShows?.Count ?? 0);
            if (availableShows == null || availableShows.Count == 0)
            {
                Log.Warning("PlaybackSession.StartCartoonShuffle: no shows available, queue will be empty");
                Mode = PlaybackMode.CartoonShuffle;
                CartoonShuffleQueue.Clear();
                CartoonShuffleLimit = 0;
                CartoonShuffleIndex = 0;
                return;
            }
            Mode = PlaybackMode.CartoonShuffle;
            CartoonShuffleQueue.Clear();
            CartoonShuffleLimit = limit;
            CartoonShuffleIndex = 0;
            int skipped = 0;
            for (int i = 0; i < limit; i++)
            {
                Episode? pick = PickRandomEpisode(availableShows);
                if (pick == null) { skipped++; continue; }
                CartoonShuffleQueue.Add(pick);
            }
            Log.Information("PlaybackSession.StartCartoonShuffle: queued {Queued} of {Limit} episodes ({Skipped} empty-show picks skipped)",
                CartoonShuffleQueue.Count, limit, skipped);
        }

        /// <summary>
        /// Walks down show -> season -> episode picking uniformly at random
        /// at each level. Note: not actually uniform over all episodes;
        /// shows with fewer episodes get over-represented relative to ones
        /// with more. Preserved from the original behavior.
        ///
        /// Returns null when the chosen show has no seasons or the chosen
        /// season has no episodes - callers must skip nulls.
        /// </summary>
        private static Episode? PickRandomEpisode(IReadOnlyList<TvShow> shows)
        {
            // Defensive bail-outs - the random.Next call would throw
            // ArgumentOutOfRangeException with no caller context if any of
            // the arrays are empty (e.g., a show with no seasons because
            // the scanner found no Season N folders, or a season with no
            // episodes because all files filtered out).
            TvShow show = shows[_random.Next(shows.Count)];
            if (show.Seasons == null || show.Seasons.Length == 0)
            {
                Log.Warning("PlaybackSession.PickRandomEpisode: show '{Name}' has no seasons", show.Name);
                return null;
            }
            Season season = show.Seasons[_random.Next(show.Seasons.Length)];
            if (season.Episodes == null || season.Episodes.Length == 0)
            {
                Log.Warning("PlaybackSession.PickRandomEpisode: show '{Name}' season {Sn} has no episodes",
                    show.Name, season.Id);
                return null;
            }
            return season.Episodes[_random.Next(season.Episodes.Length)];
        }

        public static void StartHistoryWatch()
        {
            Log.Information("PlaybackSession.StartHistoryWatch");
            Mode = PlaybackMode.HistoryWatch;
        }

        /// <summary>Clear the mode back to Normal. Called when the player closes.</summary>
        public static void End()
        {
            Log.Information("PlaybackSession.End: was {Mode}, queue cleared ({Queued} entries)",
                Mode, CartoonShuffleQueue.Count);
            Mode = PlaybackMode.Normal;
            CartoonShuffleQueue.Clear();
        }
    }
}
