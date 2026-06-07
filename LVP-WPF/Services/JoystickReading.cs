using System;

namespace LVP_WPF.Services
{
    /// <summary>
    /// One sample from the ESP8266 joystick: analog X/Y plus three button
    /// states. The TCP packet arrives as a comma-separated ASCII string;
    /// <see cref="TryParse"/> handles the format.
    ///
    /// On-the-wire convention from the firmware: each button state is sent
    /// as an int where <c>0 == pressed</c> (active-low wiring); this record
    /// exposes them as plain booleans where <c>true == pressed</c>.
    /// </summary>
    internal readonly record struct JoystickReading(
        int X,
        int Y,
        bool JoystickButton,
        bool ClickButton,
        bool ScrollButton)
    {
        /// <summary>
        /// Parses a wire packet of the form "X,Y,joystickBtn,clickBtn,scrollBtn[,...]".
        /// Returns null if the packet has more than 6 comma-separated fields
        /// (treated as a framing error). Trailing CRLF inside the last
        /// fields is tolerated.
        /// </summary>
        public static JoystickReading? TryParse(string data)
        {
            string[] parts = data.Split(',');
            if (parts.Length > 6) return null;
            return new JoystickReading(
                int.Parse(parts[0]),
                int.Parse(parts[1]),
                int.Parse(parts[2]) == 0,
                int.Parse(parts[3].Replace("\r\n", "")) == 0,
                int.Parse(parts[4].Replace("\r\n", "")) == 0);
        }
    }
}
