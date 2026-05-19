using System.Configuration;

namespace LVP_WPF.Services
{
    /// <summary>
    /// Cursor positioning / hiding config for the input-driven UX. Values
    /// are calibrated for a 1920x1080 display; the off-screen "hide" point
    /// at (35, 1100) parks the cursor below the visible area when the
    /// joystick is idle. Centred (960, 540) is the home position used at
    /// app startup.
    ///
    /// Was previously a grab-bag of `static public` fields on GuiModel.
    /// Lifted out so GuiModel is just the bindable view-model state.
    /// </summary>
    public static class CursorConfig
    {
        public static bool HideCursor { get; private set; }
        public const int HideCursorX = 35;
        public const int HideCursorY = 1100;
        public const int CenterX = 960;
        public const int CenterY = 540;

        /// <summary>
        /// Reads the `Esp8226HideCursor` AppSetting and caches it. Call once
        /// at startup; the value doesn't change at runtime.
        /// </summary>
        public static void Initialize()
        {
            string? h = ConfigurationManager.AppSettings["Esp8226HideCursor"];
            HideCursor = h != null && bool.Parse(h);
        }
    }
}
