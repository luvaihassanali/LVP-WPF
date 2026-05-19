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
            string[] path = movieEntry[0].Split('\\');
            string[] movieName = path[path.Length - 1].Split('.');
            Movie movie = new Movie(movieName[0].Trim(), movieEntry[0]);
            return movie;
        }

        private TvShow ProcessTvDirectory(string targetDir)
        {
            Log.Debug("Process tv show dir {Dir}", targetDir);
            string[] path = targetDir.Split('\\');
            string name = path[path.Length - 1].Split('%')[0];
            TvShow show = new TvShow(name.Trim(), targetDir)
            {
                Path = targetDir
            };

            string[] seasonEntries = Directory.GetDirectories(targetDir);
            string[] seasonParts = seasonEntries[0].Split('\\');
            string folderName = seasonParts[seasonParts.Length - 1];

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
                string[] langParts = langFolder.Split('\\');
                string langKey = langParts[langParts.Length - 1];
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
                        string[] namePath = episodeEntries[j].Split('\\');
                        string[] episodeNameNumber = namePath[namePath.Length - 1].Split(new[] { '%' }, 2);
                        int fileSuffixIndex = episodeNameNumber[1].LastIndexOf('.');
                        string episodeName = episodeNameNumber[1].Substring(0, fileSuffixIndex).Trim();
                        Episode episode = new Episode(0, episodeName, episodeEntries[j]);
                        season.Episodes[j] = episode;
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
                string[] namePath = entry.Split('\\');
                string[] episodeNameNumber = namePath[namePath.Length - 1].Split('%');
                int fileSuffixIndex;
                string episodeName;

                if (episodeNameNumber.Length == 1)
                {
                    fileSuffixIndex = episodeNameNumber[0].LastIndexOf('.');
                    episodeName = episodeNameNumber[0].Substring(0, fileSuffixIndex).Trim();
                }
                else
                {
                    fileSuffixIndex = episodeNameNumber[1].LastIndexOf('.');
                    episodeName = episodeNameNumber[1].Substring(0, fileSuffixIndex).Trim();
                }

                Episode ep = new Episode(_extrasIdx--, episodeName, entry);
                extras.Add(ep);
            }

            string[] subDirs = Directory.GetDirectories(targetDir);
            foreach (string subDir in subDirs)
            {
                ProcessExtrasDirectory(extras, subDir);
            }
        }

        private bool IsMultiLangSeasonFolder(string folder)
        {
            string[] folderParts = folder.Split("\\");
            string langKey = folderParts[folderParts.Length - 1];
            return _multiLangKeys.Contains(langKey);
        }

        private static int CompareIndex(string s1, string s2)
        {
            string[] s1Parts = s1.Split('%');
            string[] s2Parts = s2.Split('%');
            string[] s3Parts = s1Parts[s1Parts.Length - 2].Split('\\');
            string[] s4Parts = s2Parts[s2Parts.Length - 2].Split('\\');

            string s5Part = s3Parts[s3Parts.Length - 1];
            string s6Part = s4Parts[s4Parts.Length - 1];
            if (s5Part.Contains('#'))
            {
                s5Part = s5Part.Split('#')[0];
            }
            if (s6Part.Contains('#'))
            {
                s6Part = s6Part.Split('#')[0];
            }

            int indexA = Int32.Parse(s5Part);
            int indexB = Int32.Parse(s6Part);
            if (indexA == indexB) return 0;
            return indexA > indexB ? 1 : -1;
        }

        private static int SeasonComparer(string seasonB, string seasonA)
        {
            if (seasonB.Contains("Extras")) return 1;
            if (seasonA.Contains("Extras")) return -1;
            string[] seasonValuePathA = seasonA.Split();
            string[] seasonValuePathB = seasonB.Split();
            int seasonValueA = Int32.Parse(seasonValuePathA[seasonValuePathA.Length - 1]);
            int seasonValueB = Int32.Parse(seasonValuePathB[seasonValuePathB.Length - 1]);
            if (seasonValueA == seasonValueB) return 0;
            return seasonValueA < seasonValueB ? 1 : -1;
        }

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
