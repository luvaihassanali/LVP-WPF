namespace LVP_WPF.Services
{
    /// <summary>
    /// What MediaLibrary needs from its host UI during startup: a way to
    /// switch on the rebuild-indicator widgets (progress bar, coffee gif)
    /// when a TMDB rebuild is about to start.
    ///
    /// Lets MediaLibrary stay free of WPF control types in its public surface
    /// (no ProgressBar/MediaElement parameters).
    ///
    /// AppendLog was here too; removed because routing per-phase log lines
    /// to a load-screen TextBox dispatched too much UI-thread work and
    /// stuttered the load-screen spinner. Phase timings still flow to Serilog
    /// (file + Debug-pane sinks), which is enough for diagnostics.
    /// </summary>
    internal interface ILoadProgress
    {
        /// <summary>
        /// Make the rebuild-indicator UI (progress bar + animated spinner)
        /// visible. Called once, when a rebuild is about to begin; the
        /// no-rebuild path keeps these hidden.
        /// </summary>
        void ShowRebuildIndicators();
    }
}
