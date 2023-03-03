using System;
using System.Collections;
using System.Collections.Generic;

namespace LVP_WPF
{
    public class MainModel
    {
        private Movie[] movies;
        private TvShow[] tvShows;

        public MainModel(int m, int s)
        {
            movies = new Movie[m];
            tvShows = new TvShow[s];
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

        internal bool Compare(MainModel prevMedia)
        {
            Array.Sort(this.Movies, Movie.SortMoviesAlphabetically());
            Array.Sort(this.TvShows, TvShow.SortTvShowsAlphabetically());

            if (this.movies.Length != prevMedia.movies.Length) return false;
            if (this.tvShows.Length != prevMedia.tvShows.Length) return false;

            for (int i = 0; i < this.movies.Length; i++)
            {
                if (!this.movies[i].Compare(prevMedia.movies[i])) return false;
            }

            for (int i = 0; i < this.tvShows.Length; i++)
            {
                if (!this.tvShows[i].Compare(prevMedia.tvShows[i])) return false;
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
                    }
                    else
                    {
                        continue;
                    }
                }
            }
            //To-do* test ingest
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
                        this.tvShows[l].MultiLang = prevMedia.tvShows[i].MultiLang;
                        this.tvShows[l].MultiLangCurrSeason = prevMedia.tvShows[i].MultiLangCurrSeason;
                        this.tvShows[l].MultiLangOverview = prevMedia.tvShows[i].MultiLangOverview;
                        this.tvShows[l].MultiLangName = prevMedia.tvShows[i].MultiLangName;
                        this.tvShows[i].MultiLangLastWatched = prevMedia.tvShows[i].MultiLangLastWatched;
                        IngestSeason(prevMedia, i, l);
                        for (int a = 0; a < prevMedia.TvShows[i].MultiLangSeasons.Count; a++)
                        {
                            IngestSeasonMultiLang(prevMedia, i, l, a);
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
                this.tvShows[l].CurrSeason = prevMedia.TvShows[i].CurrSeason;

                for (int k = 0; k < prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes.Length; k++)
                {
                    if (this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].Name.Equals(prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].Name))
                    {
                        this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].Id = prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].Id;
                        this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].Name = prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].Name;
                        this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].Backdrop = prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].Backdrop;
                        this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].Date = prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].Date;
                        this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].Overview = prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].Overview;
                        this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].Path = prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].Path;
                        this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].SavedTime = prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].SavedTime;
                        this.tvShows[l].MultiLangSeasons[a][j].Episodes[k].Length = prevMedia.TvShows[i].MultiLangSeasons[a][j].Episodes[k].Length;
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
                    }
                }
            }
        }
    }

    //To-do: move common vars into media class
    public class Media
    {
        private int id;
        private string path;
        //To-do: change to auto property
        public string Name { get; set; }

        public int Id
        {
            get => id;
            set => id = value;
        }

        public string Path
        {
            get => path;
            set => path = value;
        }
    }

    public class Movie : Media
    {
        private string poster;
        private string backdrop;
        private string overview;
        DateTime? date;
        private int runningTime;

        public Movie(string n, string p)
        {
            Name = n;
            Path = p;
        }

        public string Backdrop
        {
            get => backdrop;
            set => backdrop = value;
        }

        public string Poster
        {
            get => poster;
            set => poster = value;
        }

        public string Overview
        {
            get => overview;
            set => overview = value;
        }

        public DateTime? Date
        {
            get => date;
            set => date = value;
        }

        public int RunningTime
        {
            get => runningTime;
            set => runningTime = value;
        }

        internal bool Compare(Movie localMovie)
        {
            if (!this.Name.Equals(localMovie.Name)) return false;
            if (!this.Path.Equals(localMovie.Path)) return false;
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
        private bool cartoon;
        private string overview;
        private string backdrop;
        private string poster;
        private Season[] seasons;
        private DateTime? date;
        private int currSeason;
        private int runningTime;
        Episode lastWatched;

        public TvShow(string n)
        {
            Name = n;
            currSeason = 1;
            lastWatched = null;
            cartoon = false;
            MultiLang = false;
        }

        public bool Cartoon
        {
            get => cartoon;
            set => cartoon = value;
        }

        public string Backdrop
        {
            get => backdrop;
            set => backdrop = value;
        }

        public string Poster
        {
            get => poster;
            set => poster = value;
        }

        public string Overview
        {
            get => overview;
            set => overview = value;
        }

        public DateTime? Date
        {
            get => date;
            set => date = value;
        }

        public int CurrSeason
        {
            get => currSeason;
            set => currSeason = value;
        }

        public Season[] Seasons
        {
            get => seasons;
            set => seasons = value;
        }

        public Episode LastEpisode
        {
            get => lastWatched;
            set => lastWatched = value;
        }
        public int RunningTime
        {
            get => runningTime;
            set => runningTime = value;
        }

        public bool MultiLang { get; set; }
        public List<string> MultiLangName { get; set; }
        public List<string> MultiLangOverview { get; set; }
        public List<Season[]> MultiLangSeasons { get; set; }
        public List<int> MultiLangCurrSeason { get; set; }
        public List<Episode> MultiLangLastWatched { get; set; }

        //To-do** test compare
        internal bool Compare(TvShow localShow)
        {
            if (!this.Name.Equals(localShow.Name)) return false;
         
            if (this.seasons.Length != localShow.seasons.Length) return false;
            for (int i = 0; i < this.seasons.Length; i++)
            {
                if (!this.seasons[i].Compare(localShow.seasons[i])) return false;
            }
            
            if (this.MultiLangName.Count != localShow.MultiLangName.Count) return false;
            for (int i = 0; i < this.MultiLangName.Count; i++)
            {
                if (!this.MultiLangName[i].Equals(localShow.MultiLangName[i])) return false;
            }
            
            if (this.MultiLangSeasons.Count != localShow.MultiLangSeasons.Count) return false;

            for (int i = 0; i < this.MultiLangSeasons.Count; i++)
            {
                Season[] a = this.MultiLangSeasons[i];
                Season[] b = localShow.MultiLangSeasons[i];
                if (a.Length!= b.Length) return false;
                for (int j = 0; j < a.Length; j++)
                {
                    if (!a[j].Compare(b[j])) return false;
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
            int IComparer.Compare(object a, object b)
            {
                TvShow t1 = (TvShow)a;
                TvShow t2 = (TvShow)b;
                return String.Compare(t1.Name, t2.Name);
            }
        }
    }

    public class Season
    {
        private int id;
        private Episode[] episodes;
        private string poster;
        DateTime? date;

        public Season(int i)
        {
            id = i;
        }

        public int Id
        {
            get => id;
            set => id = value;
        }

        public string Poster
        {
            get => poster;
            set => poster = value;
        }

        public DateTime? Date
        {
            get => date;
            set => date = value;
        }

        public Episode[] Episodes
        {
            get => episodes;
            set => episodes = value;
        }

        internal bool Compare(Season localSeason)
        {
            if (this.episodes.Length != localSeason.episodes.Length) return false;

            for (int i = 0; i < this.episodes.Length; i++)
            {
                if (!this.episodes[i].Compare(localSeason.episodes[i])) return false;
            }
            return true;
        }
    }

    public class Episode : Media
    {
        private bool multiEpisode = false;
        private string backdrop;
        private string overview;
        DateTime? date;
        private long savedTime;
        private long length;

        public Episode(int i, string n, string p, bool me = false)
        {
            Id = i;
            Name = n;
            Path = p;
            savedTime = 0;
            multiEpisode = me;
        }

        public string Backdrop
        {
            get => backdrop;
            set => backdrop = value;
        }

        public string Overview
        {
            get => overview;
            set => overview = value;
        }
        public DateTime? Date
        {
            get => date;
            set => date = value;
        }

        public long SavedTime
        {
            get => savedTime;
            set => savedTime = value;
        }

        public long Length
        {
            get => length;
            set => length = value;
        }

        public bool MultiEpisode
        {
            get => multiEpisode;
            set => multiEpisode = value;
        }

        internal bool Compare(Episode otherEpisode)
        {
            if (!this.Name.Equals(otherEpisode.Name)) return false;
            return true;
        }
    }
}
