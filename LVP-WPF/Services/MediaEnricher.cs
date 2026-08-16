using LVP_WPF.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LVP_WPF.Services
{
    /// <summary>
    /// Takes a freshly-scanned Movie or TvShow and enriches it with TMDB
    /// metadata + downloaded posters/backdrops. Also handles:
    ///   - Multi-result disambiguation via OptionDialog
    ///   - File renaming on TMDB name mismatches (with InputDialog confirm)
    ///   - Multi-language TV show translation via LibreTranslate
    ///   - Cartoon-genre detection (with CartoonExceptions config override)
    ///   - The "Tom and Jerry" / "Looney Tunes" custom-cache shortcuts
    ///
    /// Progress is reported via the optional onItemEnriched callback passed
    /// to the constructor (one tick per indexed unit of work).
    /// </summary>
    internal sealed class MediaEnricher
    {
        private readonly TmdbClient _tmdb;
        private readonly Translator _translator;
        private readonly IUserPrompts _prompts;
        private readonly Action? _onItemEnriched;
        private readonly Action? _saveCheckpoint;

        /// <param name="onItemEnriched">
        /// Optional callback fired once per indexed unit of work (per episode
        /// during season-cache builds, per episode during translations).
        /// Used by the orchestrator to drive a progress bar; the enricher
        /// itself doesn't know what "progress" means.
        /// </param>
        /// <param name="saveCheckpoint">
        /// Optional callback invoked before a blocking error dialog is shown
        /// (no TMDB match found). Lets the orchestrator persist partial
        /// progress so killing the app at the dialog doesn't lose work.
        /// </param>
        public MediaEnricher(TmdbClient tmdb, Translator translator, IUserPrompts prompts, Action? onItemEnriched = null, Action? saveCheckpoint = null)
        {
            _tmdb = tmdb;
            _translator = translator;
            _prompts = prompts;
            _onItemEnriched = onItemEnriched;
            _saveCheckpoint = saveCheckpoint;
        }

        public async Task EnrichMovieAsync(Movie movie)
        {
            if (movie.Id != 0)
            {
                return;
            }

            JObject movieObject = await _tmdb.SearchMovieAsync(movie.Name);
            int numMovieObjects = (int)movieObject["total_results"];

            if (numMovieObjects == 0)
            {
                _saveCheckpoint?.Invoke();
                _prompts.ShowError("Error", $"No movie found for: {movie.Name}");
            }
            else if (numMovieObjects != 1)
            {
                var parsed = ParseSearchResults((JArray)movieObject["results"], titleKey: "title", dateKey: "release_date");
                string[][] info = new string[][] { parsed.names, parsed.ids, parsed.overviews };
                movie.Id = _prompts.ChooseOption(movie.Name, movie.Path, info, parsed.dates);
            }
            else
            {
                movie.Id = (int)movieObject["results"][0]["id"];
            }

            movieObject = await _tmdb.GetMovieAsync(movie.Id);
            await UpdateMovieData(movie, movieObject);
        }

        // Sentinel used when a TMDB result is missing its date field; we still
        // need a parseable DateTime for downstream sorting. Picked as
        // MinValue+9h so all "unknown" dates collate together but slightly
        // above the absolute floor (the absolute floor sometimes confuses
        // formatting code that special-cases it).
        private static readonly DateTime UnknownDateSentinel = DateTime.MinValue.AddHours(9);

        /// <summary>
        /// Flatten a TMDB search-results JArray into the four parallel arrays
        /// the OptionDialog prompt expects. EnrichMovieAsync and
        /// BuildTvShowGeneralData both call this; their only differences are
        /// the JSON keys for the display title and the date field
        /// ("title"/"release_date" vs "name"/"first_air_date").
        /// </summary>
        private static (string[] names, string[] ids, string[] overviews, DateTime?[] dates) ParseSearchResults(
            JArray results, string titleKey, string dateKey)
        {
            int count = results.Count;
            string[] names = new string[count];
            string[] ids = new string[count];
            string[] overviews = new string[count];
            DateTime?[] dates = new DateTime?[count];

            for (int j = 0; j < count; j++)
            {
                names[j] = ((string)results[j][titleKey]).FixBrokenQuotes();
                ids[j] = (string)results[j]["id"];
                overviews[j] = ((string)results[j]["overview"]).FixBrokenQuotes();
                dates[j] = DateTime.TryParse((string)results[j][dateKey], out DateTime t) ? t : UnknownDateSentinel;
            }
            return (names, ids, overviews, dates);
        }

        public async Task EnrichTvShowAsync(TvShow tvShow)
        {
            // Two shows have custom-bundled metadata (filmography.csv shipped
            // alongside the videos); they skip the TMDB path entirely.
            if (tvShow.Name.Equals("Tom & Jerry"))
            {
                CustomCache.BuildTomAndJerryData(tvShow);
                return;
            }
            if (tvShow.Name.Equals("Looney Tunes"))
            {
                CustomCache.BuildLooneyTunesData(tvShow);
                return;
            }

            if (tvShow.Id == 0)
            {
                await BuildTvShowGeneralData(tvShow);
            }
            await BuildSeasonCache(tvShow);

            if (tvShow.MultiLang)
            {
                for (int i = 0; i < tvShow.MultiLangSeasons.Count; i++)
                {
                    Season[] currSeason = tvShow.Seasons;
                    tvShow.Seasons = tvShow.MultiLangSeasons[i];
                    // Build season cache again - some translated shows have different episode counts per season.
                    await BuildSeasonCache(tvShow);
                    tvShow.MultiLangSeasons[i] = tvShow.Seasons;
                    tvShow.Seasons = currSeason;
                }
                await ApplyMultiLangTvShowTranslations(tvShow);
            }
        }

        private async Task BuildTvShowGeneralData(TvShow tvShow)
        {
            JObject tvObject = await _tmdb.SearchTvAsync(tvShow.Name);
            int totalResults = (int)tvObject["total_results"];

            if (totalResults == 0)
            {
                _saveCheckpoint?.Invoke();
                _prompts.ShowError("Error", $"No tv show found for: {tvShow.Name}");
            }
            else if (totalResults != 1)
            {
                var parsed = ParseSearchResults((JArray)tvObject["results"], titleKey: "name", dateKey: "first_air_date");
                string[][] info = new string[][] { parsed.names, parsed.ids, parsed.overviews };
                tvShow.Id = _prompts.ChooseOption(tvShow.Name, tvShow.Seasons[0].Episodes[0].Path, info, parsed.dates);
            }
            else
            {
                tvShow.Id = (int)tvObject["results"][0]["id"];
            }

            tvObject = await _tmdb.GetTvShowAsync(tvShow.Id);

            tvShow.Date = DateTime.TryParse((string)tvObject["first_air_date"], out DateTime tempDate) ? tempDate : UnknownDateSentinel;
            tvShow.Overview = (string)tvObject["overview"];
            tvShow.Overview = tvShow.Overview.FixBrokenQuotes();
            tvShow.Poster = (string)tvObject["poster_path"];
            tvShow.Backdrop = (string)tvObject["backdrop_path"];
            int[] runtime = tvObject["episode_run_time"].Select(x => (int)x).ToArray();
            tvShow.RunningTime = runtime.Length != 0 ? runtime[0] : -1;

            JToken? genres = tvObject["genres"];
            foreach (JToken? genre in genres)
            {
                if ((int)genre["id"] == 16 && !AppConfig.CartoonExceptions.Contains(tvShow.Name))
                {
                    tvShow.Cartoon = true;
                }
            }

            if (AppConfig.ForceCartoons.Contains(tvShow.Name))
            {
                tvShow.Cartoon = true;
            }

            if (tvShow.Backdrop != null)
            {
                tvShow.Backdrop = await _tmdb.DownloadImageAsync(tvShow.Backdrop, false, tvShow.Name);
            }

            if (tvShow.Poster != null)
            {
                tvShow.Poster = await _tmdb.DownloadImageAsync(tvShow.Poster, false, tvShow.Name);
            }
        }

        private async Task BuildSeasonCache(TvShow tvShow)
        {
            int seasonIndex = 0;
            for (int j = 0; j < tvShow.Seasons.Length; j++)
            {
                Season season = tvShow.Seasons[j];
                if (season.Id == -1)
                {
                    continue;
                }

                JObject seasonObject = await _tmdb.GetTvSeasonAsync(tvShow.Id, seasonIndex);
                // TMDB returns { "success": false, "status_code": 34, ... } when the season isn't there.
                // Some shows are 1-indexed; retry from season 1 if 0 came back empty.
                if (seasonObject["success"] != null && (bool)seasonObject["success"] == false)
                {
                    seasonIndex = 1;
                    seasonObject = await _tmdb.GetTvSeasonAsync(tvShow.Id, seasonIndex);
                }

                try
                {
                    if (((string)seasonObject["name"]).Contains("Specials"))
                    {
                        seasonIndex++;
                        seasonObject = await _tmdb.GetTvSeasonAsync(tvShow.Id, seasonIndex);
                    }
                }
                catch
                {
                    _prompts.ShowError("Error", $"Season first index error: {tvShow.Name}, ID = {tvShow.Id}");
                }

                if (season.Poster == null)
                {
                    season.Poster = (string)seasonObject["poster_path"];
                    season.Date = DateTime.TryParse((string)seasonObject["air_date"], out DateTime tempDate) ? tempDate : UnknownDateSentinel;

                    if (season.Poster != null)
                    {
                        season.Poster = await _tmdb.DownloadImageAsync(season.Poster, false, tvShow.Name);
                    }
                }

                JArray jEpisodes = (JArray)seasonObject["episodes"];

                // Index TMDB episodes by episode_number so disk files map to
                // TMDB by the number encoded in their filename, NOT by array
                // position. The previous positional walk silently misaligned
                // whenever disk had missing episodes - e.g. SNL S08 with E18
                // missing left the disk file numbered "19%..." at position 17,
                // and the old code handed it TMDB's E18 record. Dictionary
                // lookup makes the mapping intent-driven: disk says E19, we
                // fetch TMDB E19; we cleanly skip the TMDB-only E18.
                Dictionary<int, JObject> jByNum = new Dictionary<int, JObject>(jEpisodes.Count);
                foreach (JToken t in jEpisodes)
                {
                    jByNum[(int)t["episode_number"]] = (JObject)t;
                }

                Episode[] episodes = season.Episodes;
                for (int k = 0; k < episodes.Length; k++)
                {
                    // Already enriched on a previous run (loaded from
                    // media.json with Id stamped from TMDB episode_number).
                    if (episodes[k].Id != 0) continue;

                    Episode episode = episodes[k];
                    int diskEpNum;
                    try
                    {
                        diskEpNum = ExtractDiskEpisodeNumber(episode.Path);
                    }
                    catch
                    {
                        _prompts.ShowError($"Error: {tvShow.Name}",
                            $"Could not parse episode number from filename '{Path.GetFileName(episode.Path)}' (Season {seasonIndex}).");
                        continue;
                    }

                    if (episode.Name.Contains('#'))
                    {
                        await EnrichMultiEpisode(tvShow, season, episode, jByNum, diskEpNum, seasonIndex);
                        // Match prior behavior: no progress tick on the
                        // multi-episode advance.
                        continue;
                    }

                    if (!jByNum.TryGetValue(diskEpNum, out JObject jEpisode))
                    {
                        _prompts.ShowError($"Error: {tvShow.Name}",
                            $"Disk episode E{diskEpNum:D2} has no match on TMDB (Season {seasonIndex} has {jByNum.Count} episodes).");
                        continue;
                    }

                    await EnrichSingleEpisode(tvShow, season, episode, jEpisode);
                    _onItemEnriched?.Invoke();
                }
                seasonIndex++;
            }
        }

        // Mirrors LibraryScanner.ExtractEpisodeIndex - parses the episode
        // number prefix from a "N%Title.ext" or "N#suffix%Title.ext" path.
        // Inlined here (not exposed from the scanner) because it's only the
        // enricher that needs it and we don't want to widen the scanner's
        // public surface for one caller. Throws FormatException on a bad
        // prefix; the caller is expected to catch and show a UI error.
        private static int ExtractDiskEpisodeNumber(string path)
        {
            string prefix = Path.GetFileName(path).Split('%')[0];
            int hash = prefix.IndexOf('#');
            if (hash >= 0) prefix = prefix.Substring(0, hash);
            return int.Parse(prefix);
        }

        // Enrich a "two-parter" episode (local name contains '#' joining
        // multiple TMDB episode names, e.g. "PilotA#PilotB.mkv"). Looks up
        // each part by consecutive episode_number starting at the disk
        // file's parsed prefix, handles per-part renames, and stamps the
        // merged Overview onto the local episode.
        //
        // Pre-refactor this took (JArray jEpisodes, int jEpIndex) and
        // returned the new jEpIndex - that positional contract is gone now
        // that BuildSeasonCache uses dictionary lookup. The dictionary +
        // start-number form is positionless: a multi-part on disk numbered
        // "12#13%..." asks for tmdb[12] and tmdb[13] directly, even if
        // either is missing or out of order elsewhere in the season.
        private async Task EnrichMultiEpisode(TvShow tvShow, Season season, Episode episode,
            Dictionary<int, JObject> jByNum, int startEpNum, int seasonIndex)
        {
            string[] multiEpNames = episode.Name.Split('#');
            int numEps = multiEpNames.Length;
            JObject[] jEpisodesMulti = new JObject[numEps];
            string multiEpisodeOverview = "";

            for (int l = 0; l < numEps; l++)
            {
                if (!jByNum.TryGetValue(startEpNum + l, out JObject jEp))
                {
                    _prompts.ShowError($"Error: {tvShow.Name}",
                        $"Multi-episode part E{startEpNum + l:D2} not on TMDB (Season {seasonIndex}). Bailing on this part-set; metadata not stamped.");
                    return;
                }
                jEpisodesMulti[l] = jEp;
                string jName = (string)jEp["name"];
                string jOverview = (string)jEp["overview"];
                string localName = multiEpNames[l];

                if (!localName.MatchesLoosely(jName.FixBrokenQuotes()))
                {
                    _prompts.ShowNotice($"Warning: {tvShow.Name}",
                        $"Multi episode name does not match retrieved data: Renaming file: '{localName}', to: '{jName.FixBrokenQuotes()}' (Season {season.Id}).",
                        tvShow, season.Id + 1, episode.Path);

                    string oldPath = episode.Path;
                    string newPath = oldPath.Replace(localName, jName.FixBrokenQuotes());
                    newPath = FinalizeRenamedPath(newPath, handleUncPaths: false);
                    if (TryMoveAndUpdateSubtitle(oldPath, newPath, tvShow, season))
                    {
                        episode.Path = newPath;
                    }
                }
                multiEpisodeOverview += jOverview + Environment.NewLine + Environment.NewLine;
            }

            JObject last = jEpisodesMulti[numEps - 1];
            episode.Date = DateTime.TryParse((string)last["air_date"], out DateTime mDate) ? mDate : UnknownDateSentinel;
            episode.Id = (int)last["episode_number"];
            episode.Backdrop = (string)last["still_path"];
            episode.Overview = multiEpisodeOverview;

            if (episode.Backdrop != null)
            {
                episode.Backdrop = await _tmdb.DownloadImageAsync(episode.Backdrop, false, tvShow.Name);
            }
        }

        // Enrich a normal one-to-one episode against its matching TMDB entry.
        // Handles the name-mismatch rename (which updates episode.Path/Name
        // unconditionally - see TryMoveAndUpdateSubtitle's caller note).
        private async Task EnrichSingleEpisode(TvShow tvShow, Season season, Episode episode, JObject jEpisode)
        {
            string jName = (string)jEpisode["name"];
            if (!episode.Name.MatchesLoosely(jName.FixBrokenQuotes()))
            {
                _prompts.ShowNotice($"Warning: {tvShow.Name}",
                    $"Local episode name does not match retrieved data. Renaming file '{episode.Name}' to '{jName.FixBrokenQuotes()}' (Season {season.Id}).",
                    tvShow, season.Id + 1, episode.Path);

                string oldPath = episode.Path;
                string newPath = ReplaceLastOccurrence(oldPath, episode.Name, jName.FixBrokenQuotes());
                newPath = FinalizeRenamedPath(newPath, handleUncPaths: true);
                TryMoveAndUpdateSubtitle(oldPath, newPath, tvShow, season);
                episode.Path = newPath;
                episode.Name = jName.FixBrokenQuotes();
            }

            episode.Date = DateTime.TryParse((string)jEpisode["air_date"], out DateTime tempDate) ? tempDate : UnknownDateSentinel;
            episode.Id = (int)jEpisode["episode_number"];
            episode.Overview = ((string)jEpisode["overview"]).FixBrokenQuotes();
            episode.Backdrop = (string)jEpisode["still_path"];

            if (episode.Backdrop != null)
            {
                episode.Backdrop = await _tmdb.DownloadImageAsync(episode.Backdrop, false, tvShow.Name);
            }
        }

        private async Task UpdateMovieData(Movie movie, JObject movieObject)
        {
            if (!movie.Name.Replace(":", "").MatchesLoosely(((string)movieObject["title"]).Replace(":", "").FixBrokenQuotes()))
            {
                string message = $"Local movie name does not match retrieved data. Renaming file '{movie.Name.Replace(":", "")}' to '{((string)movieObject["title"]).Replace(":", "")}'.";
                _prompts.ShowNotice("Warning", message, null, 0, movie.Path);
                string oldPath = movie.Path;
                string dir = Path.GetDirectoryName(oldPath) ?? "";
                string extension = Path.GetExtension(oldPath); // includes leading "."
                string newFileName = ((string)movieObject["title"]).Replace(":", "").FixBrokenQuotes();
                string newPath = Path.Combine(dir, $"{newFileName}{extension}");
                newPath = StripInvalidPathChars(newPath, "?");
                File.Move(oldPath, newPath);
                movie.Path = newPath;
                movie.Name = newFileName;
            }

            movie.Date = DateTime.TryParse((string)movieObject["release_date"], out DateTime tempDate) ? tempDate : UnknownDateSentinel;
            movie.Backdrop = (string)movieObject["backdrop_path"];
            movie.Poster = (string)movieObject["poster_path"];
            movie.Overview = (string)movieObject["overview"];
            movie.Overview = movie.Overview.FixBrokenQuotes();
            movie.RunningTime = (int)movieObject["runtime"];

            if (movie.Backdrop != null)
            {
                movie.Backdrop = await _tmdb.DownloadImageAsync(movie.Backdrop, true, movie.Name);
            }

            if (movie.Poster != null)
            {
                movie.Poster = await _tmdb.DownloadImageAsync(movie.Poster, true, movie.Name);
            }
        }

        private async Task ApplyMultiLangTvShowTranslations(TvShow tvShow)
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

                string langKey = Path.GetFileName(lang[i]);

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
                                string overview = await _translator.TranslateAsync(langKey, tvShow.Overview);
                                if (!tvShow.MultiLangOverview.Contains(overview))
                                {
                                    tvShow.MultiLangOverview.Add(overview);
                                }
                            }

                            multiLangSeason.Episodes[l].Name = await _translator.TranslateAsync(langKey, multiLangSeason.Episodes[l].Name);
                            multiLangSeason.Episodes[l].Overview = await _translator.TranslateAsync(langKey, multiLangSeason.Episodes[l].Overview);
                            _onItemEnriched?.Invoke();
                            multiLangSeason.Episodes[l].Translated = true;
                        }
                    }
                }
            }
        }

        private void CheckSubtitleName(TvShow tvShow, Season season, string oldPath, string newPath)
        {
            if (!tvShow.MultiLang) return;

            string oldSrtPath = Path.ChangeExtension(oldPath, ".srt");
            if (File.Exists(oldSrtPath))
            {
                string newSrtPath = Path.ChangeExtension(newPath, ".srt");
                string subMsg = $"Renaming subtitle file {Path.GetFileName(oldSrtPath)} to {Path.GetFileName(newSrtPath)} (Season {season.Id}).";
                _prompts.ShowNotice($"Warning: {tvShow.Name}", subMsg, tvShow, season.Id + 1, oldSrtPath);
                File.Move(oldSrtPath, newSrtPath);
            }
            else if (!oldPath.Contains("\\en\\"))
            {
                _prompts.ShowError("Error", $"No subtitle file found {oldSrtPath} (Season {season.Id}).");
            }
        }

        /// <summary>
        /// Drop characters from <paramref name="path"/> that File.Move would
        /// reject. Path.GetInvalidPathChars() doesn't include '?', ':', '*'
        /// on Windows because they're legal in path *strings* (':' is the
        /// drive separator), so callers pass the extras they want stripped.
        /// Episode renames strip '?', ':', '*' and reconstruct the drive
        /// colon afterward; movie renames keep ':' intact and only strip '?'.
        /// </summary>
        private static string StripInvalidPathChars(string path, string extraChars)
        {
            string toStrip = new string(Path.GetInvalidPathChars()) + extraChars;
            foreach (char c in toStrip)
            {
                path = path.Replace(c.ToString(), "");
            }
            return path;
        }

        // Episode-rename pipeline used by both the single- and multi-episode
        // mismatch handlers: strip "?:*" out of the new path, then put the
        // drive colon back. handleUncPaths==true preserves a leading "\\"
        // (UNC) as a single backslash; the multi-episode path didn't bother
        // with this and we preserve that.
        private static string FinalizeRenamedPath(string newPath, bool handleUncPaths)
        {
            newPath = StripInvalidPathChars(newPath, "?:*");
            char drive = newPath[0];
            string drivePath = handleUncPaths && drive == '\\' ? $"{drive}" : $"{drive}:";
            return ReplaceFirst(newPath, drive.ToString(), drivePath);
        }

        // Move oldPath -> newPath; if it works, update any companion .srt
        // alongside it. Returns true on success, false on failure (after
        // showing the user the error message).
        private bool TryMoveAndUpdateSubtitle(string oldPath, string newPath, TvShow tvShow, Season season)
        {
            try
            {
                File.Move(oldPath, newPath);
                CheckSubtitleName(tvShow, season, oldPath, newPath);
                return true;
            }
            catch (Exception e)
            {
                _prompts.ShowError("Error", e.Message);
                return false;
            }
        }

        private static string ReplaceFirst(string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0) return text;
            return string.Concat(text.AsSpan(0, pos), replace, text.AsSpan(pos + search.Length));
        }

        private static string ReplaceLastOccurrence(string source, string find, string replace)
        {
            int place = source.LastIndexOf(find);
            if (place == -1) return source;
            return source.Remove(place, find.Length).Insert(place, replace);
        }
    }
}
