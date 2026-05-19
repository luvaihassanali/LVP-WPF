using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace LVP_WPF.Services
{
    /// <summary>
    /// Decodes images off the UI thread (BitmapCacheOption.OnLoad + Freeze)
    /// so they can be safely handed to bindings on any thread.
    /// </summary>
    internal static class ImageLoader
    {
        /// <summary>Placeholder used when a movie or TV show has no cached poster.</summary>
        public const string DefaultPoster = "Resources\\noPrev.png";

        /// <summary>Placeholder used when a movie or TV show has no cached backdrop or episode still.</summary>
        public const string DefaultBackdrop = "Resources\\noPrevWide.png";

        /// <summary>
        /// The translucent play-button overlay shown when the user hovers over
        /// a backdrop or episode tile. Decoded once and shared - the underlying
        /// BitmapImage is Freeze()d so it's safe to assign onto multiple
        /// bindings from any thread.
        /// </summary>
        private static BitmapImage? _playOverlay;
        public static BitmapImage PlayOverlay => _playOverlay ??= Load("Resources\\play.png", 960);

        /// <summary>Load a poster image (300px wide), falling back to <see cref="DefaultPoster"/> when path is null.</summary>
        public static BitmapImage LoadPoster(string? path, int pixelWidth = 300)
            => Load(path ?? DefaultPoster, pixelWidth);

        /// <summary>Load a backdrop / episode still (typically 960px wide), falling back to <see cref="DefaultBackdrop"/> when path is null.</summary>
        public static BitmapImage LoadBackdrop(string? path, int pixelWidth = 960)
            => Load(path ?? DefaultBackdrop, pixelWidth);

        /// <summary>
        /// Loads a bitmap from disk, decoded to the given width. Paths that
        /// start with "Resources\" are resolved against the app base directory.
        /// </summary>
        public static BitmapImage Load(string filename, int pixelWidth)
        {
            if (filename.Contains("Resources\\"))
            {
                filename = AppDomain.CurrentDomain.BaseDirectory + filename;
            }

            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(filename);
            image.DecodePixelWidth = pixelWidth;
            image.EndInit();
            image.Freeze();
            return image;
        }

        /// <summary>
        /// For multi-language TV shows, returns up to 16 flag images (one per
        /// non-English language folder found directly under <paramref name="path"/>).
        /// English is intentionally skipped - the main image is already the
        /// English-language poster.
        /// </summary>
        public static BitmapImage[] LoadFlags(string path)
        {
            BitmapImage[] result = new BitmapImage[16];
            string[] langFolders = Directory.GetDirectories(path);
            int langIndex = 0;
            for (int i = 0; i < langFolders.Length; i++)
            {
                string langKey = langFolders[i].Replace(path, "").Split("\\")[1];
                if (langKey.Length != 2)
                {
                    return result;
                }
                if (langKey.Equals("en"))
                {
                    continue;
                }

                string imgPath = $"Resources\\flags\\{langKey.ToUpper()}.png";
                if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + imgPath))
                {
                    NotificationDialog.Show("Error", $"Flag image does not exist for language key: {langKey.ToUpper()}");
                }
                result[langIndex++] = Load(imgPath, 56);
            }
            return result;
        }
    }
}
