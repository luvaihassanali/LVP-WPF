using System;
using System.Collections;
using System.Collections.Generic;
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


            //Compare by ID... or create GUID? make cache faster... multiple tasks??
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

        internal void Ingest(MainModel prevMedia)
        {
            for (int i = 0; i < prevMedia.Movies.Length; i++)
            {
                for (int j = 0; j < this.movies.Length; j++)
                {
                    if (String.Compare(this.movies[j].Name, prevMedia.movies[i].Name, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreSymbols) == 0)
                    {
                        this.movies[j].Name = prevMedia.movies[i].Name;
                        this.movies[j].Overview = prevMedia.movies[i].Overview;
                        this.movies[j].Path = prevMedia.movies[i].Path;
                        this.movies[j].Poster = prevMedia.movies[i].Poster;
                        this.movies[j].Id = prevMedia.movies[i].Id;
                        this.movies[j].Date = prevMedia.movies[i].Date;
                        this.movies[j].Backdrop = prevMedia.movies[i].Backdrop;
                        this.movies[j].RunningTime = prevMedia.movies[i].RunningTime;
                        //Cache.mediaCount--;
                    }
                    else
                    {
                        continue;
                    }
                }
            }

            for (int i = 0; i < prevMedia.TvShows.Length; i++)
            {
                for (int l = 0; l < this.tvShows.Length; l++)
                {
                    if (String.Compare(this.tvShows[l].Name, prevMedia.tvShows[i].Name, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreSymbols) == 0)
                    {
                        this.tvShows[l].Name = prevMedia.tvShows[i].Name;
                        this.tvShows[l].Cartoon = prevMedia.tvShows[i].Cartoon;
                        this.tvShows[l].Id = prevMedia.tvShows[i].Id;
                        this.tvShows[l].Overview = prevMedia.tvShows[i].Overview;
                        this.tvShows[l].Poster = prevMedia.tvShows[i].Poster;
                        this.tvShows[l].Date = prevMedia.tvShows[i].Date;
                        this.tvShows[l].Backdrop = prevMedia.tvShows[i].Backdrop;
                        this.tvShows[l].CurrSeason = prevMedia.tvShows[i].CurrSeason;
                        this.tvShows[l].LastEpisode = prevMedia.tvShows[i].LastEpisode;
                        this.tvShows[l].RunningTime = prevMedia.tvShows[i].RunningTime;
                        IngestSeason(prevMedia, i, l);

                        if (this.tvShows[l].MultiLang)
                        {
                            this.tvShows[l].MultiLangCurrSeason = prevMedia.tvShows[i].MultiLangCurrSeason;
                            this.tvShows[l].MultiLangOverview = prevMedia.tvShows[i].MultiLangOverview;
                            this.tvShows[l].MultiLangName = prevMedia.tvShows[i].MultiLangName;
                            this.tvShows[l].MultiLangLastWatched = prevMedia.tvShows[i].MultiLangLastWatched;

                            for (int a = 0; a < prevMedia.tvShows[i].MultiLangSeasons.Count; a++)
                            {
                                IngestSeasonMultiLang(prevMedia, i, l, a);
                            }
                        }
                    }
                    else
                    {
                        continue;
                    }
                }
            }
        }

        internal void IngestSeasonMultiLang(MainModel prevMedia, int i, int l, int a)
        {
            for (int j = 0; j < prevMedia.TvShows[i].MultiLangSeasons[a].Length; j++)
            {
                this.tvShows[l].MultiLangSeasons[a][j].Id = prevMedia.TvShows[i].MultiLangSeasons[a][j].Id;
                this.tvShows[l].MultiLangSeasons[a][j].Poster = prevMedia.TvShows[i].MultiLangSeasons[a][j].Poster;
                this.tvShows[l].MultiLangSeasons[a][j].Date = prevMedia.TvShows[i].MultiLangSeasons[a][j].Date;

                for (int k = 0; k < prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes.Length; k++)
                {
                    string currFilePath = this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].Path;
                    string prevFilePath = prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].Path;
                    int currIdx = currFilePath.LastIndexOf("\\");
                    int prevIdx = prevFilePath.LastIndexOf("\\");
                    string currFileName = currFilePath.Substring(currIdx);
                    string prevFileName = prevFilePath.Substring(prevIdx);

                    if (currFileName.Equals(prevFileName))
                    {
                        this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].Id = prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].Id;
                        this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].Name = prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].Name;
                        this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].Backdrop = prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].Backdrop;
                        this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].Date = prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].Date;
                        this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].Overview = prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].Overview;
                        this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].Path = prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].Path;
                        this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].SavedTime = prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].SavedTime;
                        this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].Length = prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].Length;
                        this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].Translated = prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].Translated;
                        //Cache.mediaCount -= 2;
                    }
                }
            }
        }

        internal void IngestSeason(MainModel prevMedia, int i, int l)
        {
            for (int j = 0; j < prevMedia.TvShows[i].Seasons.Length; j++)
            {
                this.tvShows[l].Seasons[j].Id = prevMedia.TvShows[i].Seasons[j].Id;
                this.tvShows[l].Seasons[j].Poster = prevMedia.TvShows[i].Seasons[j].Poster;
                this.tvShows[l].Seasons[j].Date = prevMedia.TvShows[i].Seasons[j].Date;
                this.tvShows[l].CurrSeason = prevMedia.TvShows[i].CurrSeason;

                for (int k = 0; k < prevMedia.TvShows[i].Seasons[j].Episodes.Length; k++)
                {
                    if (this.tvShows[l].Seasons[j].Episodes[k].Name.Equals(prevMedia.TvShows[i].Seasons[j].Episodes[k].Name))
                    {
                        this.tvShows[l].Seasons[j].Episodes[k].Id = prevMedia.TvShows[i].Seasons[j].Episodes[k].Id;
                        this.tvShows[l].Seasons[j].Episodes[k].Name = prevMedia.TvShows[i].Seasons[j].Episodes[k].Name;
                        this.tvShows[l].Seasons[j].Episodes[k].Backdrop = prevMedia.TvShows[i].Seasons[j].Episodes[k].Backdrop;
                        this.tvShows[l].Seasons[j].Episodes[k].Date = prevMedia.TvShows[i].Seasons[j].Episodes[k].Date;
                        this.tvShows[l].Seasons[j].Episodes[k].Overview = prevMedia.TvShows[i].Seasons[j].Episodes[k].Overview;
                        this.tvShows[l].Seasons[j].Episodes[k].Path = prevMedia.TvShows[i].Seasons[j].Episodes[k].Path;
                        this.tvShows[l].Seasons[j].Episodes[k].SavedTime = prevMedia.TvShows[i].Seasons[j].Episodes[k].SavedTime;
                        this.tvShows[l].Seasons[j].Episodes[k].Length = prevMedia.TvShows[i].Seasons[j].Episodes[k].Length;
                        //Cache.mediaCount--;
                    }
                }
            }
        }
    }

    public class Media
    {
        //To-do: move other common vars into Media class and update variables to auto property
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
            if (!this.Name.Equals(localMovie.Name))
            {
                return false;
            }

            if (!this.Path.Equals(localMovie.Path))
            {
                return false;
            }

            return true;
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
        public TvShow(string n)
        {
            Name = n;
            CurrSeason = 1;
            Cartoon = false;
            MultiLang = false;
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

        internal bool Compare(TvShow localShow)
        {
            if (!this.Name.Split(" (")[0].Equals(localShow.Name.Split(" (")[0]))
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


                // To-do: map seasons and compare each one. Adding/removing from multi lang tv show won't trigger update
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
            if (!this.Name.Equals(otherEpisode.Name))
            {
                return false;
            }

            if (!this.Path.Equals(otherEpisode.Path))
            {
                return false;
            }

            return true;
        }
    }
}
