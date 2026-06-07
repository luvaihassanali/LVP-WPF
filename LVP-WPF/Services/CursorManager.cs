using Microsoft.Win32;
using System;

namespace LVP_WPF.Services
{
    /// <summary>
    /// Manages the per-user system cursor. On startup the app swaps Windows'
    /// default cursors for the 72x72 high-DPI custom set (so the cursor is
    /// visible on a TV-sized display from couch distance); on shutdown the
    /// originals are restored. The pre/post state is stored in the embedded
    /// Resources (keys_backup, keys_custom).
    ///
    /// Modifies HKEY_CURRENT_USER\Control Panel\Cursors and broadcasts
    /// SPI_SETCURSORS via SystemParametersInfo, so the change takes effect
    /// system-wide while LVP-WPF is running.
    /// </summary>
    internal static class CursorManager
    {
        private const string CursorsKey = @"HKEY_CURRENT_USER\Control Panel\Cursors\";
        private const uint SPI_SETCURSORSIZE = 0x2029;

        public static void RestoreSystemCursor()
        {
            string[] keys = Properties.Resources.keys_backup.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            foreach (string key in keys)
            {
                string[] keyValuePair = key.Split('=');
                Registry.SetValue(CursorsKey, keyValuePair[0], keyValuePair[1]);
            }
            ComInterop.SystemParametersInfo(ComInterop.SPI_SETCURSORS, 0, 0, ComInterop.SPIF_UPDATEINIFILE | ComInterop.SPIF_SENDCHANGE);
            ComInterop.SystemParametersInfo(SPI_SETCURSORSIZE, 0, 32, 0x01);
        }

        public static void InitializeCustomCursor()
        {
            string cursorPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
            string[] keys = Properties.Resources.keys_custom.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            foreach (string key in keys)
            {
                string[] keyValuePair = key.Split('=');
                Registry.SetValue(CursorsKey, keyValuePair[0], cursorPath + keyValuePair[1]);
            }
            ComInterop.SystemParametersInfo(ComInterop.SPI_SETCURSORS, 0, 0, ComInterop.SPIF_UPDATEINIFILE | ComInterop.SPIF_SENDCHANGE);
            ComInterop.SystemParametersInfo(SPI_SETCURSORSIZE, 0, 72, 0x01);
        }
    }
}
