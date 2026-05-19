using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace LVP_WPF
{
    public class MainModel
    {
        private Movie[] movies;
        private TvShow[] tvShows;
        internal List<Episode> historyList;

        public MainModel(int m, int s)
        {
            movies = new Movie[m];
            tvShows = new TvShow[s];
            historyList = new List<Episode>();
        }

        public Movie[] Movies
        {
            get => movies;
            set => movies = value;
        }

        public TvShow[] TvShows
        {
            get => tvShows;
            set => tvShows = value;
        }

        public List<Episode> HistoryList
        {
            get => historyList;
            set => historyList = value;
        }

        public int HistoryIndex { get; set; }
        public DateTime HistoryMin { get; set; }
        public DateTime HistoryMax { get; set; }
        public Episode HistoryEpisode { get; set; }

        internal bool Compare(MainModel prevMedia)
        {
            Array.Sort(this.Movies, Movie.SortMoviesAlphabetically());
            Array.Sort(this.TvShows, TvShow.SortTvShowsAlphabetically());

            if (this.movies.Length != prevMedia.movies.Length)
            {
                return false;
            }

            if (this.tvShows.Length != prevMedia.tvShows.Length)
            {
                return false;
            }

            for (int i = 0; i < this.movies.Length; i++)
            {
                if (!this.movies[i].Compare(prevMedia.movies[i]))
                {
                    return false;
                }

            }

            for (int i = 0; i < this.tvShows.Length; i++)
            {
                if (!this.tvShows[i].Compare(prevMedia.tvShows[i]))
                {
                    return false;
                }

            }

            return true;
        }

        // ----- Ingest -----
        //
        // After a fresh scan, copy over the TMDB-enrichment data (Ids, posters,
        // overviews, saved playback positions, etc.) from the persisted model
        // to the freshly scanned one, matching by file path.
        //
        // This used to be one big O(n^2) method with copy-paste property
        // assignments triplicated across Movie/TvShow/Season/Episode and a
        // separate MultiLang variant. The per-class field lists now live on
        // each class as CopyFrom helpers; this method just wires the matches.

        internal void Ingest(MainModel prevMedia)
        {
            Dictionary<string, Movie> prevMoviesByPath = prevMedia.movies.ToDictionary(m => m.Path);
            foreach (Movie curr in this.movies)
            {
                if (prevMoviesByPath.TryGetValue(curr.Path, out Movie? prev))
                {
                    curr.CopyFrom(prev);
                }
            }

            Dictionary<string, TvShow> prevShowsByPath = prevMedia.tvShows.ToDictionary(t => t.Path);
            foreach (TvShow curr in this.tvShows)
            {
                if (!prevShowsByPath.TryGetValue(curr.Path, out TvShow? prev))
                {
                    continue;
                }

                curr.CopyFrom(prev);
                IngestSeasonsByIndex(curr.Seasons, prev.Seasons, includeTranslated: false);

                if (curr.MultiLang)
                {
                    curr.CopyMultiLangFrom(prev);
                    for (int a = 0; a < prev.MultiLangSeasons.Count; a++)
                    {
                        IngestSeasonsByIndex(curr.MultiLangSeasons[a], prev.MultiLangSeasons[a], includeTranslated: true);
                    }
                }
            }
        }

        // Position-indexed season/episode ingest. Episodes must line up by
        // index (the scanner and the saved JSON both sort by the %N% prefix),
        // and a safety check skips mismatches if an episode was added/removed
        // in the middle - that's the original behavior, preserved here.
        // The matching key differs for multi-lang (file name) vs single-lang
        // (episode name); both are still index-based.
        private static void IngestSeasonsByIndex(Season[] currSeasons, Season[] prevSeasons, bool includeTranslated)
        {
            int seasonCount = Math.Min(currSeasons.Length, prevSeasons.Length);
            for (int j = 0; j < seasonCount; j++)
            {
                currSeasons[j].CopyFrom(prevSeasons[j]);

                Episode[] currEps = currSeasons[j].Episodes;
                Episode[] prevEps = prevSeasons[j].Episodes;
                int epCount = Math.Min(currEps.Length, prevEps.Length);
                for (int k = 0; k < epCount; k++)
                {
                    if (includeTranslated)
                    {
                        if (EpisodeFileNamesMatch(currEps[k].Path, prevEps[k].Path))
                        {
                            currEps[k].CopyFrom(prevEps[k], includeTranslated: true);
                        }
                    }
                    else
                    {
                        if (currEps[k].Name.Equals(prevEps[k].Name))
                        {
                            currEps[k].CopyFrom(prevEps[k], includeTranslated: false);
                        }
                    }
                }
            }
        }

        private static bool EpisodeFileNamesMatch(string currPath, string prevPath)
        {
            string currFile = currPath.Substring(currPath.LastIndexOf("\\"));
            string prevFile = prevPath.Substring(prevPath.LastIndexOf("\\"));
            return currFile.Equals(prevFile);
        }
    }

    public class Media
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
    }

    public class Movie : Media
    {
        public Movie(string n, string p)
        {
            Name = n;
            Path = p;
        }

        public string Backdrop { get; set; }
        public string Poster { get; set; }
        public string Overview { get; set; }
        public DateTime? Date { get; set; }
        public int RunningTime { get; set; }

        internal bool Compare(Movie localMovie)
        {
            return this.Path.Equals(localMovie.Path);
        }

        /// <summary>Copy the TMDB-enrichment fields from <paramref name="other"/> onto this Movie.</summary>
        internal void CopyFrom(Movie other)
        {
            Name = other.Name;
            Overview = other.Overview;
            Path = other.Path;
            Poster = other.Poster;
            Id = other.Id;
            Date = other.Date;
            Backdrop = other.Backdrop;
            RunningTime = other.RunningTime;
        }

        public static IComparer SortMoviesAlphabetically()
        {
            return new SortMoviesAlphabeticallyHelper();
        }

        private class SortMoviesAlphabeticallyHelper : IComparer
        {
            int IComparer.Compare(object? a, object? b)
            {
                Movie m1 = (Movie)a;
                Movie m2 = (Movie)b;
                return String.Compare(m1.Name, m2.Name);
            }
        }
    }


    public class TvShow : Media
    {
        public TvShow(string n, string p)
        {
            Name = n;
            CurrSeason = 1;
            Cartoon = false;
            MultiLang = false;
            Path = p;
        }

        public bool Cartoon { get; set; }
        public string Backdrop { get; set; }
        public string Poster { get; set; }
        public string Overview { get; set; }
        public DateTime? Date { get; set; }
        public int CurrSeason { get; set; }
        public Season[] Seasons { get; set; }
        public Episode LastEpisode { get; set; }
        public int RunningTime { get; set; }
        public bool MultiLang { get; set; }
        public List<string>? MultiLangName { get; set; }
        public List<string>? MultiLangOverview { get; set; }
        public List<Season[]>? MultiLangSeasons { get; set; }
        public List<int>? MultiLangCurrSeason { get; set; }
        public List<Episode>? MultiLangLastWatched { get; set; }

        /// <summary>
        /// Copy the top-level TvShow fields (not the Seasons array - that's
        /// done index-by-index by IngestSeasonsByIndex) from <paramref name="other"/>.
        /// </summary>
        internal void CopyFrom(TvShow other)
        {
            Name = other.Name;
            Cartoon = other.Cartoon;
            Id = other.Id;
            Overview = other.Overview;
            Poster = other.Poster;
            Date = other.Date;
            Backdrop = other.Backdrop;
            CurrSeason = other.CurrSeason;
            LastEpisode = other.LastEpisode;
            RunningTime = other.RunningTime;
        }

        /// <summary>
        /// Copy the multi-language metadata lists (names, overviews, last-watched
        /// pointers per language). MultiLangSeasons themselves are handled
        /// separately via IngestSeasonsByIndex per language.
        /// </summary>
        internal void CopyMultiLangFrom(TvShow other)
        {
            MultiLangCurrSeason = other.MultiLangCurrSeason;
            MultiLangOverview = other.MultiLangOverview;
            MultiLangName = other.MultiLangName;
            MultiLangLastWatched = other.MultiLangLastWatched;
        }

        internal bool Compare(TvShow localShow)
        {
            if (!this.Path.Equals(localShow.Path))
            {
                return false;
            }


            if (this.MultiLang)
            {
                if (this.MultiLangName.Count != localShow.MultiLangName.Count)
                {
                    return false;
                }

                for (int i = 0; i < this.MultiLangName.Count; i++)
                {
                    if (!this.MultiLangName[i].Split(" (")[0].Equals(localShow.MultiLangName[i].Split(" (")[0]))
                    {
                        return false;
                    }

                }

                if (this.MultiLangSeasons.Count != localShow.MultiLangSeasons.Count)
                {
                    return false;
                }

                for (int i = 0; i < this.MultiLangSeasons.Count; i++)
                {
                    Season[] a = this.MultiLangSeasons[i];
                    Season[] b = localShow.MultiLangSeasons[i];
                    if (a.Length != b.Length)
                    {
                        return false;
                    }

                    for (int j = 0; j < a.Length; j++)
                    {
                        if (a[j].Episodes.Length != b[j].Episodes.Length)
                        {
                            return false;
                        }

                        for (int k = 0; k < a[j].Episodes.Length; k++)
                        {
                            Episode c = a[j].Episodes[k];
                            Episode d = b[j].Episodes[k];
                            if (!c.Path.Equals(d.Path))
                            {
                                return false;
                            }

                        }
                    }
                }
                return true;
            }

            if (this.Seasons.Length != localShow.Seasons.Length)
            {
                return false;
            }

            for (int i = 0; i < this.Seasons.Length; i++)
            {
                if (!this.Seasons[i].Compare(localShow.Seasons[i]))
                {
                    return false;
                }

            }

            return true;
        }

        public static IComparer SortTvShowsAlphabetically()
        {
            return new SortTvShowsAlphabeticallyHelper();
        }

        private class SortTvShowsAlphabeticallyHelper : IComparer
        {
            int IComparer.Compare(object? a, object? b)
            {
                TvShow? t1 = (TvShow?)a;
                TvShow? t2 = (TvShow?)b;
                if (t1 != null && t2 != null)
                {
                    return String.Compare(t1.Name, t2.Name);
                }
                else throw new ArgumentNullException(nameof(a));
            }
        }
    }

    public class Season
    {
        public Season(int i)
        {
            Id = i;
        }

        public int Id { get; set; }
        public string Poster { get; set; }
        public DateTime Date { get; set; }
        public Episode[] Episodes { get; set; }

        /// <summary>Copy season metadata (Id/Poster/Date). Episodes handled separately.</summary>
        internal void CopyFrom(Season other)
        {
            Id = other.Id;
            Poster = other.Poster;
            Date = other.Date;
        }

        internal bool Compare(Season localSeason)
        {
            if (this.Episodes.Length != localSeason.Episodes.Length)
            {
                return false;
            }

            for (int i = 0; i < this.Episodes.Length; i++)
            {
                if (!this.Episodes[i].Compare(localSeason.Episodes[i]))
                {
                    return false;
                }

            }
            return true;
        }
    }

    public class Episode : Media
    {
        public Episode(int i, string n, string p, bool me = false)
        {
            Id = i;
            Name = n;
            Path = p;
            SavedTime = 0;
            MultiEpisode = me;
        }

        public bool Translated { get; set; }
        public string Backdrop { get; set; }
        public string Overview { get; set; }
        public DateTime Date { get; set; }
        public long SavedTime { get; set; }
        public long Length { get; set; }
        public bool MultiEpisode { get; set; }

        internal bool Compare(Episode otherEpisode)
        {
            return this.Path.Equals(otherEpisode.Path);
        }

        /// <summary>
        /// Copy episode metadata + playback state. Translated is opt-in
        /// because it only applies to the multi-lang ingest path.
        /// </summary>
        internal void CopyFrom(Episode other, bool includeTranslated)
        {
            Id = other.Id;
            Name = other.Name;
            Backdrop = other.Backdrop;
            Date = other.Date;
            Overview = other.Overview;
            Path = other.Path;
            SavedTime = other.SavedTime;
            Length = other.Length;
            if (includeTranslated)
            {
                Translated = other.Translated;
            }
        }
    }
}
