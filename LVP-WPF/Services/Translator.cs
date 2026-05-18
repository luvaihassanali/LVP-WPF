using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace LVP_WPF.Services
{
    /// <summary>
    /// Wraps a locally-running LibreTranslate instance (https://github.com/LibreTranslate/LibreTranslate).
    /// First call starts the libretranslate.exe process on demand and waits for
    /// it to come up; Dispose kills it again.
    /// </summary>
    internal sealed class Translator : IDisposable
    {
        private const string TranslateEndpoint = "http://localhost:5000/translate";
        private const string SourceLanguage = "en";
        private static readonly TimeSpan StartupWait = TimeSpan.FromSeconds(10);

        private readonly string _executablePath;
        private bool _started;

        /// <param name="executablePath">
        /// Path to libretranslate.exe. May contain %APPDATA% / %LOCALAPPDATA% /
        /// any environment variable; expanded on first use.
        /// </param>
        public Translator(string executablePath)
        {
            _executablePath = executablePath;
        }

        public async Task<string> TranslateAsync(string targetLang, string text, HttpClient client)
        {
#if DEBUG
            await Task.Delay(1);
            return "debug-translate";
#else
            await EnsureStartedAsync();

            Dictionary<string, string> values = new Dictionary<string, string>
            {
                { "q", text },
                { "source", SourceLanguage },
                { "target", targetLang }
            };

            FormUrlEncodedContent content = new FormUrlEncodedContent(values);
            try
            {
                using HttpResponseMessage response = await client.PostAsync(TranslateEndpoint, content);
                string responseString = await response.Content.ReadAsStringAsync();
                LibreTranslateResponse resp = JsonConvert.DeserializeObject<LibreTranslateResponse>(responseString);
                return resp.TranslatedText;
            }
            catch (Exception ex)
            {
                NotificationDialog.Show("Error", ex.Message);
                throw new Exception("LibreTranslate failure");
            }
#endif
        }

        private async Task EnsureStartedAsync()
        {
            if (_started) return;

            Process[] existing = Process.GetProcessesByName("libretranslate");
            if (existing.Length == 0)
            {
                string path = Environment.ExpandEnvironmentVariables(_executablePath);
                if (!File.Exists(path))
                {
                    NotificationDialog.Show("Error", $"LibreTranslate exe does not exist at {path}");
                }

                Process proc = new Process();
                proc.StartInfo.FileName = path;
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                proc.Start();
            }

            InputDialog.Show("Information", "LibreTranslate launched. Waiting 10 seconds till ready...");
            await Task.Delay(StartupWait);
            _started = true;
        }

        /// <summary>
        /// If we ever used the translator, kill any running libretranslate process.
        /// Mirrors the original Cache.BuildCache cleanup: it kills regardless of
        /// whether we were the one that started it.
        /// </summary>
        public void Dispose()
        {
            if (!_started) return;
            Process[] procs = Process.GetProcessesByName("libretranslate");
            if (procs.Length != 0)
            {
                procs[0].Kill();
            }
        }

        private sealed class LibreTranslateResponse
        {
            public string TranslatedText { get; set; }
        }
    }
}
