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
#if DEBUG
            EventManager.RegisterClassHandler(typeof(Window), Keyboard.KeyUpEvent, new KeyEventHandler(GlobalKeyUp), true);
#endif
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

        private void GlobalKeyUp(object sender, KeyEventArgs e)
        {
            if (OptionDialog.shown)
            {
                return;
            }

            // Arrow-key cases all do the same shape (log + Move). Dispatch via
            // a tiny table so adding/changing one of them only touches one line.
            LayoutPoint lp = TcpSerialListener.layoutPoint;
            (string Label, (int x, int y) Dir)? mapped = e.Key switch
            {
                Key.Up    => ("up",    lp.up),
                Key.Down  => ("down",  lp.down),
                Key.Left  => ("left",  lp.left),
                Key.Right => ("right", lp.right),
                _ => null
            };
            if (mapped.HasValue)
            {
                Log.Debug(mapped.Value.Label);
                lp.Move(mapped.Value.Dir);
                return;
            }

            switch (e.Key)
            {
                case Key.Enter:
                    Log.Debug("enter");
                    TcpSerialListener.DoMouseClick();
                    break;
                case Key.Escape:
                    Log.Debug("esc");
                    TcpSerialListener.layoutPoint.CloseCurrWindow();
                    break;
                case Key.S:
                    Log.Debug("cartoons");

                    TcpSerialListener.StaThreadWrapper(() =>
                    {
                        TvShowWindow.PlayRandomCartoons();
                    });
                    break;
                case Key.W:
                    Log.Debug("historyWatch");

                    TcpSerialListener.StaThreadWrapper(() =>
                    {
                        TvShowWindow.PlayHistoryList();
                    });
                    break;
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            Log.Fatal(ex.ToString());
            NotificationDialog.Show("Error", $"Unhandled exception: {ex.Message}");
        }
    }
}
