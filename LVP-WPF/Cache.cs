using LVP_WPF.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LVP_WPF
{
    internal static class Cache
    {
        private static readonly MediaRepository _repository = new MediaRepository("media.json");

        public static int mediaCount = 0;
        public static bool update = false;
        private static TextBox logTxtBox;

        internal static async Task Initialize(ProgressBar pb, MediaElement cf, TextBox tf)
        {
            logTxtBox = tf;
            await Task.Run(async () =>
            {
                string[] drives = ConfigurationManager.AppSettings["Drives"].Split(';');
                string[] langKeys = ConfigurationManager.AppSettings["Languages"].Split(";");

                LibraryRoot[] roots = drives.Select(d =>
                {
#if DEBUG
                    return new LibraryRoot($"{d}\\media\\tv", $"{d}\\media\\movie");
#else
                    return new LibraryRoot($"{d}:\\media\\tv", $"{d}:\\media\\movie");
#endif
                }).ToArray();

                LibraryScanner scanner = new LibraryScanner(langKeys);
                ScanResult scanResult = scanner.Scan(roots);

                foreach (string warning in scanResult.Warnings)
                {
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        NotificationDialog.Show("Error", warning);
                    });
                }

                MainWindow.model = scanResult.Model;
                mediaCount = scanResult.MediaCount;

                try
                {
                    update = CheckForUpdates();
                }
                catch (Exception ex)
                {
                    NotificationDialog.Show(ex.Message, ex.StackTrace);
                }

                if (update)
                {
                    //To-do MultiLang: Detect file extension changes and episode deletions
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        pb.Visibility = Visibility.Visible;
                        cf.Visibility = Visibility.Visible;
                        tf.Visibility = Visibility.Visible;
                    });
                    MainWindow.gui.ProgressBarMax = mediaCount;
                    await BuildCache();
                }

                for (int i = 0; i < MainWindow.model.Movies.Length; i++)
                {
                    MainWindow.gui.mediaDict[MainWindow.model.Movies[i].Id] = MainWindow.model.Movies[i];
                }

                for (int i = 0; i < MainWindow.model.TvShows.Length; i++)
                {
                    MainWindow.gui.mediaDict[MainWindow.model.TvShows[i].Id] = MainWindow.model.TvShows[i];
                }

                if (MainWindow.model.HistoryList.Count == 0 || update)
                {
                    MainWindow.model.HistoryList.Clear();
                    foreach (TvShow t in MainWindow.model.TvShows)
                    {
                        if (t.Cartoon)
                        {
                            continue;
                        }
                        foreach (Season s in t.Seasons)
                        {
                            foreach (Episode e in s.Episodes)
                            {
                                MainWindow.model.HistoryList.Add(e);
                            }
                        }
                    }
                    MainWindow.model.HistoryList.Sort((x, y) => DateTime.Compare(x.Date, y.Date));
                }
            });
        }

        #region BuildCache functions

        internal static async Task BuildCache()
        {
            IHttpClientFactory factory = new ServiceCollection().AddHttpClient().BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
            using HttpClient client = factory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(1);

            string apiKey = ConfigurationManager.AppSettings["TmdbApiKey"];
            string cacheRoot = $"{AppDomain.CurrentDomain.BaseDirectory}cache";
            TmdbClient tmdb = new TmdbClient(apiKey, client, cacheRoot, Log);

            string translatorPath = $"{ConfigurationManager.AppSettings["LibreTranslatePath"]}libretranslate.exe";
            using Translator translator = new Translator(translatorPath, client);

            for (int i = 0; i < MainWindow.model.Movies.Length; i++)
            {
                await BuildMovieCacheAsync(MainWindow.model.Movies[i], tmdb);
                MainWindow.gui.ProgressBarValue++;
            }

            for (int i = 0; i < MainWindow.model.TvShows.Length; i++)
            {
                TvShow tvShow = MainWindow.model.TvShows[i];

                if (tvShow.Name.Equals("Tom & Jerry"))
                {
                    CustomCache.BuildTomAndJerryData(tvShow);
                }
                else if (tvShow.Name.Equals("Looney Tunes"))
                {
                    CustomCache.BuildLooneyTunesData(tvShow);
                }
                else
                {
                    await BuildTvShowCache(tvShow, tmdb, translator);
                }
            }

            Array.Sort(MainWindow.model.Movies, Movie.SortMoviesAlphabetically());
            Array.Sort(MainWindow.model.TvShows, TvShow.SortTvShowsAlphabetically());
            SaveData();
        }

        private static async Task BuildTvShowCache(TvShow tvShow, TmdbClient tmdb, Translator translator)
        {
            if (tvShow.Id == 0)
            {
                await BuildTvShowGeneralData(tvShow, tmdb);
            }
            await BuildSeasonCache(tvShow, tmdb);

            if (tvShow.MultiLang)
            {
                for (int i = 0; i < tvShow.MultiLangSeasons.Count; i++)
                {
                    Season[] currSeason = tvShow.Seasons;
                    tvShow.Seasons = tvShow.MultiLangSeasons[i];
                    // Build Season cache again some translated shows have different episodes # per season
                    await BuildSeasonCache(tvShow, tmdb);
                    tvShow.MultiLangSeasons[i] = tvShow.Seasons;
                    tvShow.Seasons = currSeason;
                }
                await ApplyMultiLangTvShowTranslations(tvShow, translator);
            }
        }

        private static async Task ApplyMultiLangTvShowTranslations(TvShow tvShow, Translator translator)
        {
            bool skippedEnglish = false;
            bool overviewTranslated = false;
            string[] lang = Directory.GetDirectories(tvShow.Path);
            for (int i = 0; i < lang.Length; i++)
            {
                if (!skippedEnglish && lang[i].EndsWith("\\en"))
                {
                    skippedEnglish = true;
                    continue;
                }

                string[] langParts = lang[i].Split('\\');
                string langKey = langParts[langParts.Length - 1];

                for (int j = 0; j < tvShow.MultiLangSeasons.Count; j++)
                {
                    Season[] multiLangSeasons = tvShow.MultiLangSeasons[j];
                    for (int k = 0; k < multiLangSeasons.Length; k++)
                    {
                        Season multiLangSeason = multiLangSeasons[k];
                        for (int l = 0; l < multiLangSeason.Episodes.Length; l++)
                        {
                            if (multiLangSeason.Episodes[l].Translated)
                            {
                                continue;
                            }

                            if (!overviewTranslated)
                            {
                                overviewTranslated = true;
                                string overview = await translator.TranslateAsync(langKey, tvShow.Overview);
                                if (!tvShow.MultiLangOverview.Contains(overview))
                                {
                                    tvShow.MultiLangOverview.Add(overview);
                                }
                            }

                            multiLangSeason.Episodes[l].Name = await translator.TranslateAsync(langKey, multiLangSeason.Episodes[l].Name);
                            multiLangSeason.Episodes[l].Overview = await translator.TranslateAsync(langKey, multiLangSeason.Episodes[l].Overview);
                            MainWindow.gui.ProgressBarValue++;
                            multiLangSeason.Episodes[l].Translated = true;
                        }
                    }
                }
            }
        }

        private static async Task BuildTvShowGeneralData(TvShow tvShow, TmdbClient tmdb)
        {
            JObject tvObject = await tmdb.SearchTvAsync(tvShow.Name);
            int totalResults = (int)tvObject["total_results"];

            if (totalResults == 0)
            {
                Cache.SaveData();
                NotificationDialog.Show("Error", $"No tv show found for: {tvShow.Name}");
            }
            else if (totalResults != 1)
            {
                int actualResults = (int)((JArray)tvObject["results"]).Count();
                string[] names = new string[actualResults];
                string[] ids = new string[actualResults];
                string[] overviews = new string[actualResults];
                DateTime?[] dates = new DateTime?[actualResults];

                for (int j = 0; j < actualResults; j++)
                {
                    dates[j] = DateTime.TryParse((string)tvObject["results"][j]["first_air_date"], out DateTime temp) ? temp : DateTime.MinValue.AddHours(9);
                    names[j] = (string)tvObject["results"][j]["name"];
                    names[j] = names[j].FixBrokenQuotes();
                    ids[j] = (string)tvObject["results"][j]["id"];
                    overviews[j] = (string)tvObject["results"][j]["overview"];
                    overviews[j] = overviews[j].FixBrokenQuotes();
                }

                string[][] info = new string[][] { names, ids, overviews };
                Application.Current.Dispatcher.Invoke(delegate
                {
                    tvShow.Id = OptionDialog.Show(tvShow.Name, tvShow.Seasons[0].Episodes[0].Path, info, dates);
                });
            }
            else
            {
                tvShow.Id = (int)tvObject["results"][0]["id"];
            }

            tvObject = await tmdb.GetTvShowAsync(tvShow.Id);

            tvShow.Date = DateTime.TryParse((string)tvObject["first_air_date"], out DateTime tempDate) ? tempDate : DateTime.MinValue.AddHours(9);
            tvShow.Overview = (string)tvObject["overview"];
            tvShow.Overview = tvShow.Overview.FixBrokenQuotes();
            tvShow.Poster = (string)tvObject["poster_path"];
            tvShow.Backdrop = (string)tvObject["backdrop_path"];
            int[] runtime = tvObject["episode_run_time"].Select(x => (int)x).ToArray();
            if (runtime.Length != 0)
            {
                tvShow.RunningTime = runtime[0];
            }
            else
            {
                tvShow.RunningTime = -1;
            }

            JToken? genres = tvObject["genres"];
            foreach (JToken? genre in genres)
            {
                string cartoonExceptionStr = ConfigurationManager.AppSettings["CartoonExceptions"];
                string[] cartoonExceptions = cartoonExceptionStr.Split(";");
                if ((int)genre["id"] == 16 && !cartoonExceptions.Contains(tvShow.Name))
                {
                    tvShow.Cartoon = true;
                }
            }

            if (tvShow.Backdrop != null)
            {
                tvShow.Backdrop = await tmdb.DownloadImageAsync(tvShow.Backdrop, false, tvShow.Name);
            }

            if (tvShow.Poster != null)
            {
                tvShow.Poster = await tmdb.DownloadImageAsync(tvShow.Poster, false, tvShow.Name);
            }
        }

        private static async Task BuildSeasonCache(TvShow tvShow, TmdbClient tmdb)
        {
            int seasonIndex = 0;
            for (int j = 0; j < tvShow.Seasons.Length; j++)
            {
                Season season = tvShow.Seasons[j];
                if (season.Id == -1)
                {
                    continue;
                }

                JObject seasonObject = await tmdb.GetTvSeasonAsync(tvShow.Id, seasonIndex);
                //{"success":false,"status_code":34,"status_message":"The resource you requested could not be found."}
                if (seasonObject["success"] != null && (bool)seasonObject["success"] == false)
                {
                    seasonIndex = 1;
                    seasonObject = await tmdb.GetTvSeasonAsync(tvShow.Id, seasonIndex);
                }

                try
                {
                    if (((string)seasonObject["name"]).Contains("Specials"))
                    {
                        seasonIndex++;
                        seasonObject = await tmdb.GetTvSeasonAsync(tvShow.Id, seasonIndex);
                    }
                }
                catch
                {
                    NotificationDialog.Show("Error", $"Season first index error: {tvShow.Name}, ID = {tvShow.Id}");
                }

                if (season.Poster == null)
                {
                    season.Poster = (string)seasonObject["poster_path"];
                    season.Date = DateTime.TryParse((string)seasonObject["air_date"], out DateTime tempDate) ? tempDate : DateTime.MinValue.AddHours(9);

                    if (season.Poster != null)
                    {
                        season.Poster = await tmdb.DownloadImageAsync(season.Poster, false, tvShow.Name);
                    }
                }

                JArray jEpisodes = (JArray)seasonObject["episodes"];
                jEpisodes = new JArray(jEpisodes.OrderBy(obj => (int)obj["episode_number"]));

                Episode[] episodes = season.Episodes;
                int jEpIndex = 0;

                for (int k = 0; k < episodes.Length; k++)
                {
                    if (episodes[k].Id != 0)
                    {
                        jEpIndex++;
                        continue;
                    }
                    if (k > jEpisodes.Count - 1)
                    {
                        string message = $"Episode index out of TMDB episodes range S{seasonIndex}E{jEpIndex}";
                        NotificationDialog.Show($"Error: {tvShow.Name}", message);
                    }
                    Episode episode = episodes[k];

                    if (episode.Name.Contains('#'))
                    {
                        string[] multiEpNames = episode.Name.Split('#');
                        JObject[] jEpisodesMulti = new JObject[multiEpNames.Length];
                        int numEps = multiEpNames.Length;
                        String multiEpisodeOverview = "";
                        for (int l = 0; l < numEps; l++)
                        {
                            jEpisodesMulti[l] = (JObject)jEpisodes[jEpIndex + l];
                            string jCurrMultiEpisodeName = (string)jEpisodesMulti[l]["name"];
                            string jCurrMultiEpisodeOverview = (string)jEpisodesMulti[l]["overview"];
                            string currMultiEpisodeName = multiEpNames[l];
                            if (String.Compare(currMultiEpisodeName, jCurrMultiEpisodeName.FixBrokenQuotes(), System.Globalization.CultureInfo.CurrentCulture,
                                System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreSymbols) != 0)
                            {
                                string message = $"Multi episode name does not match retrieved data: Renaming file: '{currMultiEpisodeName}', to: '{jCurrMultiEpisodeName.FixBrokenQuotes()}' (Season {season.Id}).";
                                InputDialog.Show($"Warning: {tvShow.Name}", message, tvShow, season.Id + 1);

                                string oldPath = episode.Path;
                                string newPath = oldPath.Replace(currMultiEpisodeName, jCurrMultiEpisodeName.FixBrokenQuotes());
                                string invalid = new string(Path.GetInvalidPathChars()) + '?' + ':' + '*';
                                foreach (char c in invalid)
                                {
                                    newPath = newPath.Replace(c.ToString(), "");
                                }

                                try
                                {
                                    char drive = newPath[0];
                                    string drivePath = $"{drive}:";
                                    newPath = ReplaceFirst(newPath, drive.ToString(), drivePath);
                                    File.Move(oldPath, newPath);
                                    episode.Path = newPath;
                                    CheckSubtitleName(tvShow, season, oldPath, newPath);
                                }
                                catch (Exception e)
                                {
                                    NotificationDialog.Show("Error", e.Message);
                                }
                            }
                            multiEpisodeOverview += (jCurrMultiEpisodeOverview + Environment.NewLine + Environment.NewLine);
                        }

                        episode.Date = DateTime.TryParse((string)jEpisodesMulti[numEps - 1]["air_date"], out DateTime mTempDate) ? mTempDate : DateTime.MinValue.AddHours(9);
                        episode.Id = (int)jEpisodesMulti[numEps - 1]["episode_number"];
                        episode.Backdrop = (string)jEpisodesMulti[numEps - 1]["still_path"];
                        episode.Overview = multiEpisodeOverview;

                        if (episode.Backdrop != null)
                        {
                            episode.Backdrop = await tmdb.DownloadImageAsync(episode.Backdrop, false, tvShow.Name);
                        }
                        jEpIndex += (numEps);
                        continue;
                    }

                    JObject jEpisode = null;
                    try
                    {
                        jEpisode = (JObject)jEpisodes[jEpIndex];
                    }
                    catch
                    {
                        string message = $"Episode index out of TMDB episodes range S{seasonIndex}E{k + 1}";
                        NotificationDialog.Show($"Error: {tvShow.Name}", message);
                    }

                    string jEpisodeName = (string)jEpisode["name"];
                    if (!(String.Compare(episode.Name, jEpisodeName.FixBrokenQuotes(), System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreSymbols) == 0))
                    {
                        string message = $"Local episode name does not match retrieved data. Renaming file '{episode.Name}' to '{jEpisodeName.FixBrokenQuotes()}' (Season {season.Id}).";
                        InputDialog.Show($"Warning: {tvShow.Name}", message, tvShow, season.Id + 1);

                        string oldPath = episode.Path;
                        jEpisodeName = (string)jEpisode["name"];
                        string newPath = ReplaceLastOccurrence(oldPath, episode.Name, jEpisodeName.FixBrokenQuotes());
                        string invalid = new string(Path.GetInvalidPathChars()) + '?' + ':' + '*';
                        foreach (char c in invalid)
                        {
                            newPath = newPath.Replace(c.ToString(), "");
                        }

                        try
                        {
                            char drive = newPath[0];
                            string drivePath = drive == '\\' ? $"{drive}" : $"{drive}:";
                            newPath = ReplaceFirst(newPath, drive.ToString(), drivePath);
                            File.Move(oldPath, newPath);
                            CheckSubtitleName(tvShow, season, oldPath, newPath);
                        }
                        catch (Exception e)
                        {
                            NotificationDialog.Show("Error", e.Message);
                        }

                        episode.Path = newPath;
                        episode.Name = jEpisodeName.FixBrokenQuotes();
                    }

                    episode.Date = DateTime.TryParse((string)jEpisode["air_date"], out DateTime tempDate) ? tempDate : DateTime.MinValue.AddHours(9);
                    episode.Id = (int)jEpisode["episode_number"];
                    episode.Overview = (string)jEpisode["overview"];
                    episode.Overview = episode.Overview.FixBrokenQuotes();
                    episode.Backdrop = (string)jEpisode["still_path"];

                    if (episode.Backdrop != null)
                    {
                        episode.Backdrop = await tmdb.DownloadImageAsync(episode.Backdrop, false, tvShow.Name);
                    }
                    jEpIndex++;
                    MainWindow.gui.ProgressBarValue++;
                }
                seasonIndex++;
            }
        }

        public static string ReplaceLastOccurrence(string source, string find, string replace)
        {
            int place = source.LastIndexOf(find);

            if (place == -1)
                return source;

            return source.Remove(place, find.Length).Insert(place, replace);
        }

        private static void CheckSubtitleName(TvShow tvShow, Season season, string oldPath, string newPath)
        {
            if (tvShow.MultiLang)
            {
                int separatorIndex = oldPath.LastIndexOf(".");
                string oldSrtPath = $"{oldPath.Substring(0, separatorIndex)}.srt";
                if (File.Exists(oldSrtPath))
                {
                    int newSeparatorIndex = newPath.LastIndexOf(".");
                    string newSrtPath = $"{newPath.Substring(0, newSeparatorIndex)}.srt";
                    string[] temp = oldSrtPath.Split("\\");
                    string oldSubFileName = temp[temp.Length - 1];
                    temp = newSrtPath.Split("\\");
                    string newSubFileName = temp[temp.Length - 1];
                    string subMsg = $"Renaming subtitle file {oldSubFileName} to {newSubFileName} (Season {season.Id}).";
                    InputDialog.Show($"Warning: {tvShow.Name}", subMsg, tvShow, season.Id + 1);
                    File.Move(oldSrtPath, newSrtPath);
                }
                else
                {
                    if (!oldPath.Contains("\\en\\"))
                    {
                        NotificationDialog.Show("Error", $"No subtitle file found {oldSrtPath} (Season {season.Id}).");
                    }
                }
            }
        }

        private static async Task BuildMovieCacheAsync(Movie movie, TmdbClient tmdb)
        {
            if (movie.Id != 0)
            {
                return;
            }

            JObject movieObject = await tmdb.SearchMovieAsync(movie.Name);
            int numMovieObjects = (int)movieObject["total_results"];

            if (numMovieObjects == 0)
            {
                Cache.SaveData();
                NotificationDialog.Show("Error", $"No movie found for: {movie.Name}");
            }
            else if (numMovieObjects != 1)
            {
                int resultCount = ((JArray)movieObject["results"]).Count();
                string[] names = new string[resultCount];
                string[] ids = new string[resultCount];
                string[] overviews = new string[resultCount];
                DateTime?[] dates = new DateTime?[resultCount];

                for (int j = 0; j < resultCount; j++)
                {
                    names[j] = (string)movieObject["results"][j]["title"];
                    names[j] = names[j].FixBrokenQuotes();
                    ids[j] = (string)movieObject["results"][j]["id"];
                    overviews[j] = (string)movieObject["results"][j]["overview"];
                    overviews[j] = overviews[j].FixBrokenQuotes();
                    dates[j] = DateTime.TryParse((string)movieObject["results"][j]["release_date"], out DateTime temp) ? temp : DateTime.MinValue.AddHours(9);
                }

                string[][] info = new string[][] { names, ids, overviews };
                Application.Current.Dispatcher.Invoke(delegate
                {
                    movie.Id = OptionDialog.Show(movie.Name, movie.Path, info, dates);
                });
            }
            else
            {
                movie.Id = (int)movieObject["results"][0]["id"];
            }

            movieObject = await tmdb.GetMovieAsync(movie.Id);
            await UpdateMovieData(movie, movieObject, tmdb);
        }

        private static async Task UpdateMovieData(Movie movie, JObject movieObject, TmdbClient tmdb)
        {
            if (!(String.Compare(movie.Name.Replace(":", ""), ((string)movieObject["title"]).Replace(":", "").FixBrokenQuotes(), System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreSymbols) == 0))
            {
                string message = $"Local movie name does not match retrieved data. Renaming file '{movie.Name.Replace(":", "")}' to '{((string)movieObject["title"]).Replace(":", "")}'.";
                InputDialog.Show("Warning", message);
                string oldPath = movie.Path;
                string[] fileNamePath = oldPath.Split('\\');
                string fileName = fileNamePath[fileNamePath.Length - 1];
                string extension = fileName.Split('.')[1];
                string newFileName = ((string)movieObject["title"]).Replace(":", "").FixBrokenQuotes(); ;
                string newPath = oldPath.Replace(fileName, $"{newFileName}.{extension}");
                string invalid = new string(System.IO.Path.GetInvalidPathChars()) + '?';
                foreach (char c in invalid)
                {
                    newPath = newPath.Replace(c.ToString(), "");
                }
                File.Move(oldPath, newPath);
                movie.Path = newPath;
                movie.Name = newFileName;
            }

            movie.Date = DateTime.TryParse((string)movieObject["release_date"], out DateTime tempDate) ? tempDate : DateTime.MinValue.AddHours(9);
            movie.Backdrop = (string)movieObject["backdrop_path"];
            movie.Poster = (string)movieObject["poster_path"];
            movie.Overview = (string)movieObject["overview"];
            movie.Overview = movie.Overview.FixBrokenQuotes();
            movie.RunningTime = (int)movieObject["runtime"];

            if (movie.Backdrop != null)
            {
                movie.Backdrop = await tmdb.DownloadImageAsync(movie.Backdrop, true, movie.Name);
            }

            if (movie.Poster != null)
            {
                movie.Poster = await tmdb.DownloadImageAsync(movie.Poster, true, movie.Name);
            }
        }

        internal static string ReplaceFirst(string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }
            return string.Concat(text.AsSpan(0, pos), replace, text.AsSpan(pos + search.Length));
        }

        #endregion

        internal static bool CheckForUpdates()
        {
            Log("Check for updates start...");
            MainModel? prevMedia = _repository.Load();

            if (prevMedia == null)
            {
                return true;
            }

            bool result = !MainWindow.model.Compare(prevMedia);
            if (!result)
            {
                MainWindow.model = prevMedia;
            }
            else
            {
                MainWindow.model.Ingest(prevMedia);
            }
            Log("Check for updates end");
            return result;
        }

        internal static void SaveData()
        {
            _repository.Save(MainWindow.model);
        }

        private static void Log(string msg)
        {
#if DEBUG
            Debug.WriteLine(msg);
#endif
            logTxtBox.Dispatcher.Invoke(delegate
            {
                logTxtBox.Text += MainWindow.gui.ProgressBarValue != 1 ?  $"[{MainWindow.gui.ProgressBarValue}/{MainWindow.gui.ProgressBarMax}] {msg}\n" : $"{msg}\n";
                logTxtBox.Focus();
                logTxtBox.CaretIndex = logTxtBox.Text.Length;
                logTxtBox.ScrollToEnd();
            });
        }
    }

    public static class StringExtension
    {
        private const string targetSingleQuoteSymbol = "'";
        private const string genericSingleQuoteSymbol = "â€™";
        private const string openSingleQuoteSymbol = "â€˜";
        private const string closeSingleQuoteSymbol = "â€™";
        private const string frenchAccentAigu = "Ã©";
        private const string frenchAccentGrave = "Ã";

        public static string FixBrokenQuotes(this string str)
        {
            return str.Replace(genericSingleQuoteSymbol, targetSingleQuoteSymbol).Replace(openSingleQuoteSymbol, targetSingleQuoteSymbol)
                .Replace(closeSingleQuoteSymbol, targetSingleQuoteSymbol).Replace(frenchAccentAigu, "e").Replace(frenchAccentGrave, "a").Replace("%", "percent").Replace("  ", " ");
        }
    }
}