using System;

namespace LVP_WPF.Services
{
    /// <summary>
    /// Abstracts the three modal dialogs that the cache-build path needs to
    /// pop. Lets MediaEnricher and Translator stay free of direct WPF
    /// dependencies (and free of Application.Current.Dispatcher.Invoke for
    /// UI-thread marshalling).
    /// </summary>
    internal interface IUserPrompts
    {
        /// <summary>
        /// Modal error popup. Has Save and Exit buttons. Backed by
        /// NotificationDialog in the WPF implementation.
        /// </summary>
        void ShowError(string caption, string message);

        /// <summary>
        /// Modal informational/warning popup with Continue and Exit buttons.
        /// When <paramref name="tvShow"/> is supplied, also offers a "Go to
        /// TMDB" link. When <paramref name="episodePath"/> is supplied, also
        /// offers a "Folder" button that opens File Explorer with that file
        /// highlighted - useful for jumping to the offending file when the
        /// dialog is reporting a name mismatch. Backed by InputDialog in the
        /// WPF implementation.
        /// </summary>
        void ShowNotice(string caption, string message, TvShow? tvShow = null, int currSeason = 0, string? episodePath = null);

        /// <summary>
        /// Modal multi-choice picker for TMDB disambiguation. Returns the
        /// selected entry's ID. Marshals to the UI thread internally.
        /// Backed by OptionDialog in the WPF implementation.
        /// </summary>
        int ChooseOption(string title, string path, string[][] info, DateTime?[] dates);
    }
}
