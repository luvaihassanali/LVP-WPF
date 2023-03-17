using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

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
        }

        private void GlobalKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    System.Diagnostics.Debug.WriteLine("up");
                    TcpSerialListener.layoutPoint.Move(TcpSerialListener.layoutPoint.up);
                    break;
                case Key.Down:
                    System.Diagnostics.Debug.WriteLine("down");
                    TcpSerialListener.layoutPoint.Move(TcpSerialListener.layoutPoint.down);
                    break;
                case Key.Left:
                    System.Diagnostics.Debug.WriteLine("left");
                    TcpSerialListener.layoutPoint.Move(TcpSerialListener.layoutPoint.left);
                    break;
                case Key.Right:
                    System.Diagnostics.Debug.WriteLine("right");
                    TcpSerialListener.layoutPoint.Move(TcpSerialListener.layoutPoint.right);
                    break;
                case Key.Enter:
                    System.Diagnostics.Debug.WriteLine("enter");
                    TcpSerialListener.DoMouseClick();
                    break;
                case Key.Escape:
                    System.Diagnostics.Debug.WriteLine("esc");
                    TcpSerialListener.layoutPoint.CloseCurrWindow();
                    break;
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            GuiModel.Log(ex.ToString());
            NotificationDialog.Show("Error", "Unhandled exception: " + ex.Message);
        }
    }
}
