using Newtonsoft.Json;
using System.IO;
using System.Linq;

namespace LVP_WPF.Services
{
    /// <summary>
    /// Reads and writes the media library state to disk as JSON.
    /// Persistence only — no scanning, no TMDB, no UI concerns.
    /// </summary>
    internal sealed class MediaRepository
    {
        private readonly string _path;
        private readonly string _backupPath;
        private readonly string _tempPath;

        public MediaRepository(string path)
        {
            _path = path;
            _backupPath = path + ".bak";
            _tempPath = path + ".tmp";
        }

        /// <summary>
        /// Loads the persisted model, or null if no file exists. Streams the
        /// file straight into the model via JsonTextReader rather than
        /// File.ReadAllText + DeserializeObject(string); that pair allocates
        /// (1) the whole JSON as one large string on the LOH, and (2) a full
        /// intermediate JObject tree before materializing the model. Together
        /// they generate enough garbage to trigger GC pauses big enough to
        /// stutter WPF's render thread (the load-screen spinner jitters
        /// during this call). Streaming avoids both allocations.
        /// </summary>
        public MainModel? Load()
        {
            if (!File.Exists(_path))
            {
                return null;
            }
            using FileStream fs = File.OpenRead(_path);
            using StreamReader sr = new StreamReader(fs);
            using JsonTextReader jr = new JsonTextReader(sr);
            return new JsonSerializer().Deserialize<MainModel>(jr);
        }

        /// <summary>
        /// Persists the model to disk with an atomic-ish swap:
        ///   1. Serialize and write to {path}.tmp
        ///   2. Rotate the existing {path} to {path}.bak (replacing any prior backup)
        ///   3. Move {path}.tmp into place
        /// A crash between steps 2 and 3 leaves the .bak available for recovery.
        ///
        /// Items with Id == 0 (failed TMDB matches) are dropped from BOTH the
        /// in-memory model and the file, so they get re-scanned next launch.
        /// This mirrors the original SaveData() behavior pre-extraction.
        /// </summary>
        public void Save(MainModel model)
        {
            model.Movies = model.Movies.Where(m => m.Id != 0).ToArray();
            model.TvShows = model.TvShows.Where(t => t.Id != 0).ToArray();

            string json = JsonConvert.SerializeObject(model);
            File.WriteAllText(_tempPath, json);

            if (File.Exists(_path))
            {
                if (File.Exists(_backupPath))
                {
                    File.Delete(_backupPath);
                }
                File.Move(_path, _backupPath);
            }
            File.Move(_tempPath, _path);
        }
    }
}
