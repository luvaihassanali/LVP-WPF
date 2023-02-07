using System;
using System.Configuration;
using System.IO.Ports;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;

namespace MouseMoverClient
{
    class Program
    {
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetConsoleWindow();

        private static IntPtr MyConsole = GetConsoleWindow();

        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const int MOUSEEVENTF_RIGHTUP = 0x10;
        private const int SWP_NOSIZE = 0x0001;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const uint LWA_ALPHA = 0x2;

        static bool connectionEstablished = true;
        static string esp8266ServerIp = ConfigurationManager.AppSettings["Esp8266Ip"];
        static int esp8266ServerPort = 3000;
        static int joystickX;
        static int joystickY;
        static TcpClient tcpClient;
        static SerialPort serialPort;
        static System.Timers.Timer pollingTimer;

        static unsafe void Main(string[] args)
        {
            Console.Title = "";
            Console.SetWindowSize(60, 10);
            Console.SetBufferSize(60, 10);
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Blue;
            Console.Clear();

            ConsoleHelper.SetCurrentFont("Cascadia Code", 32);
            SetWindowPos(MyConsole, 0, 625, 10, 0, 0, SWP_NOSIZE);
            SetWindowLong(MyConsole, GWL_EXSTYLE, GetWindowLong(MyConsole, GWL_EXSTYLE) | WS_EX_LAYERED); // https://stackoverflow.com/questions/24110600/transparent-console-dllimport
            SetLayeredWindowAttributes(MyConsole, 0, 128, LWA_ALPHA); // Opacity = 0.5 = (255/2) = 128

            pollingTimer = new System.Timers.Timer(6000); // esp timeout is 5s
            pollingTimer.Elapsed += OnTimedEvent;
            pollingTimer.AutoReset = false;

            InitializeSerialPort();
            StartListener();

            if (tcpClient != null)
            {
                tcpClient.Close();
                tcpClient.Dispose();
            }

            if (pollingTimer != null)
            {
                pollingTimer.Stop();
                pollingTimer.Dispose();
            }
        }

        static void StartListener()
        {
            Log("Start");
            while (!Console.KeyAvailable)
            {
                Log("Pinging server...");
                connectionEstablished = false;

                Ping pingSender = new Ping();
                PingOptions options = new PingOptions();
                options.DontFragment = true;
                string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"; //32 bytes
                byte[] buffer = Encoding.ASCII.GetBytes(data);
                int timeout = 120;

                while (!connectionEstablished)
                {
                    PingReply reply = null;
                    try
                    {
                        reply = pingSender.Send(esp8266ServerIp, timeout, buffer, options);
                    }
                    catch
                    { }

                    if (reply.Status == IPStatus.Success)
                    {
                        Log("Ping success");
                        ConnectToServer();
                        connectionEstablished = true;
                    }
                    else
                    {
                        Log("Destination host unreachable");
                    }

                    CheckSerialConnection();
                }
            }
            Log("Stop");
        }

        static private void CheckSerialConnection()
        {
            if (serialPort != null)
            {
                if (!serialPort.IsOpen)
                {
                    try
                    {
                        serialPort.Open();
                        Log("Connected to serial port");
                    }
                    catch
                    {
                        Log("Serial port disconnected");
                    }
                }
            }
        }

        static void ConnectToServer()
        {
            Log("Initializing TCP connection");
            try
            {
                tcpClient = new TcpClient();
                bool success = false;
                IAsyncResult result = null;

                result = tcpClient.BeginConnect(esp8266ServerIp, esp8266ServerPort, null, null);
                success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));

                while (!success)
                {
                    Log("Cannot connect to server. Trying again");
                    return;
                }

                byte[] data = Encoding.ASCII.GetBytes("zzzz");
                NetworkStream stream = null;

                try
                {
                    stream = tcpClient.GetStream();
                    Log("Connected.");
                }
                catch (InvalidOperationException)
                {
                    Log("Server not ready. Trying again...");
                    return;
                }

                stream.Write(data, 0, data.Length);
                Log("Sent: init");
                StartTimer();

                while (true)
                {
                    int i;
                    byte[] bytes = new byte[256];
                    string buffer = null;

                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        buffer = Encoding.ASCII.GetString(bytes, 0, i);
                        Log("Received: " + buffer.Replace("\r\n", ""));

                        if (buffer.Contains("initack"))
                        {
                            // Send cursor to centre of screen
                            Cursor.Position = new System.Drawing.Point(960, 540);
                            DoMouseClick();
                            StopTimer();
                            StartTimer();
                        }

                        if (buffer.Contains("ka"))
                        {
                            StopTimer();
                            Log("Sending ack");
                            data = Encoding.ASCII.GetBytes("ack");
                            stream = tcpClient.GetStream();
                            stream.Write(data, 0, data.Length);
                            StartTimer();
                        }

                        if (!buffer.Contains("ok") && !buffer.Contains("ka") && !buffer.Contains("initack"))
                        {
                            ParseTcpDataIn(buffer);
                        }
                    }

                    Log("Stream end. Press any key");
                    stream.Close();
                    tcpClient.EndConnect(result);
                    tcpClient.Close();
                }
            }
            catch (Exception e)
            {
                Log("ConnectToServerException: " + e.Message);
            }
            finally
            {
                if (tcpClient != null)
                {
                    tcpClient.Close();
                    tcpClient.Dispose();
                }
            }
        }

        static void ParseTcpDataIn(string data)
        {
            string[] dataSplit = data.Split(',');
            if (dataSplit.Length > 6)
            {
                Log("Error. Message incorrect format: " + data);
                return;
            }
            joystickX = Int32.Parse(dataSplit[0]);
            joystickY = Int32.Parse(dataSplit[1]);
            int buttonState = Int32.Parse(dataSplit[2]);
            int buttonTwoState = Int32.Parse(dataSplit[4].Replace("\r\n", ""));
            int buttonThreeState = Int32.Parse(dataSplit[3].Replace("\r\n", ""));

            if (buttonTwoState == 0 && buttonThreeState == 0)
            {
                System.Diagnostics.Process.Start("taskmgr.exe");
            }

            if (buttonState == 0 || buttonThreeState == 0)
            {
                DoMouseClick();
                return;
            }

            if (buttonTwoState == 0)
            {
                DoMouseRightClick();
                return;
            }

            DoMouseMove();
        }

        static async void DoMouseMove()
        {
            //joystickX = -joystickX;
            joystickY = -joystickY;
            int divisor = 20;
            if ((joystickX > 0 && joystickX < 150) || (joystickX < 0 && joystickX > -150))
            {
                divisor = 60;
            }
            else if ((joystickX > 150 && joystickX < 400) || (joystickX < -150 && joystickX > -400))
            {
                divisor = 40;
            }

            for (int i = 0; i < 15; i++)
            {
                Cursor.Position = new System.Drawing.Point(Cursor.Position.X + joystickX / divisor, Cursor.Position.Y + joystickY / divisor);
                await Task.Delay(1);
            }
        }

        static void DoMouseClick()
        {
            uint X = (uint)Cursor.Position.X;
            uint Y = (uint)Cursor.Position.Y;
            mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, X, Y, 0, 0);
        }

        static void DoMouseRightClick()
        {
            uint X = (uint)Cursor.Position.X;
            uint Y = (uint)Cursor.Position.Y;
            mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, X, Y, 0, 0);
        }

        static void StartTimer()
        {
            pollingTimer.Enabled = true;
            pollingTimer.Start();
        }

        static void StopTimer()
        {
            pollingTimer.Enabled = false;
            pollingTimer.Stop();
        }

        static void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            Log("Polling timer stopped");
            pollingTimer.Enabled = false;
            pollingTimer.Stop();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            StartListener();
        }

        static public void InitializeSerialPort()
        {
            serialPort = new SerialPort();
            string portNumber = ConfigurationManager.AppSettings["SerialPort"];
            serialPort.PortName = "COM" + portNumber;
            serialPort.BaudRate = 9600;
            serialPort.DataBits = 8;
            serialPort.Parity = Parity.None;
            serialPort.StopBits = StopBits.One;
            serialPort.Handshake = Handshake.None;
            serialPort.DataReceived += SerialPort_DataReceived;

            try
            {
                serialPort.Open();
                Log("Connected to serial port");
            }
            catch
            {
                Log("Serial port disconnected");
            }
        }

        static private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort serialPort = (SerialPort)sender;
            if (e.EventType == SerialData.Chars)
            {
                string msg = serialPort.ReadLine();
                msg = msg.Replace("\r", "");
                switch (msg)
                {
                    case "power":
                        // Send cursor to centre of screen
                        Cursor.Position = new System.Drawing.Point(960, 540);
                        DoMouseClick();
                        string launchMsg = @"










   ██╗      █████╗ ██╗   ██╗███╗   ██╗ ██████╗██╗  ██╗██╗
   ██║     ██╔══██╗██║   ██║████╗  ██║██╔════╝██║  ██║██║
   ██║     ███████║██║   ██║██╔██╗ ██║██║     ███████║██║
   ██║     ██╔══██║██║   ██║██║╚██╗██║██║     ██╔══██║╚═╝
   ███████╗██║  ██║╚██████╔╝██║ ╚████║╚██████╗██║  ██║██╗
   ╚══════╝╚═╝  ╚═╝ ╚═════╝ ╚═╝  ╚═══╝ ╚═════╝╚═╝  ╚═╝╚═╝
";
                        Console.WriteLine(launchMsg);
                        string path = AppDomain.CurrentDomain.BaseDirectory;
#if DEBUG
                        path = path.Replace("Utilities\\MouseHub\\MouseHub\\bin\\Debug\\", "\\bin\\Debug\\net6.0-windows\\LVP-WPF.exe");
#else
                        path = ConfigurationManager.AppSettings["LVP-WPF-Path"] + "LVP-WPF.exe";
                        if (path.Contains("%USERPROFILE%")) { path = path.Replace("%USERPROFILE%", Environment.GetEnvironmentVariable("USERPROFILE")); }
#endif
                        System.Diagnostics.Process p = new System.Diagnostics.Process();
                        p.StartInfo = new System.Diagnostics.ProcessStartInfo();
                        p.StartInfo.FileName = path;
                        p.StartInfo.WorkingDirectory = path.Replace("LVP-WPF.exe", "");
                        p.Start();
                        break;
                    default:
                        Log("Unknown msg received: " + msg);
                        break;
                    //To-do: Add restart case
                }
            }
        }

        static void Log(string message)
        {
            Console.WriteLine("{0}: {1}", DateTime.Now.ToString("> HH:mm:ss.fff"), message);
        }
    }

    // https://stackoverflow.com/questions/6554536/possible-to-get-set-console-font-size-in-c-sharp-net#:~:text=After%20running%20the%20application%20(Ctrl,option%20to%20adjust%20the%20size.
    #region ConsoleHelper

    public static class ConsoleHelper
    {
        private const int FixedWidthTrueType = 54;
        private const int StandardOutputHandle = -11;

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetStdHandle(int nStdHandle);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetCurrentConsoleFontEx(IntPtr hConsoleOutput, bool MaximumWindow, ref FontInfo ConsoleCurrentFontEx);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool GetCurrentConsoleFontEx(IntPtr hConsoleOutput, bool MaximumWindow, ref FontInfo ConsoleCurrentFontEx);

        private static readonly IntPtr ConsoleOutputHandle = GetStdHandle(StandardOutputHandle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct FontInfo
        {
            internal int cbSize;
            internal int FontIndex;
            internal short FontWidth;
            public short FontSize;
            public int FontFamily;
            public int FontWeight;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string FontName;
        }

        public static FontInfo[] SetCurrentFont(string font, short fontSize = 0)
        {
            FontInfo before = new FontInfo
            {
                cbSize = Marshal.SizeOf<FontInfo>()
            };

            if (GetCurrentConsoleFontEx(ConsoleOutputHandle, false, ref before))
            {

                FontInfo set = new FontInfo
                {
                    cbSize = Marshal.SizeOf<FontInfo>(),
                    FontIndex = 0,
                    FontFamily = FixedWidthTrueType,
                    FontName = font,
                    FontWeight = 400,
                    FontSize = fontSize > 0 ? fontSize : before.FontSize
                };

                // Get some settings from current font.
                if (!SetCurrentConsoleFontEx(ConsoleOutputHandle, false, ref set))
                {
                    var ex = Marshal.GetLastWin32Error();
                    Console.WriteLine("Set error " + ex);
                    throw new System.ComponentModel.Win32Exception(ex);
                }

                FontInfo after = new FontInfo
                {
                    cbSize = Marshal.SizeOf<FontInfo>()
                };
                GetCurrentConsoleFontEx(ConsoleOutputHandle, false, ref after);

                return new[] { before, set, after };
            }
            else
            {
                var er = Marshal.GetLastWin32Error();
                Console.WriteLine("Get error " + er);
                throw new System.ComponentModel.Win32Exception(er);
            }
        }
    }

    #endregion
}
