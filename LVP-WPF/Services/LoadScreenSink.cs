using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;

namespace LVP_WPF.Services
{
    /// <summary>
    /// A Serilog sink that buffers formatted log lines in a thread-safe
    /// queue so WpfLoadProgress can drain them onto the load-screen TextBox
    /// on its own schedule, instead of every Log.X(...) call paying for a
    /// Dispatcher hop.
    ///
    /// Producer side (any thread): Emit() runs synchronously inside
    /// Log.Information() etc., formats the line, ConcurrentQueue.Enqueue
    /// is lock-free, returns immediately. No UI thread contact.
    ///
    /// Consumer side (UI thread): WpfLoadProgress runs a DispatcherTimer at
    /// ~100ms that calls TryDequeue in a loop, appends to TextBox in one
    /// batch per tick.
    ///
    /// Why the queue+timer indirection instead of just BeginInvoke-per-call:
    /// the previous load-screen logger did one BeginInvoke + AppendText +
    /// Focus/Scroll per log call. During scan that fires thousands of
    /// times in seconds, starves the WPF render thread, and stutters the
    /// loader animation. Batching collapses thousands of dispatcher hops
    /// down to ~10/sec at fixed cost.
    /// </summary>
    internal sealed class LoadScreenSink : ILogEventSink
    {
        // Static so App.xaml.cs can hand a sink instance to Serilog before
        // MainWindow exists, and WpfLoadProgress can drain the SAME queue
        // once the UI is constructed. The queue lives for the lifetime of
        // the process - that's fine, it just sits empty after load is done.
        public static readonly ConcurrentQueue<string> Queue = new();

        public void Emit(LogEvent logEvent)
        {
            // Compact format: "HH:mm:ss LVL message". Skip RenderMessage()'s
            // structured-property rendering allocations for already-rendered
            // text fields - the message template + args round-trip is fine
            // for human reading on the load screen.
            string ts = logEvent.Timestamp.ToString("HH:mm:ss");
            string lvl = logEvent.Level switch
            {
                LogEventLevel.Verbose     => "VRB",
                LogEventLevel.Debug       => "DBG",
                LogEventLevel.Information => "INF",
                LogEventLevel.Warning     => "WRN",
                LogEventLevel.Error       => "ERR",
                LogEventLevel.Fatal       => "FTL",
                _                         => "???"
            };
            Queue.Enqueue($"{ts} {lvl}  {logEvent.RenderMessage()}");
        }
    }
}
