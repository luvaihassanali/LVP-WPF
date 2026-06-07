using LVP_WPF.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LVP_WPF
{
    /// <summary>
    /// Hand-curated metadata for shows that aren't on TMDB. The show's
    /// directory has a filmography.csv shipped alongside the videos;
    /// episodes match the CSV row-by-row in scan order.
    /// </summary>
    public class CustomCache
    {
        internal static void BuildTomAndJerryData(TvShow tvShow) =>
            BuildCustomData(tvShow, TomAndJerryMetadata);

        internal static void BuildLooneyTunesData(TvShow tvShow) =>
            BuildCustomData(tvShow, LooneyTunesMetadata);

        // ---- per-show curated metadata ----

        private static readonly CustomShowMetadata TomAndJerryMetadata = new(
            Id: 1,
            Overview: "Tom and Jerry is an American animated media franchise and series of comedy short films created in 1940 by William Hanna and Joseph Barbera. Best known for its 161 theatrical short films by Metro-Goldwyn-Mayer, the series centers on the rivalry between the titular characters of a cat named Tom and a mouse named Jerry. Many shorts also feature several recurring characters.",
            Date: new DateTime(1940, 2, 10),
            RunningTime: 12,
            // filmography.csv columns: #;Prod.Num.;Title;Date;Summary
            CsvIdColumn: 0,
            CsvTitleColumn: 2,
            CsvDateColumn: 3,
            CsvOverviewColumn: 4);

        private static readonly CustomShowMetadata LooneyTunesMetadata = new(
            Id: 2,
            Overview: "The Golden Collection series was launched following the success of the Walt Disney Treasures series which collected archived Disney material. These collections were made possible after the merger of Time Warner and Turner Broadcasting System, along with the subsequent transfer of video rights to the Turner library from MGM Home Entertainment to Warner Home Video. The cartoons included on the set are uncut, unedited, uncensored and digitally restored and remastered from the original black & white and successive exposure Technicolor film negatives (in the case of the Cinecolor shorts, the Technicolor reprints). However, some of the cartoons in these collections are derived from the \"Blue Ribbon\" reissues, as the original titles for these cartoons are presumably lost.",
            Date: new DateTime(1946, 2, 2),
            RunningTime: 12,
            // filmography.csv columns: #;Title;Date  (no summary column)
            CsvIdColumn: 0,
            CsvTitleColumn: 1,
            CsvDateColumn: 2,
            CsvOverviewColumn: null);

        // ---- shared builder ----

        private record CustomShowMetadata(
            int Id,
            string Overview,
            DateTime Date,
            int RunningTime,
            int CsvIdColumn,
            int CsvTitleColumn,
            int CsvDateColumn,
            int? CsvOverviewColumn);

        private static void BuildCustomData(TvShow tvShow, CustomShowMetadata meta)
        {
            if (tvShow.Id != 0) return;

            // tvShow.Path is the show's root directory, which is where the
            // filmography.csv and poster/backdrop sit. (The old code rebuilt
            // this by counting how many "\\" segments the first episode's
            // path had - 6 in release, 8 in debug where the drive prefix is
            // longer - to figure out where to chop. tvShow.Path is the same
            // string either way.)
            string root = tvShow.Path + "\\";

            tvShow.Overview = meta.Overview;
            tvShow.Date = meta.Date;
            tvShow.RunningTime = meta.RunningTime;
            tvShow.Poster = $"{root}poster.jpg";
            tvShow.Backdrop = $"{root}backdrop.jpg";
            tvShow.Id = meta.Id;
            tvShow.Cartoon = true;

            List<int> ids = new();
            List<string> titles = new();
            List<string> dates = new();
            List<string?> overviews = new();
            ReadFilmographyCsv($"{root}filmography.csv", meta, ids, titles, dates, overviews);

            int index = 0;
            foreach (Season season in tvShow.Seasons)
            {
                foreach (Episode episode in season.Episodes)
                {
                    MainWindow.gui.ProgressBarValue++;

                    if (!episode.Name.MatchesLoosely(titles[index]))
                    {
                        throw new Exception($"Episode name does not match, season {season.Id} episode: {episode.Name}. Should be {titles[index]}");
                    }

                    episode.Id = ids[index];
                    episode.Date = DateTime.Parse(dates[index]);
                    if (overviews[index] != null)
                    {
                        episode.Overview = overviews[index]!;
                    }
                    //episode.Backdrop (libvlc screen snip)
                    index++;
                }
            }
        }

        private static void ReadFilmographyCsv(
            string filmographyPath,
            CustomShowMetadata meta,
            List<int> ids,
            List<string> titles,
            List<string> dates,
            List<string?> overviews)
        {
            bool skipHeader = true;
            using StreamReader reader = new StreamReader(filmographyPath, Encoding.GetEncoding("iso-8859-1"));
            while (!reader.EndOfStream)
            {
                string? row = reader.ReadLine();
                if (row == null) continue;
                if (skipHeader)
                {
                    skipHeader = false;
                    continue;
                }
                string[] values = row.Split(';');
                ids.Add(Int32.Parse(values[meta.CsvIdColumn]));
                titles.Add(values[meta.CsvTitleColumn]);
                dates.Add(values[meta.CsvDateColumn]);
                overviews.Add(meta.CsvOverviewColumn.HasValue ? values[meta.CsvOverviewColumn.Value] : null);
            }
        }
    }
}
