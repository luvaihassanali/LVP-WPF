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
            if (!Directory.Exists(logPath)) Directory.CreateDirectory(logPath);

            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Debug()
            .WriteTo.File(path: $"{logPath}LVP-WPF-.log",
            rollingInterval: RollingInterval.Month,
            rollOnFileSizeLimit: true)
            .CreateLogger();
        }

        private void GlobalKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    Log.Debug("up");
                    TcpSerialListener.layoutPoint.Move(TcpSerialListener.layoutPoint.up);
                    break;
                case Key.Down:
                    Log.Debug("down");
                    TcpSerialListener.layoutPoint.Move(TcpSerialListener.layoutPoint.down);
                    break;
                case Key.Left:
                    Log.Debug("left");
                    TcpSerialListener.layoutPoint.Move(TcpSerialListener.layoutPoint.left);
                    break;
                case Key.Right:
                    Log.Debug("right");
                    TcpSerialListener.layoutPoint.Move(TcpSerialListener.layoutPoint.right);
                    break;
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
