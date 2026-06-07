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
        private readonly HttpClient _httpClient;
        private readonly IUserPrompts _prompts;
        private bool _started;

        /// <param name="executablePath">
        /// Path to libretranslate.exe. May contain %APPDATA% / %LOCALAPPDATA% /
        /// any environment variable; expanded on first use.
        /// </param>
        /// <param name="httpClient">Reused for all translate calls; caller owns its lifetime.</param>
        /// <param name="prompts">Used for the startup announcement and any failure popups.</param>
        public Translator(string executablePath, HttpClient httpClient, IUserPrompts prompts)
        {
            _executablePath = executablePath;
            _httpClient = httpClient;
            _prompts = prompts;
        }

        public async Task<string> TranslateAsync(string targetLang, string text)
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
                using HttpResponseMessage response = await _httpClient.PostAsync(TranslateEndpoint, content);
                string responseString = await response.Content.ReadAsStringAsync();
                LibreTranslateResponse resp = JsonConvert.DeserializeObject<LibreTranslateResponse>(responseString);
                return resp.TranslatedText;
            }
            catch (Exception ex)
            {
                _prompts.ShowError("Error", ex.Message);
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
                    _prompts.ShowError("Error", $"LibreTranslate exe does not exist at {path}");
                }

                Process proc = new Process();
                proc.StartInfo.FileName = path;
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                proc.Start();
            }

            _prompts.ShowNotice("Information", "LibreTranslate launched. Waiting 10 seconds till ready...");
            await Task.Delay(StartupWait);
            _started = true;
        }

        /// <summary>
        /// If we ever used the translator, kill any running libretranslate process.
        /// Matches the original cleanup behavior: kill any running libretranslate
        /// process, regardless of whether we were the one that started it.
        /// </summary>
        public void Dispose()
        {
            if (!_started) return;
            Process[] procs = Process.GetProcessesByName("libretranslate");
            if (procs.Length == 0) return;
            try
            {
                procs[0].Kill();
            }
            catch
            {
                // Process can exit between GetProcessesByName and Kill; also
                // fails with Win32 access-denied if libretranslate was launched
                // elevated. Either way there's nothing more to clean up.
            }
        }

        private sealed class LibreTranslateResponse
        {
            public string TranslatedText { get; set; }
        }
    }
}
