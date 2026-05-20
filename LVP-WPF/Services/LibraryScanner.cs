using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LVP_WPF.Services
{
    /// <summary>
    /// A pair of root directories to scan: the TV folder and the movie folder.
    /// </summary>
    internal sealed record LibraryRoot(string TvDirectory, string MovieDirectory);

    /// <summary>
    /// The output of a single scan: the fresh model, a media-item count for
    /// progress reporting, and any non-fatal warnings the caller should surface.
    /// </summary>
    internal sealed record ScanResult(MainModel Model, int MediaCount, IReadOnlyList<string> Warnings);

    /// <summary>
    /// Walks the configured media directories and produces a freshly-built
    /// MainModel (movies + tv shows + seasons + episodes), without any TMDB
    /// enrichment, file renaming, dialog interaction, or static state.
    /// One scanner instance == one scan.
    /// </summary>
    internal sealed class LibraryScanner
    {
        private readonly string[] _multiLangKeys;
        private readonly List<string> _warnings = new();
        private readonly List<string> _tvPaths = new();
        private readonly List<string> _moviePaths = new();
        private int _mediaCount;
        private int _extrasIdx = -1;

        public LibraryScanner(string[] multiLangKeys)
        {
            _multiLangKeys = multiLangKeys;
        }

        public ScanResult Scan(IEnumerable<LibraryRoot> roots)
        {
            foreach (LibraryRoot root in roots)
            {
                ProcessRoot(root);
            }

            MainModel model = new MainModel(_moviePaths.Count, _tvPaths.Count);

            for (int i = 0; i < _moviePaths.Count; i++)
            {
                model.Movies[i] = ProcessMovieDirectory(_moviePaths[i]);
                _mediaCount++;
            }

            for (int i = 0; i < _tvPaths.Count; i++)
            {
                model.TvShows[i] = ProcessTvDirectory(_tvPaths[i]);
            }

            return new ScanResult(model, _mediaCount, _warnings);
        }

        private void ProcessRoot(LibraryRoot root)
        {
            Log.Debug("Process root dir {Tv} / {Movie}", root.TvDirectory, root.MovieDirectory);

            if (!Directory.Exists(root.TvDirectory))
            {
                _warnings.Add($"TV folder at {root.TvDirectory} not found.");
            }
            if (!Directory.Exists(root.MovieDirectory))
            {
                _warnings.Add($"Movie folder on {root.MovieDirectory} drive not found.");
            }

            _tvPaths.AddRange(Directory.GetDirectories(root.TvDirectory));
            _moviePaths.AddRange(Directory.GetDirectories(root.MovieDirectory));
        }

        private Movie ProcessMovieDirectory(string targetDir)
        {
            Log.Debug("Process movies dir {Dir}", targetDir);
            string[] movieEntry = Directory.GetFiles(targetDir).Where(name => !name.EndsWith(".srt", StringComparison.OrdinalIgnoreCase)).ToArray();
            string movieName = Path.GetFileNameWithoutExtension(movieEntry[0]);
            return new Movie(movieName.Trim(), movieEntry[0]);
        }

        private TvShow ProcessTvDirectory(string targetDir)
        {
            Log.Debug("Process tv show dir {Dir}", targetDir);
            // Show directory naming convention: "Show Name%suffix-with-cache-key".
            // Strip the % suffix to get the display name.
            string name = Path.GetFileName(targetDir).Split('%')[0];
            TvShow show = new TvShow(name.Trim(), targetDir)
            {
                Path = targetDir
            };

            string[] seasonEntries = Directory.GetDirectories(targetDir);
            string folderName = Path.GetFileName(seasonEntries[0]);

            if (folderName.Length == 2)
            {
                //To-do MultiLang: english not first folder so show.Seasons not english default
                Array.Sort(seasonEntries);
                seasonEntries = Directory.GetDirectories(seasonEntries[0]);
                Array.Sort(seasonEntries, SeasonComparer);
                show.Seasons = ProcessTvShowSeasonDirectories(seasonEntries, show);
                return ProcessMultiLangTvDirectory(targetDir, show);
            }
            else
            {
                Array.Sort(seasonEntries, SeasonComparer);
                show.Seasons = ProcessTvShowSeasonDirectories(seasonEntries, show);
            }

            return show;
        }

        private TvShow ProcessMultiLangTvDirectory(string folder, TvShow tvShow)
        {
            Log.Debug("Process multi lang tv show dir {Dir}", folder);
            tvShow.MultiLang = true;
            tvShow.MultiLangLastWatched = new List<Episode>();
            tvShow.MultiLangCurrSeason = new List<int>();
            tvShow.MultiLangSeasons = new List<Season[]>();
            tvShow.MultiLangOverview = new List<string>();
            tvShow.MultiLangName = new List<string>();

            string[] langFolders = Directory.GetDirectories(folder);
            Array.Sort(langFolders);
            //To-do MultiLang: not assume en will be index 0 (i = 1)
            for (int i = 1; i < langFolders.Length; i++)
            {
                string langFolder = langFolders[i];
                string langKey = Path.GetFileName(langFolder);
                string language = GetLangCode(langKey);
                tvShow.MultiLangName.Add($"{tvShow.Name} ({language})");
                tvShow.MultiLangCurrSeason.Add(1);
                tvShow.MultiLangLastWatched.Add(null);

                string[] seasonEntries = Directory.GetDirectories(langFolder);
                Array.Sort(seasonEntries, SeasonComparer);
                tvShow.MultiLangSeasons.Add(ProcessTvShowSeasonDirectories(seasonEntries, tvShow));
            }
            return tvShow;
        }

        private Season[] ProcessTvShowSeasonDirectories(string[] seasonEntries, TvShow tvShow)
        {
            Season[] seasons = new Season[seasonEntries.Length];
            for (int i = 0; i < seasonEntries.Length; i++)
            {
                if (!seasonEntries[i].Contains("Extras") && !seasonEntries[i].Contains("Season") && !IsMultiLangSeasonFolder(seasonEntries[i]))
                {
                    _warnings.Add($"{tvShow.Name} contains unknown season folder, index: {i + 1}");
                }

                if (seasonEntries[i].Contains("Extras"))
                {
                    Season extras = new Season(-1);
                    List<Episode> extraEpisodes = new List<Episode>();
                    ProcessExtrasDirectory(extraEpisodes, seasonEntries[i]);
                    extras.Episodes = new Episode[extraEpisodes.Count];
                    for (int j = 0; j < extraEpisodes.Count; j++)
                    {
                        _mediaCount++;
                        extras.Episodes[j] = extraEpisodes[j];
                    }
                    seasons[seasonEntries.Length - 1] = extras;
                    continue;
                }

                if (!seasonEntries[i].Contains("Season"))
                {
                    continue;
                }

                Log.Debug("Process tv show season dir {Dir}", seasonEntries[i]);
                Season season = new Season(i + 1);
                string[] episodeEntries = Directory.GetFiles(seasonEntries[i]).Where(name => !name.EndsWith(".srt", StringComparison.OrdinalIgnoreCase)).ToArray();
                try
                {
                    Array.Sort(episodeEntries, CompareIndex);
                }
                catch
                {
                    _warnings.Add($"Episode is missing separator in {tvShow.Name}, Season {i + 1}");
                }
                season.Episodes = new Episode[episodeEntries.Length];

                for (int j = 0; j < episodeEntries.Length; j++)
                {
                    _mediaCount++;
                    if (tvShow.MultiLang)
                    {
                        _mediaCount++;
                    }
                    try
                    {
                        // Episode filename convention: "N%Episode Name.ext"
                        // Take the filename, split on the first '%', drop the
                        // extension off the second part.
                        string[] episodeNameNumber = Path.GetFileName(episodeEntries[j]).Split(new[] { '%' }, 2);
                        string episodeName = Path.GetFileNameWithoutExtension(episodeNameNumber[1]).Trim();
                        season.Episodes[j] = new Episode(0, episodeName, episodeEntries[j]);
                    }
                    catch
                    {
                        _warnings.Add($"Episode is missing separator in {tvShow.Name}, Season {i + 1}");
                    }
                }
                seasons[i] = season;
            }
            return seasons;
        }

        private void ProcessExtrasDirectory(List<Episode> extras, string targetDir)
        {
            Log.Debug("Process extras dir {Dir}", targetDir);
            string[] rootEntries = Directory.GetFiles(targetDir).Where(name => !name.EndsWith(".srt", StringComparison.OrdinalIgnoreCase)).ToArray();
            foreach (string entry in rootEntries)
            {
                // Extras don't always follow the "N%name.ext" convention - some are
                // just "name.ext" with no number prefix. Either way, the displayable
                // name is the bit after the '%' (or the whole filename if none),
                // minus the extension.
                string[] episodeNameNumber = Path.GetFileName(entry).Split('%');
                string raw = episodeNameNumber.Length == 1 ? episodeNameNumber[0] : episodeNameNumber[1];
                string episodeName = Path.GetFileNameWithoutExtension(raw).Trim();
                extras.Add(new Episode(_extrasIdx--, episodeName, entry));
            }

            string[] subDirs = Directory.GetDirectories(targetDir);
            foreach (string subDir in subDirs)
            {
                ProcessExtrasDirectory(extras, subDir);
            }
        }

        private bool IsMultiLangSeasonFolder(string folder)
            => _multiLangKeys.Contains(Path.GetFileName(folder));

        // Episode-file sorter: filenames follow "NN%Title.ext" where NN is the
        // episode number, optionally suffixed with "#whatever" for two-parters
        // or alternate cuts (e.g. "12#a%Pilot.mkv"). Sort ascending by NN.
        private static int CompareIndex(string a, string b)
            => ExtractEpisodeIndex(a).CompareTo(ExtractEpisodeIndex(b));

        private static int ExtractEpisodeIndex(string path)
        {
            string prefix = Path.GetFileName(path).Split('%')[0];
            int hash = prefix.IndexOf('#');
            if (hash >= 0) prefix = prefix.Substring(0, hash);
            return Int32.Parse(prefix);
        }

        // Season-folder sorter: folder names are "Season N"; sort ascending by
        // N, with the "Extras" folder always pushed to the end.
        private static int SeasonComparer(string a, string b)
        {
            if (a.Contains("Extras")) return 1;
            if (b.Contains("Extras")) return -1;
            return ExtractSeasonNumber(a).CompareTo(ExtractSeasonNumber(b));
        }

        private static int ExtractSeasonNumber(string path)
            => Int32.Parse(Path.GetFileName(path).Split(' ').Last());

        // Maps a TMDB-style two-letter language code to the human-readable
        // name used in the UI ("en" -> "English", "it" -> "Italian").
        private static string GetLangCode(string key)
        {
            return key switch
            {
                "en" => "English",
                "it" => "Italian",
                _ => ""
            };
        }
    }
}
