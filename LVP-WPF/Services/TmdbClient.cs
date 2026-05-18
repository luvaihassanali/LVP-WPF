using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace LVP_WPF.Services
{
    /// <summary>
    /// Thin wrapper over the TMDB v3 HTTP API plus its CDN image downloads.
    /// Returns raw JObjects (no DTO mapping yet) and downloads images to a
    /// caller-provided cache root. No model mutation, no dialogs, no UI.
    /// </summary>
    internal sealed class TmdbClient
    {
        private const string ApiBase = "https://api.themoviedb.org/3/";
        private const string ImageBase = "http://image.tmdb.org/t/p/original";

        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly string _cacheRoot;
        private readonly Action<string>? _log;

        /// <param name="apiKey">TMDB v3 API key (the raw value, no "?api_key=" prefix).</param>
        /// <param name="httpClient">Reused for all calls; caller owns its lifetime.</param>
        /// <param name="cacheRoot">Directory to cache downloaded images under, e.g. {AppBase}cache.</param>
        /// <param name="log">Optional per-request log sink (currently routes to the load-screen TextBox).</param>
        public TmdbClient(string apiKey, HttpClient httpClient, string cacheRoot, Action<string>? log = null)
        {
            _apiKey = apiKey;
            _httpClient = httpClient;
            _cacheRoot = cacheRoot;
            _log = log;
        }

        public Task<JObject> SearchTvAsync(string query) =>
            GetJsonAsync($"{ApiBase}search/tv?api_key={_apiKey}&query={query}", "GET search tv show");

        public Task<JObject> GetTvShowAsync(int tvId) =>
            GetJsonAsync($"{ApiBase}tv/{tvId}?api_key={_apiKey}", "GET tv show");

        public Task<JObject> GetTvSeasonAsync(int tvId, int seasonNumber) =>
            GetJsonAsync($"{ApiBase}tv/{tvId}/season/{seasonNumber}?api_key={_apiKey}", "GET tv season");

        public Task<JObject> SearchMovieAsync(string query) =>
            GetJsonAsync($"{ApiBase}search/movie?api_key={_apiKey}&query={query}", "GET search movie");

        public Task<JObject> GetMovieAsync(int movieId) =>
            GetJsonAsync($"{ApiBase}movie/{movieId}?api_key={_apiKey}", "GET movie");

        /// <summary>
        /// Downloads an image from TMDB and caches it to disk. Returns the local path.
        /// If already cached, returns the existing path without re-fetching.
        /// </summary>
        /// <param name="imagePath">TMDB image path beginning with "/" (poster_path, backdrop_path, still_path).</param>
        /// <param name="isMovie">true => cache under cache/movies/{name}/, false => cache/tv/{name}/.</param>
        /// <param name="name">Title used as the per-item cache subfolder.</param>
        public async Task<string> DownloadImageAsync(string imagePath, bool isMovie, string name)
        {
            string url = ImageBase + imagePath;
            string subroot = isMovie ? "movies" : "tv";
            string dirPath = $"{_cacheRoot}\\{subroot}\\{name}";
            string filePath = dirPath + imagePath.Replace("/", "\\");

            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            if (!File.Exists(filePath))
            {
                using FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, short.MaxValue, true);
                try
                {
                    _log?.Invoke($"GET image {url}");
                    using HttpResponseMessage response = await _httpClient.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead);
                    using HttpContent content = response.EnsureSuccessStatusCode().Content;
                    await content.CopyToAsync(fileStream);
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.ToString());
                }
            }

            return filePath;
        }

        private async Task<JObject> GetJsonAsync(string url, string logPrefix)
        {
            _log?.Invoke($"{logPrefix} {url}");
            using HttpResponseMessage response = await _httpClient.GetAsync(url);
            using HttpContent content = response.Content;
            string body = await content.ReadAsStringAsync();
            return JObject.Parse(body);
        }
    }
}
