using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace LVP_WPF
{
    internal static class Cache
    {
        private const string apiKey = "?api_key=c69c4effc7beb9c473d22b8f85d59e4c";
        private const string apiUrl = "https://api.themoviedb.org/3/";
        private const string apiImageUrl = "http://image.tmdb.org/t/p/original";
        private const string apiTvSearchUrl = apiUrl + "search/tv" + apiKey + "&query=";
        private const string apiMovieSearchUrl = apiUrl + "search/movie" + apiKey + "&query=";
        
        private static string apiTvUrl = apiUrl + "tv/{tv_id}" + apiKey;
        private static string apiTvSeasonUrl = apiUrl + "tv/{tv_id}/season/{season_number}" + apiKey;
        private static string apiMovieUrl = apiUrl + "movie/{movie_id}" + apiKey;
        private static string bufferString = "";

        private static List<string> tvPathList = new List<string>();
        private static List<string> moviePathList = new List<string>();

        internal static void ProcessRootDirectories()
        {
            string driveString = ConfigurationManager.AppSettings["Drives"];
            string[] drives = driveString.Split(';');
            foreach (string drive in drives)
            {
                ProcessRootDirectory(drive);
            }
            Trace.WriteLine("break");
            //foreach tv/movie -> prorcess '' dir
            //
        }

        internal static void ProcessRootDirectory(string driveLetter)
        {
            string tvDirPath = driveLetter + ":\\media\\tv";
            if (!Directory.Exists(tvDirPath))
            {
                bool inputRes = InputDialog.Show(
                    "Backup and Restor center",
                    "You need to be an Administrator to run backup",
                    "Use Fast User Switching to switch to an account with administrator privileges, or log off and log on as an administrator",
                NotifyIcon.Exclamation);

                if (inputRes)
                {

                }
                else
                {
                    Environment.Exit(0);
                }
            }

            string movieDirPath = driveLetter + ":\\media\\movie";
            if (!Directory.Exists(movieDirPath))
            {
                MessageBox.Show("Error: Movie folder on " + driveLetter + " drive not found.");
                Environment.Exit(0);
            }

            tvPathList.AddRange(Directory.GetDirectories(tvDirPath));
            moviePathList.AddRange(Directory.GetDirectories(movieDirPath));
        }
    }
}
