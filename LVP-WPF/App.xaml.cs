using LVP_WPF.Windows;
using Serilog;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace LVP_WPF
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            // Global keyboard handler is active in BOTH Debug and Release.
            // The IR remote is the primary input on the production media
            // server, but keyboard works as a fallback (server keyboard or
            // remote desktop session) so users aren't locked out if the
            // remote / serial cable comes loose.
            //
            // KeyDown (not KeyUp): IR remote dispatch is press-driven, and
            // KeyDown gives Windows' auto-repeat behavior for arrows when
            // held - which mimics held-button IR repeat reasonably well.
            // Non-arrow keys filter out IsRepeat inside GlobalKeyDown so a
            // held action key doesn't fire as a stream of one-shots.
            EventManager.RegisterClassHandler(typeof(Window), Keyboard.KeyDownEvent, new KeyEventHandler(GlobalKeyDown), true);
            string baseFolder = AppDomain.CurrentDomain.BaseDirectory;
            string logPath = $"{baseFolder}logs\\";
            if (!Directory.Exists(logPath))
            {
                Directory.CreateDirectory(logPath);
            }

            // Pipeline-wide min level stays at Debug so the in-process Debug
            // sink (visible in VS Output > "Debug" pane during F5) still gets
            // everything. But the FILE sink is restricted to Information+:
            //
            // The scanner emits one Log.Debug per directory it visits. For a
            // medium-or-larger library that's thousands of structured
            // LogEvent allocations + thousands of small synchronous disk
            // writes during MediaLibrary.Initialize, which (a) GC-pressures
            // the worker thread enough to stutter WPF's render thread and
            // (b) directly contends for disk bandwidth with the JSON load
            // happening right after. Skipping them at the file sink keeps
            // them visible while debugging in VS but stops the startup
            // jitter.
            //
            // .WriteTo.Debug() routes through System.Diagnostics.Debug.WriteLine,
            // which appears in Visual Studio's Output window under the "Debug"
            // pane (View > Output, "Show output from: Debug") when launched
            // with F5. Make sure that pane is selected and that
            // Tools > Options > Debugging > General >
            // "Redirect All Output Window Text to the Immediate Window" is OFF.
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Debug()
                .WriteTo.File(path: $"{logPath}LVP-WPF-.log",
                    restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information,
                    rollingInterval: RollingInterval.Month,
                    rollOnFileSizeLimit: true)
                // Load-screen TextBox sink. Information+ so per-directory
                // Debug noise from the scanner doesn't drown the load screen.
                // WpfLoadProgress drains the queue on a 100ms DispatcherTimer.
                .WriteTo.Sink(new LVP_WPF.Services.LoadScreenSink(),
                    restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information)
                .CreateLogger();
        }

        // Routes keyboard input through the same IrSerialReader.OnCommand
        // pipeline the IR remote uses, so a keypress is indistinguishable
        // from a serial-port command in the rest of the system: same
        // debounce, same threading marshalling for player calls, same
        // logging.
        //
        // Keyboard map (also documented in README.md - keep in sync):
        //
        //   Key                       IR-equivalent    Effect
        //   ─────────────────────────  ──────────────  ──────────────────────────────
        //   Up / Down / Left / Right  up/down/left/   navigation (arrow nav)
        //                             right
        //   Enter                     enter           activate focused control
        //   Esc                       return          back / close current window
        //   Space                     play            toggle play/pause in player
        //   F                         fastforward     +30s
        //   R                         rewind          -30s
        //   End                       forward         jump to end
        //   Home                      backward        jump to start
        //
        // Removed: 'S' (cartoons) and 'W' (history) - these used to be
        // dev shortcuts when there was no UI for them; now exposed as
        // dedicated MainWindow buttons (gui.shuffleButton / historyButton).
        private void GlobalKeyDown(object sender, KeyEventArgs e)
        {
            if (OptionDialog.shown) return;

            // Skip modifier-combos so Ctrl+L / Alt+F4 / Shift+arrows etc.
            // don't fire as bare commands. The IR remote has no modifier
            // concept; only un-modifiered keypresses should mimic it.
            if (Keyboard.Modifiers != ModifierKeys.None) return;

            string msg = e.Key switch
            {
                Key.Up     => "up",
                Key.Down   => "down",
                Key.Left   => "left",
                Key.Right  => "right",
                Key.Enter  => "enter",
                Key.Escape => "return",
                Key.Space  => "play",
                Key.F      => "fastforward",
                Key.R      => "rewind",
                Key.End    => "forward",
                Key.Home   => "backward",
                _          => null
            };
            if (msg == null) return;

            // Auto-repeat policy: arrows keep their stream (IR remote behaves
            // the same when held - users expect to hold to scroll a long
            // grid). Action keys filter IsRepeat so a held Enter / Space
            // doesn't fire as a thousand one-shots. IrSerialReader's
            // debounce window inside OnCommand catches anything that slips
            // through within ~300ms anyway.
            bool isArrow = e.Key is Key.Up or Key.Down or Key.Left or Key.Right;
            if (!isArrow && e.IsRepeat) return;

            // tcpWorker is null until MainWindow_Loaded constructs it (after
            // library init), and IrReader is null if the user disabled
            // SerialPortEnabled. Guard both so a keypress during startup or
            // in a SerialPortEnabled=false config doesn't NRE.
            //
            // Fully qualified: in App's context "MainWindow" otherwise
            // resolves to Application.MainWindow (the inherited property),
            // which is just a System.Windows.Window reference - not our
            // class with the static tcpWorker field.
            LVP_WPF.MainWindow.tcpWorker?.IrReader?.OnCommand(msg, source: "kbd");
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            Log.Fatal(ex.ToString());
            NotificationDialog.Show("Error", $"Unhandled exception: {ex.Message}");
        }
    }
}
