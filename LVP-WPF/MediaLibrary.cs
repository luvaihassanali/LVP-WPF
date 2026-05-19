using LVP_WPF.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
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

        internal async Task Initialize(ILoadProgress progress)
        {
            _progress = progress;
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

                bool needsRebuild;
                try
                {
                    needsRebuild = CheckForUpdates();
                }
                catch (Exception ex)
                {
                    NotificationDialog.Show(ex.Message, ex.StackTrace);
                    needsRebuild = false;
                }

                if (needsRebuild)
                {
                    //To-do MultiLang: Detect file extension changes and episode deletions
                    _progress.ShowRebuildIndicators();
                    MainWindow.gui.ProgressBarMax = scanResult.MediaCount;
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

                if (MainWindow.model.HistoryList.Count == 0 || needsRebuild)
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

        internal async Task BuildCache()
        {
            IHttpClientFactory factory = new ServiceCollection().AddHttpClient().BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
            using HttpClient client = factory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(1);

            string apiKey = ConfigurationManager.AppSettings["TmdbApiKey"];
            string cacheRoot = $"{AppDomain.CurrentDomain.BaseDirectory}cache";
            TmdbClient tmdb = new TmdbClient(apiKey, client, cacheRoot, Log);

            IUserPrompts prompts = new WpfUserPrompts();

            string translatorPath = $"{ConfigurationManager.AppSettings["LibreTranslatePath"]}libretranslate.exe";
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

        internal void SaveData()
        {
            _repository.Save(MainWindow.model);
        }

        private void Log(string msg)
        {
#if DEBUG
            Debug.WriteLine(msg);
#endif
            _progress.AppendLog(msg);
        }
    }
}