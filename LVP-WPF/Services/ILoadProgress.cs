namespace LVP_WPF.Services
{
    /// <summary>
    /// What MediaLibrary needs from its host UI during startup: an append-only
    /// log sink and a way to switch on the rebuild-indicator widgets (progress
    /// bar, coffee gif, log textbox) when a TMDB rebuild is about to start.
    ///
    /// Lets MediaLibrary stay free of WPF control types in its public surface
    /// (no ProgressBar/MediaElement/TextBox parameters).
    /// </summary>
    internal interface ILoadProgress
    {
        /// <summary>Append a line to the load-screen log textbox.</summary>
        void AppendLog(string message);

        /// <summary>
        /// Make the rebuild-indicator UI (progress bar, animated spinner,
        /// log textbox) visible. Called once, when a rebuild is about to
        /// begin; the no-rebuild path keeps these hidden.
        /// </summary>
        void ShowRebuildIndicators();
    }
}
