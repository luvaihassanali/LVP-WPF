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

        /// <summary>Loads the persisted model, or null if no file exists.</summary>
        public MainModel? Load()
        {
            if (!File.Exists(_path))
            {
                return null;
            }
            string json = File.ReadAllText(_path);
            return JsonConvert.DeserializeObject<MainModel>(json);
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
        /// This mirrors the original Cache.SaveData() behavior.
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
