namespace LVP_WPF.Services
{
    /// <summary>
    /// Hard-coded cursor positioning constants calibrated for a 1920x1080
    /// display. The off-screen "hide" point at (35, 1100) parks the cursor
    /// below the visible area when the joystick is idle; (960, 540) is the
    /// centered home position used at app startup.
    ///
    /// The HideCursor flag (whether to actually hide) comes from
    /// <see cref="AppConfig.HideCursor"/> and is exposed here for the
    /// existing call-site convenience.
    /// </summary>
    public static class CursorConfig
    {
        public const int HideCursorX = 35;
        public const int HideCursorY = 1100;
        public const int CenterX = 960;
        public const int CenterY = 540;

        public static bool HideCursor => AppConfig.HideCursor;
    }
}
