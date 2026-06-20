using System;
using System.Collections.Generic;
using System.Linq;
namespace LVP_WPF
{
    public class MainModel
    {
        public MainModel(int m, int s)
        {
            Movies = new Movie[m];
            TvShows = new TvShow[s];
            HistoryList = new List<Episode>();
        }

        public Movie[] Movies { get; set; }
        public TvShow[] TvShows { get; set; }
        public List<Episode> HistoryList { get; set; }

        public int HistoryIndex { get; set; }
        public DateTime HistoryMin { get; set; }
        public DateTime HistoryMax { get; set; }
        public Episode HistoryEpisode { get; set; }

        internal bool Compare(MainModel prevMedia)
        {
            if (this.Movies.Length != prevMedia.Movies.Length) return false;
            if (this.TvShows.Length != prevMedia.TvShows.Length) return false;

            // Match by Path (case-insensitive). Array order isn't reliable here:
            // this.Movies[i].Name is filename-derived (what the scanner just saw),
            // prevMedia.Movies[i].Name is TMDB-derived (what we wrote on the last
            // save), so the two arrays may sort to different orders under the
            // same Name comparer. Path is the canonical join key, and
            // OrdinalIgnoreCase tolerates drive/folder case drift across boots
            // (e.g. E_media vs E_Media on the same NTFS volume).
            Dictionary<string, Movie> prevMoviesByPath =
                prevMedia.Movies.ToDictionary(m => m.Path, StringComparer.OrdinalIgnoreCase);
            foreach (Movie curr in this.Movies)
            {
                if (!prevMoviesByPath.TryGetValue(curr.Path, out Movie? prev))
                {
                    Serilog.Log.Warning("Compare miss: curr.Path = {Path}", curr.Path);
                    Serilog.Log.Warning("  Length={Len}, ends with: '{Tail}'",
                        curr.Path.Length, curr.Path.Length >= 20 ? curr.Path[^20..] : curr.Path);

                    // Find closest key by common prefix and show where they diverge.
                    string? best = null;
                    int bestPrefix = -1;
                    foreach (string key in prevMoviesByPath.Keys)
                    {
                        int p = 0;
                        int maxP = Math.Min(key.Length, curr.Path.Length);
                        while (p < maxP && char.ToLowerInvariant(key[p]) == char.ToLowerInvariant(curr.Path[p])) p++;
                        if (p > bestPrefix) { bestPrefix = p; best = key; }
                    }
                    if (best != null)
                    {
                        Serilog.Log.Warning("  Closest prev key: {Key}", best);
                        Serilog.Log.Warning("  Common prefix length: {N} (curr.Len={C}, prev.Len={P})",
                            bestPrefix, curr.Path.Length, best.Length);
                        if (bestPrefix < curr.Path.Length)
                            Serilog.Log.Warning("  curr diverges at idx {I}: '{C}' (U+{Cx:X4})",
                                bestPrefix, curr.Path[bestPrefix], (int)curr.Path[bestPrefix]);
                        if (bestPrefix < best.Length)
                            Serilog.Log.Warning("  prev diverges at idx {I}: '{C}' (U+{Cx:X4})",
                                bestPrefix, best[bestPrefix], (int)best[bestPrefix]);
                    }
                    else
                    {
                        Serilog.Log.Warning("  prevMoviesByPath is EMPTY");
                    }
                    return false;
                }
                if (!curr.Compare(prev)) return false;
            }

            Dictionary<string, TvShow> prevShowsByPath =
                prevMedia.TvShows.ToDictionary(t => t.Path, StringComparer.OrdinalIgnoreCase);
            foreach (TvShow curr in this.TvShows)
            {
                if (!prevShowsByPath.TryGetValue(curr.Path, out TvShow? prev))
                {
                    Serilog.Log.Warning("Compare miss: curr.Path = {Path}", curr.Path);
                    Serilog.Log.Warning("  Length={Len}, ends with: '{Tail}'",
                        curr.Path.Length, curr.Path.Length >= 20 ? curr.Path[^20..] : curr.Path);

                    // Find closest key by common prefix and show where they diverge.
                    string? best = null;
                    int bestPrefix = -1;
                    foreach (string key in prevShowsByPath.Keys)
                    {
                        int p = 0;
                        int maxP = Math.Min(key.Length, curr.Path.Length);
                        while (p < maxP && char.ToLowerInvariant(key[p]) == char.ToLowerInvariant(curr.Path[p])) p++;
                        if (p > bestPrefix) { bestPrefix = p; best = key; }
                    }
                    if (best != null)
                    {
                        Serilog.Log.Warning("  Closest prev key: {Key}", best);
                        Serilog.Log.Warning("  Common prefix length: {N} (curr.Len={C}, prev.Len={P})",
                            bestPrefix, curr.Path.Length, best.Length);
                        if (bestPrefix < curr.Path.Length)
                            Serilog.Log.Warning("  curr diverges at idx {I}: '{C}' (U+{Cx:X4})",
                                bestPrefix, curr.Path[bestPrefix], (int)curr.Path[bestPrefix]);
                        if (bestPrefix < best.Length)
                            Serilog.Log.Warning("  prev diverges at idx {I}: '{C}' (U+{Cx:X4})",
                                bestPrefix, best[bestPrefix], (int)best[bestPrefix]);
                    }
                    else
                    {
                        Serilog.Log.Warning("  prevShowsByPath is EMPTY");
                    }
                    return false;
                }
                if (!curr.Compare(prev)) 
                    return false;
            }

            return true;
        }

        // ----- Ingest -----
        //
        // After a fresh scan, copy over the TMDB-enrichment data (Ids, posters,
        // overviews, saved playback positions, etc.) from the persisted model
        // to the freshly scanned one, matching by file path.
        //
        // This used to be one big O(n^2) method with copy-paste property
        // assignments triplicated across Movie/TvShow/Season/Episode and a
        // separate MultiLang variant. The per-class field lists now live on
        // each class as CopyFrom helpers; this method just wires the matches.

        internal void Ingest(MainModel prevMedia)
        {
            Dictionary<string, Movie> prevMoviesByPath = prevMedia.Movies.ToDictionary(m => m.Path, StringComparer.OrdinalIgnoreCase);
            foreach (Movie curr in this.Movies)
            {
                if (prevMoviesByPath.TryGetValue(curr.Path, out Movie? prev))
                {
                    curr.CopyFrom(prev);
                }
            }

            Dictionary<string, TvShow> prevShowsByPath = prevMedia.TvShows.ToDictionary(t => t.Path, StringComparer.OrdinalIgnoreCase);
            foreach (TvShow curr in this.TvShows)
            {
                if (!prevShowsByPath.TryGetValue(curr.Path, out TvShow? prev))
                {
                    continue;
                }

                curr.CopyFrom(prev);
                IngestSeasonsByIndex(curr.Seasons, prev.Seasons, includeTranslated: false);

                if (curr.MultiLang)
                {
                    curr.CopyMultiLangFrom(prev);
                    for (int a = 0; a < prev.MultiLangSeasons.Count; a++)
                    {
                        IngestSeasonsByIndex(curr.MultiLangSeasons[a], prev.MultiLangSeasons[a], includeTranslated: true);
                    }
                }
            }
        }

        // Position-indexed season/episode ingest. Episodes must line up by
        // index (the scanner and the saved JSON both sort by the %N% prefix),
        // and a safety check skips mismatches if an episode was added/removed
        // in the middle - that's the original behavior, preserved here.
        // The matching key differs for multi-lang (file name) vs single-lang
        // (episode name); both are still index-based.
        private static void IngestSeasonsByIndex(Season[] currSeasons, Season[] prevSeasons, bool includeTranslated)
        {
            int seasonCount = Math.Min(currSeasons.Length, prevSeasons.Length);
            for (int j = 0; j < seasonCount; j++)
            {
                currSeasons[j].CopyFrom(prevSeasons[j]);

                Episode[] currEps = currSeasons[j].Episodes;
                Episode[] prevEps = prevSeasons[j].Episodes;
                int epCount = Math.Min(currEps.Length, prevEps.Length);
                for (int k = 0; k < epCount; k++)
                {
                    if (includeTranslated)
                    {
                        if (EpisodeFileNamesMatch(currEps[k].Path, prevEps[k].Path))
                        {
                            currEps[k].CopyFrom(prevEps[k], includeTranslated: true);
                        }
                    }
                    else
                    {
                        if (currEps[k].Name.Equals(prevEps[k].Name))
                        {
                            currEps[k].CopyFrom(prevEps[k], includeTranslated: false);
                        }
                    }
                }
            }
        }

        private static bool EpisodeFileNamesMatch(string currPath, string prevPath)
            => System.IO.Path.GetFileName(currPath).Equals(System.IO.Path.GetFileName(prevPath), StringComparison.OrdinalIgnoreCase);

        // Truncation-aware path equality used by Compare paths.
        //
        // Release builds: strict case-insensitive ordinal compare.
        //
        // DEBUG builds: also accept the "period-truncation" symptom where a
        // file copy from the media server chopped names at an internal
        // period (e.g. "Goku vs. Vegeta.mp4" landed as "Goku vs.mp4").
        // Accept the pair when the directories + extensions are equal AND
        // one filename body is a prefix of the other AND the next char in
        // the longer one is '.', i.e. the divergence is exactly at a
        // period (the actual truncation signature, not arbitrary shared
        // prefix). Centralized so Episode.Compare and CompareSeasonsByPath
        // stay in lockstep; deleting the #if DEBUG block restores strict
        // matching everywhere in one place.
        internal static bool PathsMatch(string a, string b)
        {
            if (a.Equals(b, StringComparison.OrdinalIgnoreCase)) return true;
#if DEBUG
            string aDir = System.IO.Path.GetDirectoryName(a) ?? "";
            string bDir = System.IO.Path.GetDirectoryName(b) ?? "";
            if (!aDir.Equals(bDir, StringComparison.OrdinalIgnoreCase))
                return false;

            string aExt = System.IO.Path.GetExtension(a);
            string bExt = System.IO.Path.GetExtension(b);

#if !DEBUG
            if (!aExt.Equals(bExt, StringComparison.OrdinalIgnoreCase))
                return false;
#endif

            string aName = System.IO.Path.GetFileNameWithoutExtension(a);
            string bName = System.IO.Path.GetFileNameWithoutExtension(b);
            string shorter = aName.Length <= bName.Length ? aName : bName;
            string longer = aName.Length <= bName.Length ? bName : aName;

            /*bool res = shorter.Length > 0
                && longer.Length > shorter.Length
                && longer.StartsWith(shorter, StringComparison.OrdinalIgnoreCase)
                && longer[shorter.Length] == '.';*/

            bool res = MatchAfterCopyArtifacts(shorter, longer);
            if (!res)
            {
                Serilog.Log.Information("HERE");
            }

            return res;
#else
            return false;
#endif
        }

        // Loose match used only on the dev workstation where a file copy from
        // the media server (a) truncated some names at an internal period
        // (treating '.' as an extension delimiter), and (b) stripped certain
        // symbols mid-name (commas, apostrophes, etc.). 'shorter' is the copy
        // result; 'longer' is the original from the server's persisted JSON.
        // Returns true when shorter could plausibly have been derived from
        // longer by either artifact.
        private static bool MatchAfterCopyArtifacts(string shorter, string longer)
        {
#if DEBUG
            if (shorter.Length == 0) return LogFail("empty shorter", shorter, longer, -1, -1);

            int i = 0;
            string failReason = "";

            foreach (char c in longer)
            {
                if (i >= shorter.Length)
                {
                    if (c == '.') return true;
                    return LogFail($"shorter exhausted; longer[{i}]=U+{(int)c:X4} not '.'",
                        shorter, longer, i, (int)c);
                }

                char sc = shorter[i];

                // 1. Exact match (case-insensitive).
                if (char.ToLowerInvariant(c) == char.ToLowerInvariant(sc))
                {
                    i++;
                    continue;
                }

                // 2. Both non-alphanumeric at the same position - treat as equivalent.
                //    Covers "comma replaced with space" and friends, where the copy
                //    tool substituted one punctuation/whitespace for another.
                if (!char.IsLetterOrDigit(c) && !char.IsLetterOrDigit(sc))
                {
                    i++;
                    continue;
                }

                // 3. longer has an extra non-alphanumeric the copy dropped from shorter.
                if (!char.IsLetterOrDigit(c))
                {
                    continue;  // skip the longer char, do NOT advance i
                }

                // 4. letter/digit divergence: real difference, bail.
                return LogFail(
                    $"letter/digit mismatch: longer has U+{(int)c:X4} ('{c}'), shorter[{i}]=U+{(int)sc:X4} ('{sc}')",
                    shorter, longer, i, (int)c);
            }

            if (i == shorter.Length) return true;
            failReason = $"ran out of longer with i={i} still < shorter.Length={shorter.Length}";
            return LogFail(failReason, shorter, longer, i, -1);
#else
    bool res = shorter.Length > 0
        && longer.StartsWith(shorter, StringComparison.OrdinalIgnoreCase)
        && (longer.Length == shorter.Length || longer[shorter.Length] == '.');
    return res;
#endif
        }

#if DEBUG
        private static bool LogFail(string reason, string shorter, string longer, int i, int badChar)
        {
            Serilog.Log.Information("MatchAfterCopyArtifacts FAIL: {Reason}", reason);
            Serilog.Log.Information("  shorter='{S}' (len {SL})", shorter, shorter.Length);
            Serilog.Log.Information("  longer ='{L}' (len {LL})", longer, longer.Length);
            Serilog.Log.Information("  shorter codepoints: {Hex}",
                string.Join(" ", shorter.Select(ch => ((int)ch).ToString("X4"))));
            Serilog.Log.Information("  longer  codepoints: {Hex}",
                string.Join(" ", longer.Select(ch => ((int)ch).ToString("X4"))));
            return false;
        }
#endif
    }

    public class Media
    {
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
            => this.Path.Equals(localMovie.Path, StringComparison.OrdinalIgnoreCase);

        /// <summary>Copy the TMDB-enrichment fields from <paramref name="other"/> onto this Movie.</summary>
        internal void CopyFrom(Movie other)
        {
            Name = other.Name;
            Overview = other.Overview;
            Path = other.Path;
            Poster = other.Poster;
            Id = other.Id;
            Date = other.Date;
            Backdrop = other.Backdrop;
            RunningTime = other.RunningTime;
        }

        public static IComparer<Movie> SortMoviesAlphabetically()
            => Comparer<Movie>.Create((a, b) => string.Compare(a.Name, b.Name));
    }


    public class TvShow : Media
    {
        public TvShow(string n, string p)
        {
            Name = n;
            CurrSeason = 1;
            Cartoon = false;
            MultiLang = false;
            Path = p;
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

        /// <summary>
        /// Flip this show into multi-language mode and allocate the parallel
        /// per-language lists. The scanner calls this once when it detects a
        /// show directory whose immediate children are 2-letter language codes
        /// instead of season folders.
        /// </summary>
        internal void EnableMultiLang()
        {
            MultiLang = true;
            MultiLangName = new List<string>();
            MultiLangOverview = new List<string>();
            MultiLangSeasons = new List<Season[]>();
            MultiLangCurrSeason = new List<int>();
            MultiLangLastWatched = new List<Episode>();
        }

        /// <summary>
        /// Locate the episode within this show's seasons. Returns the Id of
        /// the containing season (1..N for regular seasons, -1 for the
        /// Extras pseudo-season), or null if the episode isn't found.
        /// Matches episodes by Name.
        /// </summary>
        internal int? FindSeasonIdOf(Episode episode)
        {
            for (int i = 0; i < Seasons.Length; i++)
            {
                Season season = Seasons[i];
                for (int j = 0; j < season.Episodes.Length; j++)
                {
                    if (episode.Name.Equals(season.Episodes[j].Name))
                    {
                        return season.Id;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Pick the next episode to play after <paramref name="current"/>.
        /// Walks forward within the current season; if at the end of the
        /// season, advances to the first episode of the next season.
        /// Returns null when there's nothing left (already on the last
        /// regular season, or only Extras remain).
        /// </summary>
        internal Episode? GetNextEpisode(Episode current, out bool seasonChanged)
        {
            seasonChanged = false;
            for (int i = 0; i < Seasons.Length; i++)
            {
                Season season = Seasons[i];
                for (int j = 0; j < season.Episodes.Length; j++)
                {
                    if (!current.Name.Equals(season.Episodes[j].Name)) continue;

                    if (j < season.Episodes.Length - 1)
                    {
                        // Still within this season - just step forward.
                        Episode next = season.Episodes[j + 1];
                        Serilog.Log.Debug("GetNextEpisode: '{Show}' S{Sn}E{From} '{FromName}' -> E{To} '{ToName}'",
                            Name, season.Id, current.Id, current.Name, next.Id, next.Name);
                        return next;
                    }

                    // End of this season. Walk forward looking for a
                    // non-empty regular season. Three terminal cases:
                    //   - hit end of Seasons array       -> end of show
                    //   - hit Extras (Id == -1)          -> end of regular content
                    //   - skip over an empty season      -> walks past it,
                    //                                       logs a warning,
                    //                                       keeps searching
                    // Previously the code only checked the IMMEDIATELY next
                    // season and would crash on Seasons[nextSeasonIdx].Episodes[0]
                    // if that season had a folder but no episodes (empty
                    // "Season 2" directory between Season 1 and Season 3 -
                    // happens with incomplete downloads). Now it walks past
                    // empty seasons cleanly.
                    int nextSeasonIdx = i + 1;
                    while (nextSeasonIdx < Seasons.Length)
                    {
                        Season nextSeason = Seasons[nextSeasonIdx];
                        if (nextSeason.Id == -1)
                        {
                            Serilog.Log.Information("GetNextEpisode: '{Show}' S{Sn}E{Ep} '{Name}' - END OF SHOW (next slot is Extras, no more regular content)",
                                Name, season.Id, current.Id, current.Name);
                            return null;
                        }
                        if (nextSeason.Episodes == null || nextSeason.Episodes.Length == 0)
                        {
                            Serilog.Log.Warning("GetNextEpisode: '{Show}' S{Sn} has no episodes on disk - skipping",
                                Name, nextSeason.Id);
                            nextSeasonIdx++;
                            continue;
                        }

                        seasonChanged = true;
                        Episode firstOfNext = nextSeason.Episodes[0];
                        Serilog.Log.Information("GetNextEpisode: '{Show}' season change S{From} -> S{To}, opening '{NextName}'",
                            Name, season.Id, nextSeason.Id, firstOfNext.Name);
                        return firstOfNext;
                    }

                    Serilog.Log.Information("GetNextEpisode: '{Show}' S{Sn}E{Ep} '{Name}' - END OF SHOW (no more seasons - was on last regular season)",
                        Name, season.Id, current.Id, current.Name);
                    return null;
                }
            }
            Serilog.Log.Warning("GetNextEpisode: '{Show}' current ep '{Name}' not found in any season ({TotalSeasons} seasons searched) - returning null",
                Name, current.Name, Seasons.Length);
            return null;
        }

        /// <summary>
        /// Copy the top-level TvShow fields (not the Seasons array - that's
        /// done index-by-index by IngestSeasonsByIndex) from <paramref name="other"/>.
        /// </summary>
        internal void CopyFrom(TvShow other)
        {
            Name = other.Name;
            Cartoon = other.Cartoon;
            Id = other.Id;
            Overview = other.Overview;
            Poster = other.Poster;
            Date = other.Date;
            Backdrop = other.Backdrop;
            CurrSeason = other.CurrSeason;
            LastEpisode = other.LastEpisode;
            RunningTime = other.RunningTime;
        }

        /// <summary>
        /// Swap the top-level language-dependent fields (Name/Overview/CurrSeason/
        /// LastEpisode/Seasons) with the entries at MultiLang*[<paramref name="index"/>].
        /// Used by TvShowWindow when the user switches the language dropdown:
        /// what was "current" goes into the multilang slot, what was in that
        /// slot becomes "current."
        /// </summary>
        internal void SwapWithLanguageIndex(int index)
        {
            (Name, MultiLangName[index]) = (MultiLangName[index], Name);
            (Overview, MultiLangOverview[index]) = (MultiLangOverview[index], Overview);
            (CurrSeason, MultiLangCurrSeason[index]) = (MultiLangCurrSeason[index], CurrSeason);
            (LastEpisode, MultiLangLastWatched[index]) = (MultiLangLastWatched[index], LastEpisode);
            (Seasons, MultiLangSeasons[index]) = (MultiLangSeasons[index], Seasons);
        }

        /// <summary>
        /// Copy the multi-language metadata lists (names, overviews, last-watched
        /// pointers per language). MultiLangSeasons themselves are handled
        /// separately via IngestSeasonsByIndex per language.
        /// </summary>
        internal void CopyMultiLangFrom(TvShow other)
        {
            MultiLangCurrSeason = other.MultiLangCurrSeason;
            MultiLangOverview = other.MultiLangOverview;
            MultiLangName = other.MultiLangName;
            MultiLangLastWatched = other.MultiLangLastWatched;
        }

        internal bool Compare(TvShow localShow)
        {
            if (!this.Path.Equals(localShow.Path, StringComparison.OrdinalIgnoreCase))
                return false;

            if (this.MultiLang)
            {
                return CompareMultiLang(localShow);
            }

            return CompareSeasons(this.Seasons, localShow.Seasons);
        }

        // Structural equality for the parallel multi-lang fields: same number
        // of language entries, matching base names (strip " (Italian)" etc.),
        // and matching season/episode counts + episode paths across all langs.
        private bool CompareMultiLang(TvShow localShow)
        {
            if (this.MultiLangName.Count != localShow.MultiLangName.Count) 
                return false;
            for (int i = 0; i < this.MultiLangName.Count; i++)
            {
                string a = this.MultiLangName[i].Split(" (")[0];
                string b = localShow.MultiLangName[i].Split(" (")[0];
                if (!a.Equals(b)) 
                    return false;
            }

            if (this.MultiLangSeasons.Count != localShow.MultiLangSeasons.Count) 
                return false;
            for (int i = 0; i < this.MultiLangSeasons.Count; i++)
            {
                if (!CompareSeasonsByPath(this.MultiLangSeasons[i], localShow.MultiLangSeasons[i]))
                {
                    return false;
                }
            }
            return true;
        }

        // Single-lang season compare: delegates to Season.Compare which
        // compares episode paths as the structural key.
        private static bool CompareSeasons(Season[] a, Season[] b)
        {
            if (a.Length != b.Length) 
                return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].Compare(b[i])) 
                    return false;
            }
            return true;
        }

        // Lighter version used by the multi-lang compare path: matches
        // episode count and Path only, no metadata fields. Path equality
        // goes through MainModel.PathsMatch so the DEBUG truncation
        // workaround applies here too.
        private static bool CompareSeasonsByPath(Season[] a, Season[] b)
        {
            if (a.Length != b.Length) 
                return false;
            for (int j = 0; j < a.Length; j++)
            {
                if (a[j].Episodes.Length != b[j].Episodes.Length)
                    return false;

                for (int k = 0; k < a[j].Episodes.Length; k++)
                {
                    if (!MainModel.PathsMatch(a[j].Episodes[k].Path, b[j].Episodes[k].Path)) 
                        return false;
                }
            }
            return true;
        }

        public static IComparer<TvShow> SortTvShowsAlphabetically()
            => Comparer<TvShow>.Create((a, b) => string.Compare(a.Name, b.Name));
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

        /// <summary>Copy season metadata (Id/Poster/Date). Episodes handled separately.</summary>
        internal void CopyFrom(Season other)
        {
            Id = other.Id;
            Poster = other.Poster;
            Date = other.Date;
        }

        internal bool Compare(Season localSeason)
        {
            if (this.Id.Equals(localSeason.Id) && this.Id.Equals(-1))
            {
                return true;
            }

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
            => MainModel.PathsMatch(this.Path, otherEpisode.Path);

        /// <summary>
        /// Copy episode metadata + playback state. Translated is opt-in
        /// because it only applies to the multi-lang ingest path.
        /// </summary>
        internal void CopyFrom(Episode other, bool includeTranslated)
        {
            Id = other.Id;
            Name = other.Name;
            Backdrop = other.Backdrop;
            Date = other.Date;
            Overview = other.Overview;
            Path = other.Path;
            SavedTime = other.SavedTime;
            Length = other.Length;
            if (includeTranslated)
            {
                Translated = other.Translated;
            }
        }
    }
}
