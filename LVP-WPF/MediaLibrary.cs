using LVP_WPF.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace LVP_WPF
{
    /// <summary>
    /// Orchestrates loading the media library: scan disk -> compare to the
    /// persisted state -> rebuild from TMDB if needed -> populate the GUI
    /// model. Also exposes the persistence entry points used by other places
    /// in the app (MainWindow on close, NotificationDialog's Save button).
    ///
    /// What used to be called "Cache" - the class kept growing until it was
    /// 1200 lines of mixed concerns. Persistence, scanning, TMDB calls,
    /// translation, image loading, and string fixes have all been pulled
    /// out into separate services under Services/ and Util/.
    /// </summary>
    internal sealed class MediaLibrary
    {
        private readonly MediaRepository _repository;
        private ILoadProgress _progress;

        public MediaLibrary(MediaRepository repository)
        {
            _repository = repository;
        }

        internal Task Initialize(ILoadProgress progress)
        {
            _progress = progress;

            // Run the heavy startup work on a dedicated BelowNormal-priority
            // thread instead of the thread pool. Two reasons:
            //
            // (1) Priority: the OS scheduler now lets WPF's render thread
            //     preempt this worker, so the load-screen spinner gets CPU
            //     time during the scan + JSON load and doesn't stutter.
            //
            // (2) GC latency mode: we flip the GC to LowLatency for the
            //     duration of init so it skips Gen 2 collections. Those
            //     Gen 2 pauses are what blip the render thread - they
            //     suspend all managed threads briefly, regardless of
            //     priority. The heap grows temporarily; a normal full GC
            //     happens after init returns.
            //
            // The dedicated thread also lets us call BuildCache (only on
            // the rare needsRebuild path) via .GetAwaiter().GetResult()
            // without a sync-over-async deadlock - we own this thread, no
            // SynchronizationContext to deadlock on.
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            Thread worker = new Thread(() =>
            {
                System.Runtime.GCLatencyMode prevMode = System.Runtime.GCSettings.LatencyMode;
                try
                {
                    System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.LowLatency;
                    RunInitializeBody();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
                finally
                {
                    System.Runtime.GCSettings.LatencyMode = prevMode;
                }
            });
            worker.IsBackground = true;
            worker.Priority = ThreadPriority.BelowNormal;
            worker.Name = "MediaLibrary.Initialize";
            worker.Start();
            return tcs.Task;
        }

        private void RunInitializeBody()
        {
            Stopwatch totalSw = Stopwatch.StartNew();
            Stopwatch phaseSw = Stopwatch.StartNew();
            Log("Init: START");

            LibraryRoot[] roots = AppConfig.Drives.Select(d =>
            {
#if DEBUG
                return new LibraryRoot($"{d}\\media\\tv", $"{d}\\media\\movie");
#else
                return new LibraryRoot($"{d}:\\media\\tv", $"{d}:\\media\\movie");
#endif
            }).ToArray();
            Log($"Init: roots built in {phaseSw.ElapsedMilliseconds}ms ({roots.Length} drives)");

            LibraryScanner scanner = new LibraryScanner(AppConfig.Languages);
            phaseSw.Restart();
            ScanResult scanResult = scanner.Scan(roots);
            Log($"Init: scanner.Scan TOTAL {phaseSw.ElapsedMilliseconds}ms ({scanResult.Model.Movies.Length} movies, {scanResult.Model.TvShows.Length} tv shows, {scanResult.MediaCount} media)");

            phaseSw.Restart();
            foreach (string warning in scanResult.Warnings)
            {
                Application.Current.Dispatcher.Invoke(delegate
                {
                    NotificationDialog.Show("Error", warning);
                });
            }
            if (scanResult.Warnings.Count > 0)
            {
                Log($"Init: warnings dispatched in {phaseSw.ElapsedMilliseconds}ms ({scanResult.Warnings.Count})");
            }

            MainWindow.model = scanResult.Model;

            bool needsRebuild;
            phaseSw.Restart();
            try
            {
                needsRebuild = CheckForUpdates();
            }
            catch (Exception ex)
            {
                NotificationDialog.Show(ex.Message, ex.StackTrace);
                needsRebuild = false;
            }
            Log($"Init: CheckForUpdates TOTAL {phaseSw.ElapsedMilliseconds}ms (needsRebuild={needsRebuild})");

            if (needsRebuild)
            {
                //To-do MultiLang: Detect file extension changes and episode deletions
                _progress.ShowRebuildIndicators();
                MainWindow.gui.ProgressBarMax = scanResult.MediaCount;
                // Sync-over-async is intentional here - we own this thread,
                // no captured SynchronizationContext to deadlock on, and
                // BuildCache only runs in the rare rebuild path.
                phaseSw.Restart();
                BuildCache().GetAwaiter().GetResult();
                Log($"Init: BuildCache {phaseSw.ElapsedMilliseconds}ms");
            }

            phaseSw.Restart();
            foreach (Movie m in MainWindow.model.Movies)   MainWindow.gui.mediaDict[m.Id] = m;
            foreach (TvShow t in MainWindow.model.TvShows) MainWindow.gui.mediaDict[t.Id] = t;
            Log($"Init: mediaDict populated in {phaseSw.ElapsedMilliseconds}ms ({MainWindow.gui.mediaDict.Count} entries)");

            if (MainWindow.model.HistoryList.Count == 0 || needsRebuild)
            {
                phaseSw.Restart();
                // Flatten all non-cartoon episodes into the history list, sorted
                // by air date. Keeps the existing List<Episode> instance (Clear+AddRange
                // rather than reassign) in case anything has captured the reference.
                MainWindow.model.HistoryList.Clear();
                MainWindow.model.HistoryList.AddRange(
                    MainWindow.model.TvShows
                        .Where(t => !t.Cartoon)
                        .SelectMany(t => t.Seasons)
                        .SelectMany(s => s.Episodes));
                MainWindow.model.HistoryList.Sort((a, b) => a.Date.CompareTo(b.Date));
                Log($"Init: HistoryList rebuilt in {phaseSw.ElapsedMilliseconds}ms ({MainWindow.model.HistoryList.Count} episodes)");
            }

            Log($"Init: END (total {totalSw.ElapsedMilliseconds}ms)");
        }

        internal async Task BuildCache()
        {
            IHttpClientFactory factory = new ServiceCollection().AddHttpClient().BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
            using HttpClient client = factory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(1);

            string cacheRoot = $"{AppDomain.CurrentDomain.BaseDirectory}cache";
            TmdbClient tmdb = new TmdbClient(AppConfig.TmdbApiKey, client, cacheRoot, Log);

            IUserPrompts prompts = new WpfUserPrompts();

            string translatorPath = $"{AppConfig.LibreTranslatePath}libretranslate.exe";
            using Translator translator = new Translator(translatorPath, client, prompts);

            MediaEnricher enricher = new MediaEnricher(
                tmdb,
                translator,
                prompts,
                onItemEnriched: () => MainWindow.gui.ProgressBarValue++,
                saveCheckpoint: SaveData);

            foreach (Movie movie in MainWindow.model.Movies)
            {
                await enricher.EnrichMovieAsync(movie);
                MainWindow.gui.ProgressBarValue++;
            }

            foreach (TvShow tvShow in MainWindow.model.TvShows)
            {
                await enricher.EnrichTvShowAsync(tvShow);
            }

            Array.Sort(MainWindow.model.Movies, Movie.SortMoviesAlphabetically());
            Array.Sort(MainWindow.model.TvShows, TvShow.SortTvShowsAlphabetically());
            SaveData();
        }

        internal bool CheckForUpdates()
        {
            Log("Check for updates start...");

            // Split CheckForUpdates into its three sub-phases so we can see
            // which one dominates startup time on a real library.
            Stopwatch sw = Stopwatch.StartNew();
            MainModel? prevMedia = _repository.Load();
            Log($"  Load: {sw.ElapsedMilliseconds}ms");

            if (prevMedia == null)
            {
                return true;
            }

            sw.Restart();
            bool result = !MainWindow.model.Compare(prevMedia);
            Log($"  Compare: {sw.ElapsedMilliseconds}ms (changed={result})");

            sw.Restart();
            if (!result)
            {
                MainWindow.model = prevMedia;
            }
            else
            {
                MainWindow.model.Ingest(prevMedia);
            }
            Log($"  {(result ? "Ingest" : "Swap")}: {sw.ElapsedMilliseconds}ms");

            Log("Check for updates end");
            return result;
        }

        internal void SaveData()
        {
            _repository.Save(MainWindow.model);
        }

        // Single log helper: writes to Serilog (file + Debug sinks). The
        // previous version also wrote to a load-screen TextBox, but the
        // per-call dispatcher hop + TextBox layout work was eating enough
        // UI-thread cycles to visibly stutter the load-screen spinner.
        // Log lines now live only in Serilog destinations (file under
        // {baseDir}\logs and the Debug pane in VS).
        private void Log(string msg) => Serilog.Log.Information(msg);
    }
}