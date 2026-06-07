using System;
using System.Configuration;

namespace LVP_WPF.Services
{
    /// <summary>
    /// Strongly-typed accessors for the settings in App.config. Call
    /// <see cref="Initialize"/> once at MainWindow construction; values
    /// don't change at runtime so they're cached on first read.
    ///
    /// Was previously a dozen ad-hoc <c>ConfigurationManager.AppSettings["..."]</c>
    /// calls scattered across MediaEnricher, MediaLibrary, TcpSerialListener,
    /// IrSerialReader, TvShowWindow, MainWindow - including one in
    /// MediaEnricher's hot path that parsed the value on every TV-show
    /// metadata fetch.
    ///
    /// Note: the config keys "Esp8226Enabled" / "Esp8226HideCursor" preserve
    /// the original typo (should be Esp8266) so existing App.config files
    /// keep working; the C# property names use the correct spelling.
    /// </summary>
    public static class AppConfig
    {
        // ---- Library / cache build ----
        public static string[] Drives { get; private set; }
        public static string[] Languages { get; private set; }
        public static string[] CartoonExceptions { get; private set; }
        public static string TmdbApiKey { get; private set; }
        public static string LibreTranslatePath { get; private set; }
        public static int CartoonLimit { get; private set; }

        // ---- Remote input ----
        public static string Esp8266Ip { get; private set; }
        public static int Esp8266Port { get; private set; }
        public static bool Esp8266Enabled { get; private set; }
        public static int SerialPort { get; private set; }
        public static bool SerialPortEnabled { get; private set; }
        public static bool HideCursor { get; private set; }

        // ---- UI / shell ----
        public static bool ShowSnow { get; private set; }
        public static string MouseHubPath { get; private set; }

        public static void Initialize()
        {
            Drives = ConfigurationManager.AppSettings["Drives"].Split(';');
            Languages = ConfigurationManager.AppSettings["Languages"].Split(';');
            CartoonExceptions = ConfigurationManager.AppSettings["CartoonExceptions"].Split(';');
            TmdbApiKey = ConfigurationManager.AppSettings["TmdbApiKey"];
            LibreTranslatePath = ConfigurationManager.AppSettings["LibreTranslatePath"];
            CartoonLimit = int.Parse(ConfigurationManager.AppSettings["CartoonLimit"]);

            Esp8266Ip = ConfigurationManager.AppSettings["Esp8266Ip"];
            Esp8266Port = int.Parse(ConfigurationManager.AppSettings["Esp8266Port"]);
            Esp8266Enabled = bool.Parse(ConfigurationManager.AppSettings["Esp8226Enabled"]);
            SerialPort = int.Parse(ConfigurationManager.AppSettings["SerialPort"]);
            SerialPortEnabled = bool.Parse(ConfigurationManager.AppSettings["SerialPortEnabled"]);
            string? hide = ConfigurationManager.AppSettings["Esp8226HideCursor"];
            HideCursor = hide != null && bool.Parse(hide);

            ShowSnow = bool.Parse(ConfigurationManager.AppSettings["Snow"]);
            MouseHubPath = ConfigurationManager.AppSettings["MouseHubPath"];
        }
    }
}
