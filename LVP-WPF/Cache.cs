using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace LVP_WPF
{
    internal static class Cache
    {
        private const string apiKey = "?api_key=c69c4effc7beb9c473d22b8f85d59e4c";
        private const string apiUrl = "https://api.themoviedb.org/3/";
        private const string apiImageUrl = "http://image.tmdb.org/t/p/original";
        private const string apiTvSearchUrl = apiUrl + "search/tv" + apiKey + "&query=";
        private const string apiMovieSearchUrl = apiUrl + "search/movie" + apiKey + "&query=";
        private const string jsonFile = "media-data.json";

        private static string apiTvUrl = apiUrl + "tv/{tv_id}" + apiKey;
        private static string apiTvSeasonUrl = apiUrl + "tv/{tv_id}/season/{season_number}" + apiKey;
        private static string apiMovieUrl = apiUrl + "movie/{movie_id}" + apiKey;
        private static string imageDownloadBufferString = "";

        private static List<string> tvPathList = new List<string>();
        private static List<string> moviePathList = new List<string>();

        #region BuildCache function

        internal static async void BuildCache()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(5);

            for (int i = 0; i < MainWindow.model.Movies.Length; i++)
            {
                await BuildMovieCacheAsync(MainWindow.model.Movies[i], client);
            }

            /*for (int i = 0; i < MainWindow.mainModel.Movies.Length; i++)
            {
                BuildTvShowCache(MainWindow.mainModel.TvShows[i], client);
            }*/

            client.Dispose();

            //Array.Sort(MainForm.media.Movies, Movie.SortMoviesAlphabetically());
            //Array.Sort(MainForm.media.TvShows, TvShow.SortTvShowsAlphabetically());

            //string jsonString = JsonConvert.SerializeObject(MainForm.media);
            //File.WriteAllText(MainForm.jsonFile, jsonString);
        }

        private static async Task BuildMovieCacheAsync(Movie movie, HttpClient client)
        {
            if (movie.Id != 0) return;
            //To-do create binding in gui model for update label
            //UpdateLoadingLabel("Processing: " + MainForm.media.Movies[i].Name);
            string movieSearchUrl = apiMovieSearchUrl + movie.Name;
            using HttpResponseMessage movieSearchResponse = await client.GetAsync(movieSearchUrl);
            using HttpContent movieSearchContent = movieSearchResponse.Content;
            string movieResourceString = await movieSearchContent.ReadAsStringAsync();

            JObject movieObject = JObject.Parse(movieResourceString);
            int numMovieObjects = (int)movieObject["total_results"];

            if (numMovieObjects == 0)
            {
                NotificationDialog.Show(
                    "Cache.BuildMovieCache",
                    "Error",
                    "No movie found for: " + movie.Name,
                NotifyIcon.Exclamation);
                Environment.Exit(0);
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
                    names[j] = names[j].fixBrokenQuotes();
                    ids[j] = (string)movieObject["results"][j]["id"];
                    overviews[j] = (string)movieObject["results"][j]["overview"];
                    overviews[j] = overviews[j].fixBrokenQuotes();
                    DateTime temp;
                    dates[j] = DateTime.TryParse((string)movieObject["results"][j]["release_date"], out temp) ? temp : DateTime.MinValue.AddHours(9);
                }

                string[][] info = new string[][] { names, ids, overviews };
                movie.Id = OptionDialog.Show(movie.Name, info, dates, NotifyIcon.Question);
            }
            else
            {
                movie.Id = (int)movieObject["results"][0]["id"];
            }

            string movieUrl = apiMovieUrl.Replace("{movie_id}", movie.Id.ToString());
            using HttpResponseMessage movieResponse = await client.GetAsync(movieUrl);
            using HttpContent movieContent = movieResponse.Content;
            string movieString = await movieContent.ReadAsStringAsync();

            movieObject = JObject.Parse(movieString);
            await UpdateMovieData(movie, movieObject);
        }

        private static async Task UpdateMovieData(Movie movie, JObject movieObject)
        {
            if (!(String.Compare(movie.Name.Replace(":", ""), ((string)movieObject["title"]).Replace(":", "").fixBrokenQuotes(), System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreSymbols) == 0))
            {
                string message = "Local movie name does not match retrieved data. Renaming file '" + movie.Name.Replace(":", "") + "' to '" + ((string)movieObject["title"]).Replace(":", "") + "'.";
                NotificationDialog.Show(
                    "Cache.UpdateMovieData",
                    "Warning",
                     message,
                NotifyIcon.Exclamation);

                string oldPath = movie.Path;
                string[] fileNamePath = oldPath.Split('\\');
                string fileName = fileNamePath[fileNamePath.Length - 1];
                string extension = fileName.Split('.')[1];
                string newFileName = ((string)movieObject["title"]).Replace(":", "").fixBrokenQuotes(); ;
                string newPath = oldPath.Replace(fileName, newFileName + "." + extension);
                string invalid = new string(Path.GetInvalidPathChars()) + '?';
                foreach (char c in invalid)
                {
                    newPath = newPath.Replace(c.ToString(), "");
                }
                File.Move(oldPath, newPath);
                movie.Path = newPath;
                movie.Name = newFileName;
            }

            DateTime tempDate;
            movie.Date = DateTime.TryParse((string)movieObject["release_date"], out tempDate) ? tempDate : DateTime.MinValue.AddHours(9);
            movie.Backdrop = (string)movieObject["backdrop_path"];
            movie.Poster = (string)movieObject["poster_path"];
            movie.Overview = (string)movieObject["overview"];
            movie.Overview = movie.Overview.fixBrokenQuotes();
            movie.RunningTime = (int)movieObject["runtime"];

            if (movie.Backdrop != null)
            {
                movie.Backdrop = await DownloadImage(movie.Backdrop, movie.Name, true);
            }

            if (movie.Poster != null)
            {
                movie.Poster = await DownloadImage(movie.Poster, movie.Name, true);
            }
        }

        private static void BuildTvShowCache(TvShow tvShow, HttpClient client)
        {
            /*
for (int i = 0; i < MainForm.media.TvShows.Length; i++)
{
    TvShow tvShow = MainForm.media.TvShows[i];

    if (tvShow.Name.Equals("Tom & Jerry"))
    {
        CustomCache.BuildTomAndJerryData(tvShow);
        continue;
    }
    else if (tvShow.Name.Equals("Looney Tunes"))
    {
        CustomCache.BuildLooneyTunesData(tvShow);
        continue;
    }

    // If id is not 0 then general show data initialized
    if (tvShow.Id == 0)
    {
        string tvResourceString = client.DownloadString(tvSearch + tvShow.Name);

        JObject tvObject = JObject.Parse(tvResourceString);
        int totalResults = (int)tvObject["total_results"];

        if (totalResults == 0)
        {
            CustomDialog.ShowMessage("Error", "No tv show for: " + tvShow.Name, mainForm.Width, mainForm.Height);
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
                names[j] = (string)tvObject["results"][j]["name"];
                names[j] = names[j].fixBrokenQuotes();
                ids[j] = (string)tvObject["results"][j]["id"];
                overviews[j] = (string)tvObject["results"][j]["overview"];
                overviews[j] = overviews[j].fixBrokenQuotes();

                DateTime temp;
                dates[j] = DateTime.TryParse((string)tvObject["results"][j]["first_air_date"], out temp) ? temp : DateTime.MinValue.AddHours(9);
            }

            string[][] info = new string[][] { names, ids, overviews };
            tvShow.Id = CustomDialog.ShowOptions(tvShow.Name, info, dates, mainForm.Width, mainForm.Height);
        }
        else
        {
            tvShow.Id = (int)tvObject["results"][0]["id"];
        }

        // 404
        string tvString = "";
        try
        {
            tvString = client.DownloadString(tvGet.Replace("{tv_id}", tvShow.Id.ToString()));
        }
        catch
        {
            CustomDialog.ShowMessage("Error", "No tv show found for: " + tvShow.Name, mainForm.Width, mainForm.Height);
            Environment.Exit(1);
        }

        tvObject = JObject.Parse(tvString);
        tvShow.Overview = (string)tvObject["overview"];
        tvShow.Overview = tvShow.Overview.fixBrokenQuotes();
        tvShow.Poster = (string)tvObject["poster_path"];
        tvShow.Backdrop = (string)tvObject["backdrop_path"];
        tvShow.RunningTime = (int)tvObject["episode_run_time"][0];
        var genres = tvObject["genres"];
        foreach (var genre in genres)
        {
            if ((int)genre["id"] == 16 && !(tvShow.Name == "Family Guy" || tvShow.Name == "The Simpsons" || tvShow.Name == "Futurama" || tvShow.Name == "The Boondocks" || tvShow.Name == "American Dad!"))
            {
                tvShow.Cartoon = true;
            }
        }

        DateTime tempDate;
        tvShow.Date = DateTime.TryParse((string)tvObject["first_air_date"], out tempDate) ? tempDate : DateTime.MinValue.AddHours(9);

        if (tvShow.Backdrop != null)
        {
            await DownloadImage(tvShow.Backdrop, tvShow.Name, false, token);
            tvShow.Backdrop = bufferString;
        }

        if (tvShow.Poster != null)
        {
            await DownloadImage(tvShow.Poster, tvShow.Name, false, token);
            tvShow.Poster = bufferString;
        }
    }

    // Always check season data for new content
    // Some shows first season index = 1
    string tvIdExceptionsStr = ConfigurationManager.AppSettings["tvIdExceptions"];
    string[] tvIdExceptionsStrArr = tvIdExceptionsStr.Split(';');
    int[] tvIdExceptions = new int[tvIdExceptionsStrArr.Length];
    for (int idIdx = 0; idIdx < tvIdExceptionsStrArr.Length; idIdx++)
    {
        tvIdExceptions[idIdx] = int.Parse(tvIdExceptionsStrArr[idIdx]);
    }
    int seasonIndex = 0;
    if (tvIdExceptions.Contains(tvShow.Id))
    {
        seasonIndex = 1;
    }
    for (int j = 0; j < tvShow.Seasons.Length; j++)
    {
        Season season = tvShow.Seasons[j];

        if (season.Id == -1) continue;

        string seasonLabel = tvShow.Seasons[j].Id == -1 ? "Extras" : (j + 1).ToString();
        UpdateLoadingLabel("Processing: " + tvShow.Name + " Season " + seasonLabel);

        string seasonApiCall = tvSeasonGet.Replace("{tv_id}", tvShow.Id.ToString()).Replace("{season_number}", seasonIndex.ToString());

        string seasonString = "";
        try
        {
            seasonString = client.DownloadString(seasonApiCall);
        }
        catch
        {
            CustomDialog.ShowMessage("Error", "Season first index error: " + tvShow.Name + ", ID = " + tvShow.Id, mainForm.Width, mainForm.Height);
            Environment.Exit(1);
        }

        JObject seasonObject = JObject.Parse(seasonString);
        if (((string)seasonObject["name"]).Contains("Specials"))
        {
            seasonIndex++;
            seasonString = client.DownloadString(tvSeasonGet.Replace("{tv_id}", tvShow.Id.ToString()).Replace("{season_number}", seasonIndex.ToString()));
            seasonObject = JObject.Parse(seasonString);
        }

        if (season.Poster == null)
        {
            season.Poster = (string)seasonObject["poster_path"];
            DateTime tempDate;
            season.Date = DateTime.TryParse((string)seasonObject["air_date"], out tempDate) ? tempDate : DateTime.MinValue.AddHours(9);

            if (season.Poster != null)
            {
                await DownloadImage(season.Poster, tvShow.Name, false, token);
                season.Poster = bufferString;
            }
        }

        JArray jEpisodes = (JArray)seasonObject["episodes"];
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
                string message = "Episode index out of TMDB episodes range S" + seasonIndex.ToString() + "E" + (k + 1).ToString();
                CustomDialog.ShowMessage("Warning: " + tvShow.Name, message, mainForm.Width, mainForm.Height);
                continue;
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
                    String jCurrMultiEpisodeName = (string)jEpisodesMulti[l]["name"];
                    String jCurrMultiEpisodeOverview = (string)jEpisodesMulti[l]["overview"];
                    String currMultiEpisodeName = multiEpNames[l];
                    if (String.Compare(currMultiEpisodeName, jCurrMultiEpisodeName.fixBrokenQuotes(), System.Globalization.CultureInfo.CurrentCulture,
                        System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreSymbols) != 0)
                    {
                        string message = "Multi episode name does not match retrieved data: Episode name: '" + currMultiEpisodeName + "', retrieved: '" + jCurrMultiEpisodeName.fixBrokenQuotes() + "' (Season " + season.Id + ").";
                        CustomDialog.ShowMessage("Warning: " + tvShow.Name, message, mainForm.Width, mainForm.Height);
                    }
                    multiEpisodeOverview += (jCurrMultiEpisodeOverview + Environment.NewLine + Environment.NewLine);
                }

                episode.Id = (int)jEpisodesMulti[numEps - 1]["episode_number"];
                episode.Backdrop = (string)jEpisodesMulti[numEps - 1]["still_path"];
                episode.Overview = multiEpisodeOverview;
                DateTime tempDate;
                episode.Date = DateTime.TryParse((string)jEpisodesMulti[numEps - 1]["air_date"], out tempDate) ? tempDate : DateTime.MinValue.AddHours(9);

                if (episode.Backdrop != null)
                {
                    await DownloadImage(episode.Backdrop, tvShow.Name, false, token);
                    episode.Backdrop = bufferString;
                }
                jEpIndex += (numEps);
                continue;
            }

            JObject jEpisode = (JObject)jEpisodes[jEpIndex];
            String jEpisodeName = (string)jEpisode["name"];
            if (String.Compare(episode.Name, jEpisodeName.fixBrokenQuotes(),
                System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreSymbols) == 0)
            {
                episode.Id = (int)jEpisode["episode_number"];
                episode.Overview = (string)jEpisode["overview"];
                episode.Overview = episode.Overview.fixBrokenQuotes();
                episode.Backdrop = (string)jEpisode["still_path"];
                DateTime tempDate;
                episode.Date = DateTime.TryParse((string)jEpisode["air_date"], out tempDate) ? tempDate : DateTime.MinValue.AddHours(9);

                if (episode.Backdrop != null)
                {
                    await DownloadImage(episode.Backdrop, tvShow.Name, false, token);
                    episode.Backdrop = bufferString;
                }
            }
            else
            {
                string message = "Local episode name does not match retrieved data. Renaming file '" + episode.Name + "' to '" + jEpisodeName.fixBrokenQuotes() + "' (Season " + season.Id + ").";
                CustomDialog.ShowMessage("Warning: " + tvShow.Name, message, mainForm.Width, mainForm.Height);

                string oldPath = episode.Path;
                jEpisodeName = (string)jEpisode["name"];
                string newPath = oldPath.Replace(episode.Name, jEpisodeName.fixBrokenQuotes());
                string invalid = new string(Path.GetInvalidPathChars()) + '?' + ':' + '*';
                foreach (char c in invalid)
                {
                    newPath = newPath.Replace(c.ToString(), "");
                }
                try
                {
                    char drive = newPath[0];
                    string drivePath = drive + ":";
                    newPath = ReplaceFirst(newPath, drive.ToString(), drivePath);

                    File.Move(oldPath, newPath);
                }
                catch (Exception e)
                {
                    CustomDialog.ShowMessage("Error", e.Message, mainForm.Width, mainForm.Height);
                }

                episode.Path = newPath;
                episode.Name = jEpisodeName.fixBrokenQuotes();
                episode.Id = (int)jEpisode["episode_number"];
                episode.Overview = (string)jEpisode["overview"];
                episode.Overview = episode.Overview.fixBrokenQuotes();
                episode.Backdrop = (string)jEpisode["still_path"];
                DateTime tempDate;
                episode.Date = DateTime.TryParse((string)jEpisode["air_date"], out tempDate) ? tempDate : DateTime.MinValue.AddHours(9);

                if (episode.Backdrop != null)
                {
                    await DownloadImage(episode.Backdrop, tvShow.Name, false, token);
                    episode.Backdrop = bufferString;
                }
            }
            jEpIndex++;
        }
        seasonIndex++;
    }
}
}*/
        }

        internal static string ReplaceFirst(string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }

        internal static async Task<string> DownloadImage(string imagePath, string name, bool isMovie)
        {
            string url = apiImageUrl + imagePath;
            string dirPath;
            string filePath;
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;

            if (isMovie)
            {
                dirPath = "cache\\movies\\" + name;
                filePath = dirPath + imagePath.Replace("/", "\\");
            }
            else
            {
                dirPath = "cache\\tv\\" + name;
                filePath = dirPath + imagePath.Replace("/", "\\");
            }

            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            if (!File.Exists(filePath))
            {
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, short.MaxValue, true))
                {
                    try
                    {
                        var requestUri = new Uri(url);
                        HttpClientHandler handler = new HttpClientHandler
                        {
                            PreAuthenticate = true,
                            UseDefaultCredentials = true
                        };
                        var response = await (new HttpClient(handler)).GetAsync(requestUri,
                            HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                        var content = response.EnsureSuccessStatusCode().Content;
                        await content.CopyToAsync(fileStream).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError(ex.ToString());
                    }
                }
            }

            return filePath;
        }

        #endregion

        internal static void Initialize()
        {
            string driveString = ConfigurationManager.AppSettings["Drives"];
            string[] drives = driveString.Split(';');
            foreach (string drive in drives)
            {
                ProcessRootDirectory(drive);
            }

            MainWindow.model = new MainModel(moviePathList.Count, tvPathList.Count);
            for (int i = 0; i < moviePathList.Count; i++)
            {
                MainWindow.model.Movies[i] = ProcessMovieDirectory(moviePathList[i]);
            }

            for (int i = 0; i < tvPathList.Count; i++)
            {
                MainWindow.model.TvShows[i] = ProcessTvDirectory(tvPathList[i]);
            }

            bool update = CheckForUpdates();
            if (update) BuildCache();
        }

        internal static bool CheckForUpdates()
        {
            MainModel prevMedia = null;

            if (File.Exists(jsonFile))
            {
                string jsonString = File.ReadAllText(jsonFile);
                prevMedia = Newtonsoft.Json.JsonConvert.DeserializeObject<MainModel>(jsonString);
            }

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

            return result;
        }

        internal static Movie ProcessMovieDirectory(string targetDir)
        {
            string[] movieEntry = Directory.GetFiles(targetDir);
            string[] path = movieEntry[0].Split('\\');
            string[] movieName = path[path.Length - 1].Split('.');
            Movie movie = new Movie(movieName[0].Trim(), movieEntry[0]);
            return movie;
        }

        internal static TvShow ProcessTvDirectory(string targetDir)
        {
            string[] path = targetDir.Split('\\');
            string name = path[path.Length - 1].Split('%')[0];
            TvShow show = new TvShow(name.Trim());
            string[] seasonEntries = Directory.GetDirectories(targetDir);
            Array.Sort(seasonEntries, SeasonComparer);
            show.Seasons = new Season[seasonEntries.Length];

            for (int i = 0; i < seasonEntries.Length; i++)
            {
                if (seasonEntries[i].Contains("Extras"))
                {
                    Season extras = new Season(-1);
                    List<Episode> extraEpisodes = new List<Episode>();
                    ProcessExtrasDirectory(extraEpisodes, seasonEntries[i]);
                    extras.Episodes = new Episode[extraEpisodes.Count];
                    for (int j = 0; j < extraEpisodes.Count; j++)
                    {
                        extras.Episodes[j] = extraEpisodes[j];
                    }
                    show.Seasons[show.Seasons.Length - 1] = extras;
                    continue;
                }

                if (!seasonEntries[i].Contains("Season")) continue;

                Season season = new Season(i + 1);
                string[] episodeEntries = Directory.GetFiles(seasonEntries[i]);
                Array.Sort(episodeEntries, CompareIndex);
                season.Episodes = new Episode[episodeEntries.Length];

                for (int j = 0; j < episodeEntries.Length; j++)
                {
                    string[] namePath = episodeEntries[j].Split('\\');
                    if (!episodeEntries[j].Contains('%'))
                    {
                        //("Missing separator: " + namePath);
                        NotificationDialog.Show(
                            "Cache.ProcessTvDirectory",
                            "Episode is missing separator",
                            episodeEntries[j],
                            NotifyIcon.Exclamation);
                        Environment.Exit(0);
                    }
                    string[] episodeNameNumber = namePath[namePath.Length - 1].Split(new[] { '%' }, 2);
                    int fileSuffixIndex = episodeNameNumber[1].LastIndexOf('.');
                    string episodeName = episodeNameNumber[1].Substring(0, fileSuffixIndex).Trim();
                    Episode episode = new Episode(0, episodeName, episodeEntries[j]);
                    season.Episodes[j] = episode;
                }
                show.Seasons[i] = season;
            }

            return show;
        }

        internal static void ProcessExtrasDirectory(List<Episode> extras, string targetDir)
        {
            string[] rootEntries = Directory.GetFiles(targetDir);
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

                Episode ep = new Episode(-1, episodeName, entry);
                extras.Add(ep);
            }
            string[] subDirectories = Directory.GetDirectories(targetDir);
            foreach (string subDir in subDirectories)
            {
                ProcessExtrasDirectory(extras, subDir);
            }
        }

        internal static int CompareIndex(string s1, string s2)
        {
            string[] s1Parts = s1.Split('%');
            string[] s2Parts = s2.Split('%');
            string[] s3Parts = s1Parts[s1Parts.Length - 2].Split('\\');
            string[] s4Parts = s2Parts[s2Parts.Length - 2].Split('\\');
            string s5Part = s3Parts[s3Parts.Length - 1];
            string s6Part = s4Parts[s4Parts.Length - 1];
            if (s5Part.Contains("#"))
            {
                s5Part = s5Part.Split('#')[0];
            }
            if (s6Part.Contains("#"))
            {
                s6Part = s6Part.Split('#')[0];
            }
            int indexA = Int32.Parse(s5Part);
            int indexB = Int32.Parse(s6Part);
            if (indexA == indexB)
            {
                return 0;
            }
            else if (indexA > indexB)
            {
                return 1;
            }
            else
            {
                return -1;
            }

        }

        internal static int SeasonComparer(string seasonB, string seasonA)
        {
            if (seasonB.Contains("Extras"))
            {
                return 1;
            }
            else if (seasonA.Contains("Extras"))
            {
                return -1;
            }
            string[] seasonValuePathA = seasonA.Split();
            string[] seasonValuePathB = seasonB.Split();
            int seasonValueA = Int32.Parse(seasonValuePathA[seasonValuePathA.Length - 1]);
            int seasonValueB = Int32.Parse(seasonValuePathB[seasonValuePathB.Length - 1]);
            if (seasonValueA == seasonValueB) return 0;
            if (seasonValueA < seasonValueB) return 1;
            return -1;
        }

        internal static void ProcessRootDirectory(string driveLetter)
        {
            string tvDirPath = driveLetter + ":\\media\\tv";
            if (!Directory.Exists(tvDirPath))
            {
                /*bool inputRes = InputDialog.Show(
                    "Backup and Restor center",
                    "You need to be an Administrator to run backup",
                    "Use Fast User Switching to switch to an account with administrator privileges, or log off and log on as an administrator",
                NotifyIcon.Exclamation);

                if (inputRes)
                {

                }
                else
                {
                    Environment.Exit(0);
                }*/
                NotificationDialog.Show(
                    "Cache.ProcessRootDirectory",
                    "Error",
                    "TV folder on " + driveLetter + " drive not found.",
                NotifyIcon.Exclamation);
                Environment.Exit(0);
            }

            string movieDirPath = driveLetter + ":\\media\\movie";
            if (!Directory.Exists(movieDirPath))
            {
                NotificationDialog.Show(
                    "Cache.ProcessRootDirectory",
                    "Error",
                    "Movie folder on " + driveLetter + " drive not found.",
                NotifyIcon.Exclamation);
                Environment.Exit(0);
            }

            tvPathList.AddRange(Directory.GetDirectories(tvDirPath));
            moviePathList.AddRange(Directory.GetDirectories(movieDirPath));
        }

        static public void Save()
        {
            string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(MainWindow.model);
            File.WriteAllText(jsonFile, jsonString);
            Trace.WriteLine("Media data file saved");
        }
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
    // look at that 70s show?
    public static string fixBrokenQuotes(this string str)
    {
        return str.Replace(genericSingleQuoteSymbol, targetSingleQuoteSymbol).Replace(openSingleQuoteSymbol, targetSingleQuoteSymbol)
            .Replace(closeSingleQuoteSymbol, targetSingleQuoteSymbol).Replace(frenchAccentAigu, "e").Replace(frenchAccentGrave, "a").Replace("%", "percent");
    }
}