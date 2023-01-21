using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace LVP_WPF
{
    public class MainModel
    {
        private Movie[] movies;
        private TvShow[] tvShows;
        private Dictionary<int, Movie> movieDict;
        private Dictionary<int, TvShow> tvShowDict;
        private Dictionary<int, Media> mediaDict;

        public MainModel(int m, int s)
        {
            movies = new Movie[m];
            movieDict = new Dictionary<int, Movie>();
            tvShows = new TvShow[s];
            tvShowDict = new Dictionary<int, TvShow>();
            mediaDict = new Dictionary<int, Media>();
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

        public Dictionary<int, Movie> MovieDict
        {
            get => movieDict;
            set => movieDict = value;
        }

        public Dictionary <int, TvShow> TvShowDict
        {
            get => tvShowDict;
            set => tvShowDict = value;
        }

        public Dictionary <int, Media> MediaDict
        {
            get => mediaDict;
            set => mediaDict = value;
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
                        this.movies[j].Subtitles = prevMedia.movies[i].Subtitles;
                        this.movies[j].SubtitleTrack = prevMedia.movies[i].SubtitleTrack;
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
                    else
                    {
                        continue;
                    }
                }
            }
        }
    }

    //To-do: move common vars into media class
    public class Media
    {
        private int id;

        public int Id
        {
            get => id;
            set => id = value;
        }
    }

    public class Movie : Media
    {
        //private int id;
        private string name;
        private string path;
        private string poster;
        private string backdrop;
        private string overview;
        DateTime? date;
        private int runningTime;
        private bool subtitles;
        private int subtitleTrack;

        public Movie(string n, string p)
        {
            name = n;
            path = p;
            subtitles = false;
            subtitleTrack = 0;
        }

        /*public int Id
        {
            get => id;
            set => id = value;
        }*/

        public string Name
        {
            get => name;
            set => name = value;
        }

        public string Path
        {
            get => path;
            set => path = value;
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

        public bool Subtitles
        {
            get => subtitles;
            set => subtitles = value;
        }

        public int SubtitleTrack
        {
            get => subtitleTrack;
            set => subtitleTrack = value;
        }

        internal bool Compare(Movie localMovie)
        {
            if (!this.name.Equals(localMovie.Name)) return false;
            if (!this.path.Equals(localMovie.Path)) return false;

            return true;
        }

        public static IComparer SortMoviesAlphabetically()
        {
            return (IComparer)new SortMoviesAlphabeticallyHelper();
        }

        private class SortMoviesAlphabeticallyHelper : IComparer
        {
            int IComparer.Compare(object? a, object? b)
            {
                Movie m1 = (Movie)a;
                Movie m2 = (Movie)b;
                return String.Compare(m1.Name, m2.name);
            }
        }
    }

    public class TvShow : Media
    {
        private bool cartoon;
        //private int id;
        private string name;
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
            name = n;
            currSeason = 1;
            lastWatched = null;
            cartoon = false;
        }

        public bool Cartoon
        {
            get => cartoon;
            set => cartoon = value;
        }

        /*public int Id
        {
            get => id;
            set => id = value;
        }*/

        public string Name
        {
            get => name;
            set => name = value;
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

        internal bool Compare(TvShow localShow)
        {
            if (!this.name.Equals(localShow.name)) return false;
            if (this.seasons.Length != localShow.seasons.Length) return false;
            for (int i = 0; i < this.seasons.Length; i++)
            {
                if (!this.seasons[i].Compare(localShow.seasons[i])) return false;
            }
            return true;
        }

        public static IComparer SortTvShowsAlphabetically()
        {
            return (IComparer)new SortTvShowsAlphabeticallyHelper();
        }

        private class SortTvShowsAlphabeticallyHelper : IComparer
        {
            int IComparer.Compare(object a, object b)
            {
                TvShow t1 = (TvShow)a;
                TvShow t2 = (TvShow)b;
                return String.Compare(t1.Name, t2.name);
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

    public class Episode
    {
        private bool multiEpisode = false;
        private int id;
        private string name;
        private string backdrop;
        private string overview;
        private string path;
        DateTime? date;
        private long savedTime;
        private long length;

        public Episode(int i, string n, string p, bool me = false)
        {
            id = i;
            name = n;
            path = p;
            savedTime = 0;
            multiEpisode = me;
        }

        public int Id
        {
            get => id;
            set => id = value;
        }

        public string Name
        {
            get => name;
            set => name = value;
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

        public string Path
        {
            get => path;
            set => path = value;
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

        internal bool Compare(Episode otherEpisode)
        {
            if (!this.name.Equals(otherEpisode.name)) return false;
            return true;
        }
    }
}
