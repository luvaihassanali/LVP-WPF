using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;

namespace MouseMoverClient
{
    class Program
    {
        private static bool connectionEstablished;
        private static string esp8266ServerIp;
        private static int esp8266ServerPort;
        private static int joystickX;
        private static int joystickY;
        private static int opacity;

        private static System.Timers.Timer pollingTimer;
        private static SerialPort serialPort;
        private static TcpClient tcpClient;

        private static void Main(string[] args)
        {
            connectionEstablished = false;
            esp8266ServerIp = ConfigurationManager.AppSettings["Esp8266Ip"];
            esp8266ServerPort = Int32.Parse(ConfigurationManager.AppSettings["Esp8266Port"]);
            opacity = Int32.Parse(ConfigurationManager.AppSettings["Opacity"]);

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
            ConsoleHelper.SetCurrentFont("Segoe Mono Boot", 30);
            Console.Title = "";
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Blue;
            Console.CursorSize = 100;
            Console.SetWindowSize(50, 12);
            Console.SetBufferSize(50, 12);
            ConsoleHelper.SetWindowPosition(600, 680);
            ConsoleHelper.SetWindowTransparency(opacity);
            ConsoleHelper.HideTitleBar();
            ConsoleHelper.DisableQuickEditMode();

            pollingTimer = new System.Timers.Timer(6000); // esp timeout is 5s
            pollingTimer.Elapsed += OnTimedEventAsync;
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

        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine(e.ExceptionObject.ToString());
            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
        }

        private static void StartListener()
        {
            int cursorPos = 49;
            Log("Starting listener");
            while (!Console.KeyAvailable)
            {
                Log("Pinging server...");
                connectionEstablished = false;

                Ping pingSender = new Ping();
                PingOptions options = new PingOptions();
                options.DontFragment = true;
                string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"; // 32 bytes
                byte[] buffer = Encoding.ASCII.GetBytes(data);
                int timeout = 120;

                while (!connectionEstablished)
                {
                    PingReply reply = null;
                    try
                    {
                        reply = pingSender.Send(esp8266ServerIp, timeout, buffer, options);
                    }
                    catch { }

                    if (reply != null && reply.Status == IPStatus.Success)
                    {
                        Log("Ping success");
                        ConnectToServer();
                        connectionEstablished = true;
                    }
                    else
                    {
                        //Log("Destination host unreachable");
                        Console.CursorVisible = true;
                        Console.SetCursorPosition(cursorPos, Console.CursorTop);
                        cursorPos--;
                        if (cursorPos == 0) cursorPos = 49;
                    }
                    ConsoleHelper.StartBlink();
                    ConsoleHelper.CloseTeamViewerDialog();
                    CheckSerialConnection();
                }
            }

            Log("Stopping listener");
        }
       
        private static void CheckSerialConnection()
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
                        //Log("Serial port disconnected");
                    }
                }
            }
        }

        private static void ConnectToServer()
        {
            Log("Initializing TCP connection");
            try
            {
                tcpClient = new TcpClient();
                bool success = false;
                IAsyncResult result = null;

                result = tcpClient.BeginConnect(esp8266ServerIp, esp8266ServerPort, null, null);
                success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));

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
                    Log("Connected to server");
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
                    RunServerWorker(stream, result, data);
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

        private static void RunServerWorker(NetworkStream stream, IAsyncResult result, byte[] data)
        {
            byte[] bytes = new byte[256];
            int i;
            string buffer;

            while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
            {
                buffer = Encoding.ASCII.GetString(bytes, 0, i);
                Log("Received: " + buffer.Replace("\r\n", ""));

                if (buffer.Contains("initack"))
                {
                    Log("initack received");
                    Cursor.Position = new Point(960, 540); // Send cursor to centre of screen
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

                ConsoleHelper.CloseTeamViewerDialog();
            }

            Log("!! Stream end !!");
            stream.Close();
            tcpClient.EndConnect(result);
            tcpClient.Close();
        }

        private static void ParseTcpDataIn(string data)
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

        private static async void DoMouseMove()
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
                Cursor.Position = new Point(Cursor.Position.X + joystickX / divisor, Cursor.Position.Y + joystickY / divisor);
                await Task.Delay(1);
            }
        }

        private static void DoMouseClick()
        {
            uint X = (uint)Cursor.Position.X;
            uint Y = (uint)Cursor.Position.Y;
            ConsoleHelper.mouse_event(ConsoleHelper.MOUSEEVENTF_LEFTDOWN | ConsoleHelper.MOUSEEVENTF_LEFTUP, X, Y, 0, 0);
        }

        private static void DoMouseRightClick()
        {
            uint X = (uint)Cursor.Position.X;
            uint Y = (uint)Cursor.Position.Y;
            ConsoleHelper.mouse_event(ConsoleHelper.MOUSEEVENTF_RIGHTDOWN | ConsoleHelper.MOUSEEVENTF_RIGHTUP, X, Y, 0, 0);
        }

        private static void StartTimer()
        {
            pollingTimer.Enabled = true;
            pollingTimer.Start();
        }

        private static void StopTimer()
        {
            pollingTimer.Enabled = false;
            pollingTimer.Stop();
        }

        private static void OnTimedEventAsync(Object source, ElapsedEventArgs e)
        {
            Log("Polling timer stopped");
            pollingTimer.Enabled = false;
            pollingTimer.Stop();
            StartListener();
        }

        private static void InitializeSerialPort()
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

        private static void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
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
                        Cursor.Position = new Point(960, 540);
                        DoMouseClick();
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
                        ConsoleHelper.StartMatrix();
                        break;
                    default:
                        Log("Unknown msg received: " + msg);
                        break;
                }
            }
        }

        private static void Log(string message)
        {
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.WriteLine("{0}: {1}", DateTime.Now.ToString("> HH:mm:ss.fff"), message);
        }
    }

    // https://stackoverflow.com/questions/13656846/how-to-programmatic-disable-c-sharp-console-applications-quick-edit-mode/36720802#36720802
    // https://stackoverflow.com/questions/6554536/possible-to-get-set-console-font-size-in-c-sharp-net#:~:text=After%20running%20the%20application%20(Ctrl,option%20to%20adjust%20the%20size.
    
    #region ConsoleHelper

    public static class ConsoleHelper
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetStdHandle(int nStdHandle);

        // > Set window position x=600,y=680

        private const int SWP_NOSIZE = 0x0001;
        internal static void SetWindowPosition(int x, int y)
        {
            SetWindowPos(MyConsole, 0, 600, 680, 0, 0, SWP_NOSIZE);
        }

        // > Transparency

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const uint LWA_ALPHA = 0x2;

        [DllImport("user32.dll")]
        static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

        internal static void SetWindowTransparency(int opacity)
        {
            SetWindowLong(MyConsole, GWL_EXSTYLE, GetWindowLong(MyConsole, GWL_EXSTYLE) | WS_EX_LAYERED); // https://stackoverflow.com/questions/24110600/transparent-console-dllimport
            // Opacity = 0.5 = (255/2) = 128, 75 = 191, 80 = 204, 90 = 230
            SetLayeredWindowAttributes(MyConsole, 0, (byte)opacity, LWA_ALPHA);
        }

        // > Font 

        private const int FixedWidthTrueType = 54;
        private const int StandardOutputHandle = -11;

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

        internal static FontInfo[] SetCurrentFont(string font, short fontSize = 0)
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

                // Get settings from current font
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

        // > Disable quick edit mode

        private const uint ENABLE_QUICK_EDIT = 0x0040;
        // STD_INPUT_HANDLE (DWORD): -10 is the standard input device
        private const int STD_INPUT_HANDLE = -10;

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        internal static bool DisableQuickEditMode()
        {

            IntPtr consoleHandle = GetStdHandle(STD_INPUT_HANDLE);

            // get current console mode
            uint consoleMode;
            if (!GetConsoleMode(consoleHandle, out consoleMode))
            {
                // ERROR: Unable to get console mode.
                return false;
            }

            // Clear the quick edit bit in the mode flags
            consoleMode &= ~ENABLE_QUICK_EDIT;

            // set the new mode
            if (!SetConsoleMode(consoleHandle, consoleMode))
            {
                // ERROR: Unable to set console mode
                return false;
            }

            return true;
        }

        // > Hide title bar

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetConsoleWindow();

        private static IntPtr MyConsole = GetConsoleWindow();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        internal static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);

        internal static void HideTitleBar()
        {
            int style = GetWindowLong(MyConsole, -16);
            style &= -12582913;
            SetWindowLong(MyConsole, -16, style);
            SetWindowPos(MyConsole, 0, 0, 0, 0, 0, 0x27);
        }

        #region > Matrix

        private static int matrixCounter;
        private static Random randomPosition = new Random();
        private static int flowSpeed = 50;
        private static int fastFlow = flowSpeed + 30;
        private static int textFlow = flowSpeed + 500;
        private static ConsoleColor baseColor = ConsoleColor.DarkBlue;
        private static ConsoleColor fadedColor = ConsoleColor.White;

        private static int divisor = 10;
        private static int modVal = 9;
        private static int yPad = 2;
        private static int yPad1 = 2;

        internal static void StartMatrix()
        {
            Console.CursorVisible = false;
            int width, height;
            int[] y;
            Initialize(out width, out height, out y);
            while (true)
            {
                matrixCounter++;
                ColumnUpdate(width, height, y);
                if (matrixCounter > (3 * flowSpeed)) matrixCounter = 0;
            }
        }

        private static int YPositionFields(int yPosition, int height)
        {
            if (yPosition < 0) return yPosition + height;
            else if (yPosition < height) return yPosition;
            else return 0;

        }

        private static void Initialize(out int width, out int height, out int[] y)
        {
            height = Console.WindowHeight;
            width = Console.WindowWidth - 1;
            y = new int[width];
            Console.Clear();

            for (int x = 0; x < width; ++x) { y[x] += randomPosition.Next(height); }
        }

        private static void ColumnUpdate(int width, int height, int[] y)
        {
            int x;
            if (matrixCounter < flowSpeed)
            {
                for (x = 0; x < width; ++x)
                {
                    if (x % divisor == 1) Console.ForegroundColor = fadedColor;
                    else Console.ForegroundColor = baseColor;

                    Console.SetCursorPosition(x, y[x]);
                    Console.Write(Asciicharacters);

                    if (x % divisor == modVal) Console.ForegroundColor = fadedColor;
                    else Console.ForegroundColor = baseColor;

                    int temp = y[x] - yPad;
                    Console.SetCursorPosition(x, YPositionFields(temp, height));
                    Console.Write(Asciicharacters);

                    int temp1 = y[x] - yPad1;
                    Console.SetCursorPosition(x, YPositionFields(temp1, height));
                    Console.Write(' ');
                    y[x] = YPositionFields(y[x] + 1, height);
                }
            }
            else if (matrixCounter > flowSpeed && matrixCounter < textFlow)
            {
                for (x = 0; x < width; ++x)
                {
                    Console.SetCursorPosition(x, y[x]);
                    if (x % divisor == modVal) Console.ForegroundColor = fadedColor;
                    else Console.ForegroundColor = baseColor;

                    Console.Write(Asciicharacters);

                    y[x] = YPositionFields(y[x] + 1, height);

                }
            }
            else if (matrixCounter > fastFlow)
            {
                for (x = 0; x < width; ++x)
                {
                    Console.SetCursorPosition(x, y[x]);
                    Console.Write(' ');

                    int temp1 = y[x] - yPad1;
                    Console.SetCursorPosition(x, YPositionFields(temp1, height));
                    Console.Write(' ');

                    if (matrixCounter > fastFlow && matrixCounter < textFlow)
                    {
                        if (x % divisor == modVal) Console.ForegroundColor = fadedColor;
                        else Console.ForegroundColor = baseColor;

                        int temp = y[x] - yPad;
                        Console.SetCursorPosition(x, YPositionFields(temp, height));
                        Console.Write(Asciicharacters);

                    }
                    Console.SetCursorPosition(width / 2, height / 2);
                    y[x] = YPositionFields(y[x] + 1, height);
                }
            }
        }

        private static char Asciicharacters
        {
            get
            {
                int t = randomPosition.Next(10);

                if (t <= 2) return (char)('0' + randomPosition.Next(10));
                else if (t <= 4) return (char)('a' + randomPosition.Next(27));
                else if (t <= 6) return (char)('A' + randomPosition.Next(27));
                else return (char)(randomPosition.Next(32, 255));

            }
        }

        #endregion

        // > Close TeamViewer dialog

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.Dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr parentHandle, Win32Callback callback, IntPtr lParam);
        public delegate bool Win32Callback(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        internal static void CloseTeamViewerDialog()
        {
            Process p = Process.GetProcessesByName("TeamViewer")[0];
            List<IntPtr> rootWindows = GetRootWindowsOfProcess(p.Id);
            foreach (IntPtr rw in rootWindows)
            {
                string parentTitle = GetWindowTitle(rw);
                //Debug.WriteLine($"Parent: {parentTitle}");
                if (parentTitle.Equals("Sponsored session"))
                {
                    SendMessage(rw, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE = 0x0010;
                    break;
                }
                /*List<IntPtr> children = GetChildWindows(rw);
                foreach (IntPtr child in children)
                {
                    //Debug.WriteLine($"Child: {GetWindowTitle(child)}");
                }*/
            }
        }

        private static string GetWindowTitle(IntPtr hWnd)
        {
            var length = GetWindowTextLength(hWnd) + 1;
            var title = new StringBuilder(length);
            GetWindowText(hWnd, title, length);
            return title.ToString();
        }

        private static List<IntPtr> GetRootWindowsOfProcess(int pid)
        {
            List<IntPtr> rootWindows = GetChildWindows(IntPtr.Zero);
            List<IntPtr> dsProcRootWindows = new List<IntPtr>();
            foreach (IntPtr hWnd in rootWindows)
            {
                uint lpdwProcessId;
                GetWindowThreadProcessId(hWnd, out lpdwProcessId);
                if (lpdwProcessId == pid)
                    dsProcRootWindows.Add(hWnd);
            }
            return dsProcRootWindows;
        }

        private static List<IntPtr> GetChildWindows(IntPtr parent)
        {
            List<IntPtr> result = new List<IntPtr>();
            GCHandle listHandle = GCHandle.Alloc(result);
            try
            {
                Win32Callback childProc = new Win32Callback(EnumWindow);
                EnumChildWindows(parent, childProc, GCHandle.ToIntPtr(listHandle));
            }
            finally
            {
                if (listHandle.IsAllocated)
                    listHandle.Free();
            }
            return result;
        }

        private static bool EnumWindow(IntPtr handle, IntPtr pointer)
        {
            GCHandle gch = GCHandle.FromIntPtr(pointer);
            List<IntPtr> list = gch.Target as List<IntPtr>;
            if (list == null)
            {
                throw new InvalidCastException("GCHandle Target could not be cast as List<IntPtr>");
            }
            list.Add(handle);
            //  You can modify this to check to see if you want to cancel the operation, then return a null here
            return true;
        }

        // > Mouse clicks

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        internal static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

        internal const int MOUSEEVENTF_LEFTDOWN = 0x02;
        internal const int MOUSEEVENTF_LEFTUP = 0x04;
        internal const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        internal const int MOUSEEVENTF_RIGHTUP = 0x10;

        // Keep on top

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        internal static void StartBlink()
        {
            IntPtr hwnd = Process.GetCurrentProcess().MainWindowHandle;
            if (hwnd == GetForegroundWindow())
            {
                return;
            }
            SetForegroundWindow(hwnd);
        }
    }
    #endregion
}
