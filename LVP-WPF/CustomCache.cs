using System;
using System.Collections.Generic;
using System.IO;

namespace LVP_WPF
{
    public class CustomCache
    {
        internal static void BuildTomAndJerryData(TvShow tvShow)
        {
            if (tvShow.Id != 0)
            {
                return;
            }

            string path = tvShow.Seasons[0].Episodes[0].Path;
            string[] pathParts = path.Split('\\');
            string root = "";

            int idx;
            switch (pathParts.Length)
            {
                case 6:
                    idx = 4;
                    break;
                case 8:
                    idx = 6;
                    break;
                default:
                    throw new Exception("Unrecognized # of path parts for custom cache");
            }

            for (int i = 0; i < idx; i++)
            {
                root += $"{pathParts[i]}\\";
            }

            tvShow.Overview = "Tom and Jerry is an American animated media franchise and series of comedy short films created in 1940 by William Hanna and Joseph Barbera. Best known for its 161 theatrical short films by Metro-Goldwyn-Mayer, the series centers on the rivalry between the titular characters of a cat named Tom and a mouse named Jerry. Many shorts also feature several recurring characters.";
            tvShow.Date = DateTime.Parse("1940-02-10T00:00:00");
            tvShow.RunningTime = 12;
            tvShow.Poster = $"{root}poster.jpg";
            tvShow.Backdrop = $"{root}backdrop.jpg";
            tvShow.Id = 1;
            tvShow.Cartoon = true;

            bool skipHeader = true;
            string filmography = $"{root}filmography.csv";
            List<int> ids = new List<int>();
            List<string> titles = new List<string>();
            List<string> dates = new List<string>();
            List<string> overviews = new List<string>();

            using (StreamReader reader = new StreamReader(filmography, System.Text.Encoding.GetEncoding("iso-8859-1")))
            {
                while (!reader.EndOfStream)
                {
                    string? row = reader.ReadLine();
                    if (row == null)
                    {
                        continue;
                    }
                    if (skipHeader) //#;Prod.Num.;Title;Date;Summary
                    {
                        skipHeader = false;
                        continue;
                    }
                    string[] values = row.Split(';');
                    ids.Add(Int32.Parse(values[0]));
                    titles.Add(values[2]);
                    dates.Add(values[3]);
                    overviews.Add(values[4]);
                }
            }

            int index = 0;
            foreach (Season season in tvShow.Seasons)
            {
                foreach (Episode episode in season.Episodes)
                {
                    MainWindow.gui.ProgressBarValue++;
                    int id = ids[index];
                    string title = titles[index];
                    string date = dates[index];
                    string overview = overviews[index];

                    if (String.Compare(episode.Name, title, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreSymbols) != 0)
                    {
                        throw new Exception($"Episode name does not match, season {season.Id} episode: {episode.Name}. Should be {title}");
                    }

                    episode.Id = id;
                    episode.Overview = overview;
                    episode.Date = DateTime.Parse(date);
                    //To-do: libvlc screen snip
                    //episode.Backdrop
                    index++;
                }
            }
        }

        internal static void BuildLooneyTunesData(TvShow tvShow)
        {
            if (tvShow.Id != 0)
            {
                return;
            }
            // C: 1 Media 2 tv 3 Looney Tunes % S01-S24.Golden.Collection.DVDRip.x264-JCH[rartv] 4 Season 1 5 episode.mkv
            
            string path = tvShow.Seasons[0].Episodes[0].Path;
            string[] pathParts = path.Split('\\');
            string root = "";

            int idx;
            switch (pathParts.Length)
            {
                case 6:
                    idx = 4;
                    break;
                case 8:
                    idx = 6;
                    break;
                default:
                    throw new Exception("Unrecognized # of path parts for custom cache");
            }

            for (int i = 0; i < idx; i++)
            {
                root += $"{pathParts[i]}\\";
            }

            tvShow.Overview = "The Golden Collection series was launched following the success of the Walt Disney Treasures series which collected archived Disney material. These collections were made possible after the merger of Time Warner and Turner Broadcasting System, along with the subsequent transfer of video rights to the Turner library from MGM Home Entertainment to Warner Home Video. The cartoons included on the set are uncut, unedited, uncensored and digitally restored and remastered from the original black & white and successive exposure Technicolor film negatives (in the case of the Cinecolor shorts, the Technicolor reprints). However, some of the cartoons in these collections are derived from the \"Blue Ribbon\" reissues, as the original titles for these cartoons are presumably lost.";
            tvShow.Date = DateTime.Parse("1946-02-02T00:00:00");
            tvShow.RunningTime = 12;
            tvShow.Poster = $"{root}poster.jpg";
            tvShow.Backdrop = $"{root}backdrop.jpg";
            tvShow.Id = 2;
            tvShow.Cartoon = true;

            bool skipHeader = true;
            string filmography = $"{root}filmography.csv";
            List<int> ids = new List<int>();
            List<string> titles = new List<string>();
            List<string> dates = new List<string>();

            using (StreamReader reader = new StreamReader(filmography, System.Text.Encoding.GetEncoding("iso-8859-1")))
            {
                while (!reader.EndOfStream)
                {
                    string? row = reader.ReadLine();
                    if (row == null)
                    {
                        continue;
                    }
                    if (skipHeader) //#;Prod.Num.;Title;Date;Summary
                    {
                        skipHeader = false;
                        continue;
                    }

                    string[] values = row.Split(';');
                    ids.Add(Int32.Parse(values[0]));
                    titles.Add(values[1]);
                    dates.Add(values[2]);
                }
            }

            int index = 0;
            foreach (Season season in tvShow.Seasons)
            {
                foreach (Episode episode in season.Episodes)
                {
                    MainWindow.gui.ProgressBarValue++;
                    int id = ids[index];
                    string title = titles[index];
                    string date = dates[index];
                    if (String.Compare(episode.Name, title, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreSymbols) != 0)
                    {
                        throw new Exception($"Episode name does not match, season {season.Id} episode: {episode.Name}. Should be {title}");
                    }
                    episode.Id = id;
                    episode.Date = DateTime.Parse(date);
                    //episode.Backdrop
                    index++;
                }
            }
        }
    }
}
