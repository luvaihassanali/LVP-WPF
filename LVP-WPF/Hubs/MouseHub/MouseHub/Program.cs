using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
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
        private static bool connectionEstablished;
        private static bool launched;
        private static string esp8266ServerIp;
        private static int esp8266ServerPort;
        private static int joystickX;
        private static int joystickY;

        private static System.Timers.Timer pollingTimer;
        private static SerialPort serialPort;
        private static TcpClient tcpClient;

        private static void Main(string[] args)
        {
            connectionEstablished = false;
            esp8266ServerIp = ConfigurationManager.AppSettings["Esp8266Ip"];
            esp8266ServerPort = Int32.Parse(ConfigurationManager.AppSettings["Esp8266Port"]);
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
            // Font height in pixels. The visible window is roughly
            // (cols * fontSize/2) wide x (rows * fontSize) tall, so this is
            // the dominant knob for "how big is the window on screen". At
            // 38 (the original hardcoded value), 76x31 cells ~= 1444x1178 px.
            // Defaulted to 20; bump it up via App.config if you want
            // chunkier text, down if you want a smaller window.
            short fontSize = (short)Int32.Parse(ConfigurationManager.AppSettings["ConsoleFontSize"] ?? "20");
            ConsoleHelper.SetCurrentFont("Segoe Mono Boot", fontSize);
            Console.Title = "";
            //Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;
            //Console.CursorSize = 1;
            Console.CursorVisible = false;

            // Read console layout from App.config so the window matches what
            // the shortcut's Properties dialog sets (Screen Buffer Size,
            // Window Size, Window Position). Without these the previously
            // hardcoded 112x27 @ (-8,-8) would clobber the shortcut layout
            // whenever LVP-WPF restarted MouseHub.
            int winW = Int32.Parse(ConfigurationManager.AppSettings["ConsoleWindowWidth"]);
            int winH = Int32.Parse(ConfigurationManager.AppSettings["ConsoleWindowHeight"]);
            int bufH = Int32.Parse(ConfigurationManager.AppSettings["ConsoleBufferHeight"]);
            int winLeft = Int32.Parse(ConfigurationManager.AppSettings["ConsoleWindowLeft"]);
            int winTop  = Int32.Parse(ConfigurationManager.AppSettings["ConsoleWindowTop"]);

#pragma warning disable CA1416 // Validate platform compatibility
            // Two launch paths to support:
            //   1. From MouseHub.lnk - conhost already applied the shortcut's
            //      Properties (size/buffer/position) before Main ran. State
            //      matches our target -> skip the resize entirely. This avoids
            //      the "buffer size would be too large" ArgumentOutOfRangeException
            //      we hit when we shrunk to 1x1 first: at the configured font
            //      size (Segoe Mono Boot @ 38pt) the implicit max-window-height
            //      after the shrink rejects 31 rows.
            //   2. From LVP-WPF's Process.Start(MouseHub.exe) restart - conhost
            //      uses default console host settings (not the .lnk), so we
            //      need to resize to match the shortcut layout.
            //
            // Either way: be defensive. If the configured dimensions don't fit
            // the current display state for whatever reason, log and continue
            // instead of crashing - the app is still functional at whatever
            // size conhost gave us.
            try
            {
                bool sizeMatches = Console.WindowWidth  == winW
                                && Console.WindowHeight == winH
                                && Console.BufferWidth  == winW
                                && Console.BufferHeight == bufH;

                if (!sizeMatches)
                {
                    // Window must always be <= buffer in both dimensions.
                    // Safe pattern when both might need to shrink: first
                    // shrink window to min(current, target), then set buffer
                    // (now guaranteed >= window), then set window to target.
                    int shrinkW = Math.Min(Console.WindowWidth,  winW);
                    int shrinkH = Math.Min(Console.WindowHeight, winH);
                    if (shrinkW < Console.WindowWidth || shrinkH < Console.WindowHeight)
                    {
                        Console.SetWindowSize(shrinkW, shrinkH);
                    }
                    Console.SetBufferSize(winW, bufH);
                    Console.SetWindowSize(winW, winH);
                }
            }
            catch (ArgumentOutOfRangeException ex)
            {
                // Configured size exceeds dwMaximumWindowSize for the current
                // font + display. Leave conhost's default and keep going.
                Console.WriteLine($"[MouseHub] Console resize skipped: {ex.Message}");
            }
#pragma warning restore CA1416 // Validate platform compatibility

            // ConsoleCenterOnScreen wins over ConsoleWindowLeft/Top when
            // set to true. Both keys stay so a user can flip back to fixed
            // placement by toggling this to false without having to rewrite
            // the Left/Top values.
            bool centerOnScreen =
                bool.TryParse(ConfigurationManager.AppSettings["ConsoleCenterOnScreen"], out bool c) && c;
            if (centerOnScreen)
            {
                ConsoleHelper.CenterOnPrimaryScreen();
            }
            else
            {
                ConsoleHelper.SetWindowPosition(winLeft, winTop);
            }
            int opacity = Int32.Parse(ConfigurationManager.AppSettings["Opacity"]);
            ConsoleHelper.SetWindowTransparency(opacity); // /256
            // Title bar (caption + min/max/close + system menu) intentionally
            // left enabled. Removed the HideTitleBar() call here so the
            // window behaves like a regular console - you can drag, minimize,
            // and close it from the chrome instead of having to kill the
            // process. NB: if you ever re-enable HideTitleBar, the
            // ConsoleWindowTop coord in App.config needs to drop ~30px to
            // compensate for the missing caption height.
            ConsoleHelper.DisableQuickEditMode();

            pollingTimer = new System.Timers.Timer(6000); // esp timeout is 5s
            pollingTimer.Elapsed += OnTimedEventAsync;
            pollingTimer.AutoReset = false;

            Task.Run(() => { ConsoleHelper.StartMatrix(); });

            // Self-test path: `MouseHub.exe --test-banner` (or -t) skips the
            // serial setup and just verifies the launch banner visually.
            // Lets the matrix render for 2 seconds so you see what it looks
            // like in steady state, then triggers ShowLaunchingBanner so
            // you can confirm the transition (matrix -> red flood -> banner)
            // is noticeable without needing the IR remote / ESP / serial
            // port hooked up. Press any key to exit.
            //
            // Use this on dev workstations before deploying changes to the
            // media server box. Keeps you from chasing "did it work?" cycles
            // through the real hardware path.
            if (args.Any(a => a == "--test-banner" || a == "-t"))
            {
                System.Threading.Thread.Sleep(2000);
                ConsoleHelper.ShowLaunchingBanner("TEST MODE - press any key to exit");
                Console.ReadKey(intercept: true);
                return;
            }

            // Dev key listener - press 'm' to manually fire the launch banner
            // without going through the real "power" serial command. Useful
            // when the release build's banner doesn't look the same as the
            // dry-run from --test-banner (different window chrome, transparency,
            // font scaling on the dev box vs the media server, etc.) and you
            // want to verify the visual against production conditions.
            //
            // Doesn't kick off the LVP-WPF Process.Start so MouseHub stays
            // alive and the banner stays on-screen - you can press 'm' again
            // to re-trigger if you tweak something and want to retest.
            //
            // try/catch keeps the task alive across spurious ReadKey errors
            // (e.g. console buffer in a weird state right after a banner
            // repaint); without it one exception would silently kill the
            // listener task and the 'm' shortcut would stop working with no
            // obvious cause.
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
                        if (key.KeyChar == 'm' || key.KeyChar == 'M')
                        {
                            ConsoleHelper.ShowLaunchingBanner("Press M to re-trigger");
                        }
                    }
                    catch
                    {
                        System.Threading.Thread.Sleep(50);
                    }
                }
            });

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
                        //Console.CursorVisible = true;
                        Console.SetCursorPosition(cursorPos, Console.CursorTop);
                        cursorPos--;
                        if (cursorPos == 0)
                        {
                            cursorPos = 49;
                        }
                    }
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
                Log($"ConnectToServerException: {e.Message}");
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
                string logStr = buffer.Replace("\r\n", "");
                Log($"------> Received: {logStr}");

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
                Log($"Error. Message incorrect format: {data}");
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
            string portNumber = ConfigurationManager.AppSettings["SerialPort"];
            serialPort = new SerialPort
            {
                PortName = $"COM{portNumber}",
                BaudRate = 9600,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Handshake = Handshake.None
            };
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
                        if (!launched)
                        {
                            // Send cursor to centre of screen
                            Cursor.Position = new Point(960, 540);
                            DoMouseClick();
                            string path = AppDomain.CurrentDomain.BaseDirectory;
#if DEBUG
                        path = path.Replace("Utilities\\MouseHub\\MouseHub\\bin\\Debug\\", "\\bin\\Debug\\net6.0-windows\\LVP-WPF.exe");
#else
                            path = $"{ConfigurationManager.AppSettings["LVP-WPF-Path"]}LVP-WPF.exe";
                            if (path.Contains("%USERPROFILE%"))
                            {
                                path = path.Replace("%USERPROFILE%", Environment.GetEnvironmentVariable("USERPROFILE"));
                            }
#endif
                            Process p = new Process();
                            p.StartInfo = new ProcessStartInfo();
                            p.StartInfo.FileName = path;
                            p.StartInfo.WorkingDirectory = path.Replace("LVP-WPF.exe", "");
                            p.Start();
                            // Stops the matrix loop, flood-fills the console
                            // with a red background, and draws a centered
                            // "LAUNCHING" box-banner + audio beep. The 5
                            // dashed WriteLines used to live here but the
                            // matrix Task.Run repainted them within ~50ms,
                            // so the user often missed the confirmation.
                            ConsoleHelper.ShowLaunchingBanner();
                            launched = true;
                        }
                        break;
                    default:
                        Log($"Unknown msg received: {msg}");
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
        //
        // Coords are the desired location of the visible CONTENT (client
        // area), not the outer window box. The function compensates for
        // whatever non-client chrome the OS gave the window: caption +
        // borders when the title bar is enabled, or just borders when it
        // isn't. That way the App.config values keep their meaning if you
        // ever toggle HideTitleBar - no need to hand-tune the Y coord by
        // SM_CYCAPTION pixels.
        //
        // The offset is computed at runtime instead of hardcoded so it
        // tracks Windows version, DPI scaling, and theme correctly:
        // GetWindowRect gives the outer-window rect in screen coords,
        // ClientToScreen(0,0) gives the screen coord of the client's
        // top-left, the deltas are exactly the chrome thicknesses.

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x, y; }

        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        private const int SWP_NOSIZE = 0x0001;
        internal static void SetWindowPosition(int x, int y)
        {
            int adjX = x;
            int adjY = y;
            try
            {
                if (GetWindowRect(MyConsole, out RECT wr))
                {
                    POINT cp = new POINT { x = 0, y = 0 };
                    if (ClientToScreen(MyConsole, ref cp))
                    {
                        // Non-client offset = how far below/right the client
                        // area starts vs the outer window's top-left edge.
                        // Subtract from the requested coords so the client
                        // lands exactly at (x, y).
                        adjX = x - (cp.x - wr.left);
                        adjY = y - (cp.y - wr.top);
                    }
                }
            }
            catch
            {
                // Win32 query failed for some reason - fall back to the
                // original "outer window at (x, y)" behavior rather than
                // refusing to place the window at all.
            }
            SetWindowPos(MyConsole, 0, adjX, adjY, 0, 0, SWP_NOSIZE);
        }

        // Center the console's OUTER window on the primary screen. Uses
        // Screen.PrimaryScreen.Bounds (full display) rather than
        // .WorkingArea (which excludes the taskbar): the media-server box
        // hides its taskbar, so the working area's exclusion band would
        // just push the window off-center by however many pixels the
        // taskbar would have taken. Must be called AFTER the console has
        // been resized to its final dimensions - GetWindowRect returns the
        // CURRENT window rect, so if this runs before Console.SetWindowSize
        // the centering is based on the pre-resize size and lands wrong.
        internal static void CenterOnPrimaryScreen()
        {
            if (!GetWindowRect(MyConsole, out RECT wr)) return;

            // Screen.PrimaryScreen is nullable in .NET 6+ (headless / no
            // display attached). No meaningful "center" without a display,
            // so bail cleanly and leave the window wherever conhost placed
            // it.
            Screen? primary = Screen.PrimaryScreen;
            if (primary == null) return;

            int windowWidth  = wr.right  - wr.left;
            int windowHeight = wr.bottom - wr.top;

            System.Drawing.Rectangle bounds = primary.Bounds;
            int centeredLeft = bounds.Left + (bounds.Width  - windowWidth)  / 2;
            int centeredTop  = bounds.Top  + (bounds.Height - windowHeight) / 2;

            // SWP_NOSIZE: move only, keep the current size. We center the
            // OUTER window (not the client area) so a titlebar-included
            // window still looks centered on screen.
            SetWindowPos(MyConsole, 0, centeredLeft, centeredTop, 0, 0, SWP_NOSIZE);
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
                    int ex = Marshal.GetLastWin32Error();
                    Console.WriteLine($"Set error {ex}");
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
                int er = Marshal.GetLastWin32Error();
                Console.WriteLine($"Get error {er}");
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
        private static ConsoleColor fadedColor = ConsoleColor.Blue;

        private static int divisor = 10;
        private static int modVal = 9;
        private static int yPad = 2;
        private static int yPad1 = 2;

        // Cooperative stop flag - the matrix loop runs in a Task.Run on the
        // thread pool with no native cancellation. Setting this from another
        // thread makes the loop exit on its next iteration. volatile so the
        // matrix thread sees the change without needing a memory barrier.
        private static volatile bool _matrixStopRequested;

        // Short-term pause flag - used by ShowLaunchingBanner to suspend the
        // matrix loop just long enough to paint the banner without racing
        // against the matrix's Console.ForegroundColor writes. The matrix
        // and the banner painter both touch Console.ForegroundColor as
        // shared state; without a pause, the matrix flips FG to DarkBlue
        // between our "FG=White" and our Write call, which renders the
        // banner text DarkBlue-on-DarkBlue (invisible). Once painting is
        // done and the guard rect is in place, the pause is released and
        // the matrix resumes (now skipping banner cells via the guard).
        private static volatile bool _matrixPaused;

        // Guard rectangle - matrix paint sites skip cells in this region so
        // a banner overlay can stay visible while the matrix keeps flowing
        // around it. _guardActive is checked first; only when true do the
        // _guardX1..Y2 ints matter, so the bounds writes don't need to be
        // atomic - they're guaranteed to be fully written before the
        // volatile flag goes true.
        private static volatile bool _guardActive;
        private static int _guardX1, _guardY1, _guardX2, _guardY2;

        private static bool IsGuarded(int x, int y) =>
            _guardActive && x >= _guardX1 && x <= _guardX2 && y >= _guardY1 && y <= _guardY2;

        // Inlined-friendly replacement for Console.SetCursorPosition + Write.
        // Skips the call entirely when the target cell is inside the active
        // guard region. The ForegroundColor / BackgroundColor state set by
        // the matrix before calling this still gets set even if we skip -
        // that's fine, those property writes don't paint anything until the
        // next actual Write.
        private static void Paint(int x, int y, char ch)
        {
            if (IsGuarded(x, y)) return;
            Console.SetCursorPosition(x, y);
            Console.Write(ch);
        }

        internal static void StartMatrix()
        {
            //Console.CursorVisible = false;
            Initialize(out int width, out int height, out int[] y);
            while (!_matrixStopRequested)
            {
                if (_matrixPaused)
                {
                    // Banner painter is mid-paint; back off for a tick so
                    // we don't fight it over Console.ForegroundColor.
                    System.Threading.Thread.Sleep(20);
                    continue;
                }
                matrixCounter++;
                if (matrixCounter == 50)
                {
                    matrixCounter = 0;
                }
                ColumnUpdate(width, height, y);
                if (matrixCounter > (3 * flowSpeed))
                {
                    matrixCounter = 0;
                }
            }
        }

        internal static void StopMatrix() => _matrixStopRequested = true;

        // Confirmation banner shown when the "power" serial command is
        // received. Stays visible while the matrix KEEPS flowing around it.
        //
        // Mechanism:
        //   1. Compute the banner's outer rectangle.
        //   2. Set _guardX1..Y2 to that rectangle, then set _guardActive
        //      true. From this moment, the matrix's Paint() helper skips
        //      any cell inside the rectangle - matrix continues advancing
        //      its column trails internally, but their paints "tunnel
        //      under" the banner without overwriting it.
        //   3. Sleep briefly so any in-flight ColumnUpdate iteration that
        //      started before _guardActive flipped has time to finish
        //      (otherwise stale paints from that iteration could land on
        //      banner cells we're about to paint).
        //   4. Paint the box border + title/subtitle text strips. The
        //      cells inside the box (not painted by us) keep whatever
        //      glyphs the matrix left there before the guard activated -
        //      "frozen matrix" behind the banner.
        //   5. Restore BackgroundColor to Black so subsequent matrix paints
        //      keep using a black backdrop instead of inheriting our
        //      DarkBlue, which would patch DarkBlue rectangles into the
        //      flowing matrix outside the banner.
        //
        // True transparency isn't possible in conhost (each cell has one
        // opaque bg color), but guard-skipping the banner region gets the
        // same visual effect: the matrix appears to flow around a static
        // overlay rather than blanking the whole screen.
        internal static void ShowLaunchingBanner(string subtitle = "Starting LVP-WPF...")
        {
            int w = Console.WindowWidth;
            int h = Console.WindowHeight;

            string title = "L A U N C H I N G";
            int innerW   = Math.Max(title.Length, subtitle.Length) + 14;
            int boxW     = innerW + 2;
            int boxH     = 7;

            int startCol = Math.Max(0, (w - boxW) / 2);
            int startRow = Math.Max(0, (h - boxH) / 2);
            int endCol   = startCol + boxW - 1;
            int endRow   = startRow + boxH - 1;

            // Set bounds first, then flip the volatile flag - that way the
            // matrix loop never sees half-written bounds.
            _guardX1 = startCol; _guardY1 = startRow;
            _guardX2 = endCol;   _guardY2 = endRow;
            _guardActive = true;

            // Pause the matrix entirely while we paint. The guard rectangle
            // alone isn't enough: the matrix and banner painter both write
            // to Console.ForegroundColor (and BackgroundColor), so without
            // the pause our "FG=White" can be clobbered by the matrix's
            // "FG=DarkBlue" mid-paint, rendering our text invisible
            // (DarkBlue-on-DarkBlue). 100 ms is comfortably longer than a
            // ColumnUpdate iteration; once we release the pause at the end
            // the guard rect keeps the matrix off banner cells permanently.
            _matrixPaused = true;
            System.Threading.Thread.Sleep(100);

            try
            {
                Console.BackgroundColor = ConsoleColor.DarkBlue;
                Console.ForegroundColor = ConsoleColor.White;
                // NO Console.Clear() - we deliberately leave the rest of
                // the screen alone so the matrix keeps flowing through it.

                // Build each row as a full string from edge to edge with
                // the interior filled with spaces (or centered text).
                // Painting each cell explicitly gives the interior a solid
                // DarkBlue wash; the matrix flows OUTSIDE the box (the
                // guard rect doesn't apply there).
                //
                // ASCII border chars (+, =, |) instead of Unicode
                // box-drawing (╔═╗║║╚═╝) for portability: the prod media
                // server's font fell back to one without glyphs in the
                // U+2550 box-drawing block, which rendered each border cell
                // as a "missing-glyph" white square. ASCII works on every
                // monospace font that's ever existed, including the cheap
                // fallback fonts.
                string horiz    = new string('=', innerW);
                string emptyMid = new string(' ', innerW);
                string[] lines  = new[] {
                    "|" + horiz + "|",
                    "|" + emptyMid + "|",
                    "|" + CenterIn(title,    innerW) + "|",
                    "|" + emptyMid + "|",
                    "|" + CenterIn(subtitle, innerW) + "|",
                    "|" + emptyMid + "|",
                    "|" + horiz + "|",
                };
                for (int i = 0; i < lines.Length; i++)
                {
                    Console.SetCursorPosition(startCol, startRow + i);
                    Console.Write(lines[i]);
                }

                // Restore BG to Black so the matrix's continuing paints
                // outside the guard rect land on a black backdrop instead
                // of inheriting DarkBlue (which would patch DarkBlue
                // rectangles into the flowing matrix - very visible).
                // Foreground gets reset by ColumnUpdate every iteration
                // so we don't need to touch it.
                Console.BackgroundColor = ConsoleColor.Black;
            }
            catch
            {
                // Console operations can fail if the window was closed
                // mid-paint. Swallow - the user doesn't need a stack
                // trace for a cosmetic banner.
            }
            finally
            {
                // Release the pause regardless of outcome - even on failure
                // we don't want to leave the matrix permanently paused.
                // The guard rect is still active so the matrix's resumed
                // iterations will skip the banner cells.
                _matrixPaused = false;
            }
        }

        // Returns `text` centered within a `width`-wide column, padded on
        // either side with spaces. If the text is longer than the column,
        // truncates from the right (shouldn't happen in normal banner use -
        // innerW is sized off the longest of {title, subtitle} + padding).
        private static string CenterIn(string text, int width)
        {
            if (text.Length >= width) return text.Substring(0, width);
            int left  = (width - text.Length) / 2;
            int right = width - text.Length - left;
            return new string(' ', left) + text + new string(' ', right);
        }

        private static int YPositionFields(int yPosition, int height)
        {
            if (yPosition < 0)
            {
                return yPosition + height;
            }
            else if (yPosition < height)
            {
                return yPosition;
            }
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
            // Every paint goes through Paint(x, y, ch) - it short-circuits
            // when the cell is inside the active guard rectangle so a
            // banner overlay (ShowLaunchingBanner) can stay visible while
            // this loop keeps animating everywhere else. y[x] still
            // advances even when the paint is skipped, so the matrix's
            // column trails "tunnel under" the banner and resume on the
            // other side.
            int x;
            if (matrixCounter < flowSpeed)
            {
                for (x = 0; x < width; ++x)
                {
                    if (x % divisor == 1) Console.ForegroundColor = fadedColor;
                    else                  Console.ForegroundColor = baseColor;
                    Paint(x, y[x], Asciicharacters);

                    if (x % divisor == modVal) Console.ForegroundColor = fadedColor;
                    else                       Console.ForegroundColor = baseColor;
                    Paint(x, YPositionFields(y[x] - yPad,  height), Asciicharacters);
                    Paint(x, YPositionFields(y[x] - yPad1, height), ' ');

                    y[x] = YPositionFields(y[x] + 1, height);
                }
            }
            else if (matrixCounter > flowSpeed && matrixCounter < textFlow)
            {
                for (x = 0; x < width; ++x)
                {
                    if (x % divisor == modVal) Console.ForegroundColor = fadedColor;
                    else                       Console.ForegroundColor = baseColor;
                    Paint(x, y[x], Asciicharacters);
                    y[x] = YPositionFields(y[x] + 1, height);
                }
            }
            else if (matrixCounter > fastFlow)
            {
                for (x = 0; x < width; ++x)
                {
                    Paint(x, y[x],                                  ' ');
                    Paint(x, YPositionFields(y[x] - yPad1, height), ' ');

                    if (matrixCounter > fastFlow && matrixCounter < textFlow)
                    {
                        if (x % divisor == modVal) Console.ForegroundColor = fadedColor;
                        else                       Console.ForegroundColor = baseColor;
                        Paint(x, YPositionFields(y[x] - yPad, height), Asciicharacters);
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
                if (t <= 2)
                {
                    return (char)('0' + randomPosition.Next(10));
                }
                else if (t <= 4)
                {
                    return (char)('a' + randomPosition.Next(27));
                }
                else if (t <= 6)
                {
                    return (char)('A' + randomPosition.Next(27));
                }
                else
                {
                    return (char)randomPosition.Next(32, 255);
                }
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
            Process p;
            try
            {
                p = Process.GetProcessesByName("TeamViewer")[0];
            }
            catch
            {
                return;
            }

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
            int length = GetWindowTextLength(hWnd) + 1;
            StringBuilder title = new StringBuilder(length);
            _ = GetWindowText(hWnd, title, length);
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

        // Previously: GetForegroundWindow + SetForegroundWindow + FlashWindowEx
        // were declared here to power a "draw attention on disconnect" hook
        // (ConsoleHelper.StartBlink). The whole feature got removed - it
        // was stealing focus from LVP-WPF on every failed ESP ping, and the
        // taskbar-flash replacement was still distracting. The connection
        // state is visible in the console output, that's enough.
    }
    #endregion
}
