namespace LVP_WPF.Services
{
    /// <summary>
    /// What MediaLibrary needs from its host UI during startup: a way to
    /// switch on the rebuild-indicator widgets (progress bar, coffee gif)
    /// when a TMDB rebuild is about to start, and a way to stop the
    /// load-screen log drain once the load is finished.
    ///
    /// Per-line log routing isn't on this interface: callers just use
    /// Serilog's Log.X(...) and the LoadScreenSink picks them up
    /// automatically.
    /// </summary>
    internal interface ILoadProgress
    {
        /// <summary>
        /// Make the rebuild-indicator UI (progress bar + animated spinner)
        /// visible. Called once, when a rebuild is about to begin; the
        /// no-rebuild path keeps these hidden.
        /// </summary>
        void ShowRebuildIndicators();

        /// <summary>
        /// Stop the load-screen log drain timer. Called by MainWindow once
        /// the load + tile-population are done and the load grid is being
        /// hidden, so the timer isn't ticking forever in the background.
        /// </summary>
        void StopLogDrain();
    }
}
