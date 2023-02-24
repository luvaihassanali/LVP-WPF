using System;
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
        }
        // make serialPortEnabled static?
        private void GlobalKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    TcpSerialListener.layoutPoint.Move(TcpSerialListener.layoutPoint.up);
                    break;
                case Key.Down:
                    TcpSerialListener.layoutPoint.Move(TcpSerialListener.layoutPoint.down);
                    break;
                case Key.Left:
                    TcpSerialListener.layoutPoint.Move(TcpSerialListener.layoutPoint.left);
                    break;
                case Key.Right:
                    TcpSerialListener.layoutPoint.Move(TcpSerialListener.layoutPoint.right);
                    break;
                case Key.Enter:
                    TcpSerialListener.DoMouseClick();
                    break;
                case Key.Escape:
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
