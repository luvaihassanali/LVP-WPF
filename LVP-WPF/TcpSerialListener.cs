using LVP_WPF.Services;
using LVP_WPF.Windows;
using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace LVP_WPF
{
    public class TcpSerialListener
    {

        private bool connectionEstablished;
        private string esp8266ServerIp;
        private int esp8266ServerPort;
        private bool esp8266Enabled;
        private bool workerThreadRunning;

        internal GuiModel gui;
        public static LayoutPoint layoutPoint;
        private static System.Timers.Timer pollingTimer;
        private static Thread dispatcherThread;
        private static Dispatcher featureDispatcher;

        private TcpClient tcpClient;
        private Thread workerThread;
        private IrSerialReader serialReader;

        public TcpSerialListener(GuiModel g)
        {
            dispatcherThread = null;
            featureDispatcher = null;
            gui = g;
            connectionEstablished = false;
            workerThreadRunning = false;
            esp8266ServerIp = AppConfig.Esp8266Ip;
            esp8266ServerPort = AppConfig.Esp8266Port;
            esp8266Enabled = AppConfig.Esp8266Enabled;
            serialReader = new IrSerialReader(g);
            layoutPoint = new LayoutPoint(g);
            if (CursorConfig.HideCursor)
            {
                Application.Current.Dispatcher.Invoke(new Action(() => { Mouse.OverrideCursor = Cursors.None; }));
            }
        }

        public void StartThread()
        {
            if (serialReader.Enabled)
            {
                serialReader.Initialize();
            }
            try
            {
                if (workerThread == null)
                {
                    workerThread = new Thread(new ThreadStart(this.StartListener));
                    workerThread.SetApartmentState(ApartmentState.STA);
                    workerThread.IsBackground = true;
                    workerThread.Name = "LVP_WPF TcpSerialListener thread";
                    workerThreadRunning = true;
                    workerThread.Start();
                }
            }
            catch (Exception e)
            {
                DebugLog(e.Message);
            }
        }

        public void StopThread()
        {
            if (pollingTimer != null)
            {
                pollingTimer.Stop();
                pollingTimer.Dispose();
                pollingTimer = null;
            }

            if (tcpClient != null)
            {
                tcpClient.Close();
                tcpClient.Dispose();
            }

            if (workerThread != null)
            {
                workerThreadRunning = false;
                workerThread.Interrupt();
                workerThread.Join();
                workerThread = null;
            }
        }

        private void StartListener()
        {
            while (workerThreadRunning && (esp8266Enabled || serialReader.Enabled))
            {
                PollConnections();
            }
        }

        private void PollConnections()
        {
            if (esp8266Enabled)
            {
                DebugLog("Pinging server...");
            }
            connectionEstablished = false;

            Ping pingSender = new Ping();
            PingOptions options = new PingOptions
            {
                DontFragment = true
            };
            string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"; //32 bytes
            byte[] buffer = Encoding.ASCII.GetBytes(data);
            int timeout = 120;

            while (!connectionEstablished && workerThreadRunning)
            {
                if (esp8266Enabled)
                {
                    PingReply reply = null;
                    try { reply = pingSender.Send(esp8266ServerIp, timeout, buffer, options); }
                    catch { }

                    if (reply != null && reply.Status == IPStatus.Success)
                    {
                        DebugLog("Ping success");
                        ConnectToServer();
                        connectionEstablished = true;
                    }
                    else
                    {
                        //DebugLog("Destination host unreachable");
                    }
                }
                ComInterop.CloseTeamViewerDialog();
                serialReader.CheckConnection();
            }

            pingSender.Dispose();
        }

        private void ConnectToServer()
        {
            DebugLog("Initializing TCP connection");
            try
            {
                tcpClient = new TcpClient();
                bool success = false;
                IAsyncResult result = null;

                result = tcpClient.BeginConnect(esp8266ServerIp, esp8266ServerPort, null, null);
                success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));

                if (!success)
                {
                    DebugLog("Cannot connect to server");
                    return;
                }

                byte[] data = Encoding.ASCII.GetBytes("zzzz");
                NetworkStream stream = null;
                try
                {
                    stream = tcpClient.GetStream();
                    DebugLog("Connected to server");
                }
                catch (Exception ex)
                {
                    DebugLog($"Server not ready. Trying again ({ex.Message})");
                    return;
                }

                stream.Write(data, 0, data.Length);
                DebugLog("Sent init");
                StartTimer();

                // RunServerWorker reads the stream until EOF, then closes
                // both the stream and the tcpClient. The old `while (true)`
                // here was a no-op in practice: a second call would Read
                // from a closed stream, throw, and bail to the outer catch.
                RunServerWorker(stream, result, data);
            }
            catch (Exception e)
            {
                DebugLog($"MouseWorker_ConnectToServerException: {e.Message}");
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

        private void RunServerWorker(NetworkStream stream, IAsyncResult result, byte[] data)
        {
            byte[] bytes = new byte[256];
            int i;
            string buffer;

            while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
            {
                buffer = Encoding.ASCII.GetString(bytes, 0, i);
                DebugLog($"Received: {buffer.Replace("\r\n", "")}");

                if (buffer.Contains("initack"))
                {
                    DebugLog("initack received");
                    ComInterop.SetCursorPos(CursorConfig.HideCursorX, CursorConfig.HideCursorY);
                    DoMouseClick();
                    StopTimer();
                    StartTimer();
                }

                if (buffer.Contains("ka"))
                {
                    StopTimer();
                    DebugLog("Sending ack");
                    data = Encoding.ASCII.GetBytes("ack");
                    stream = tcpClient.GetStream();
                    stream.Write(data, 0, data.Length);
                    StartTimer();
                }

                if (!IsControlMessage(buffer))
                {
                    ParseTcpDataIn(buffer);
                }

                ComInterop.CloseTeamViewerDialog();
            }

            DebugLog("!! Stream end !!");
            stream.Close();
            tcpClient.EndConnect(result);
            tcpClient.Close();
        }

        private void ParseTcpDataIn(string data)
        {
            if (CursorConfig.HideCursor)
            {
                Application.Current.Dispatcher.Invoke(new Action(() => { Mouse.OverrideCursor = Cursors.Arrow; }));
            }

            JoystickReading? reading = JoystickReading.TryParse(data);
            if (reading == null)
            {
                DebugLog($"Error. Message incorrect format: {data}");
                return;
            }
            JoystickReading r = reading.Value;

            // Easter egg: holding scroll + click at the same time pops Task Manager.
            if (r.ScrollButton && r.ClickButton)
            {
                System.Diagnostics.Process.Start("taskmgr.exe");
            }

            if (r.JoystickButton || r.ClickButton)
            {
                DoMouseClick();
                return;
            }

            if (r.ScrollButton)
            {
                // Mouse-wheel mode: send vertical scroll, magnitude scaled up 4x.
                ComInterop.mouse_event(ComInterop.MOUSEEVENTF_WHEEL, 0, 0, (uint)(r.Y * 4), 0);
            }
            else
            {
                DoMouseMove(r.X, r.Y);
            }
        }

        // The joystick reports magnitude as an analog value; we divide it
        // down to a per-tick pixel delta. Larger divisor = slower cursor:
        // tiny deflections (|x| < 150) get the slowest, mid deflections
        // get medium, and full-deflection runs at the highest speed.
        async void DoMouseMove(int x, int y)
        {
            y = -y;
            int absX = Math.Abs(x);
            int divisor = absX < 150 ? 60
                        : absX < 400 ? 40
                        : 20;

            for (int i = 0; i < 15; i++)
            {
                ComInterop.GetCursorPos(out ComInterop.POINT currPos);
                ComInterop.SetCursorPos(currPos.X + x / divisor, currPos.Y + y / divisor);
                await Task.Delay(1);
            }
        }

        public static void DoMouseClick()
            => SendMouseEventAtCursor(ComInterop.MOUSEEVENTF_LEFTDOWN | ComInterop.MOUSEEVENTF_LEFTUP);

        public static void DoMouseRightClick()
            => SendMouseEventAtCursor(ComInterop.MOUSEEVENTF_RIGHTDOWN | ComInterop.MOUSEEVENTF_RIGHTUP);

        private static void SendMouseEventAtCursor(uint flags)
        {
            ComInterop.GetCursorPos(out ComInterop.POINT pos);
            ComInterop.mouse_event(flags, (uint)pos.X, (uint)pos.Y, 0, 0);
        }

        /// <summary>
        /// Spins up an STA thread that runs <paramref name="action"/> and then
        /// pumps a WPF Dispatcher (so the action can own modal windows like
        /// PlayerWindow from outside the main UI thread). Use EndFeature to
        /// shut it down cleanly.
        /// </summary>
        internal static void StaThreadWrapper(Action action)
        {
            ManualResetEventSlim ready = new ManualResetEventSlim(false);
            dispatcherThread = new Thread(() =>
            {
                // Capture the Dispatcher for *this* thread so EndFeature can
                // call InvokeShutdown from elsewhere to break the pump cleanly.
                featureDispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();
                action();
                Dispatcher.Run();
            });
            dispatcherThread.SetApartmentState(ApartmentState.STA);
            dispatcherThread.IsBackground = true;
            dispatcherThread.Start();
            ready.Wait();
        }

        /// <summary>
        /// Stops the feature thread started by StaThreadWrapper. Uses
        /// Dispatcher.InvokeShutdown - the .NET-6+ replacement for the
        /// Thread.Abort pattern this code used to use (which throws
        /// PlatformNotSupportedException at runtime).
        /// </summary>
        internal static void EndFeature()
        {
            if (dispatcherThread == null) return;
            featureDispatcher?.InvokeShutdown();
            dispatcherThread.Join();
            dispatcherThread = null;
            featureDispatcher = null;
        }

        private void StartTimer()
        {
            if (pollingTimer == null)
            {
                pollingTimer = new System.Timers.Timer(6000); // esp timeout is 5s
                pollingTimer.Elapsed += PollingTimer_Tick;
                pollingTimer.AutoReset = false;
            }
            pollingTimer.Enabled = true;
            pollingTimer.Start();
        }

        private void StopTimer()
        {
            if (pollingTimer == null) return;
            pollingTimer.Enabled = false;
            pollingTimer.Stop();
        }

        // ESP8266 protocol messages we shouldn't try to parse as a joystick
        // reading: "ok" / "ka" (keepalives) / "initack" (handshake response).
        private static bool IsControlMessage(string buffer)
            => buffer.Contains("ok") || buffer.Contains("ka") || buffer.Contains("initack");

        private void PollingTimer_Tick(Object source, System.Timers.ElapsedEventArgs e)
        {
            DebugLog("Polling timer stopped");
            pollingTimer.Enabled = false;
            pollingTimer.Stop();
            StopThread();
            StartThread();
        }

        public void DebugLog(string message)
        {
            System.Diagnostics.Debug.WriteLine("{0}: {1}", DateTime.Now.ToString("HH:mm:ss.fff"), message);
        }
    }
}
