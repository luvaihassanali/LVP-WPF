using LVP_WPF.Services;
using LVP_WPF.Windows;
using Serilog;
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

        // Exposed so GlobalKeyDown in App.xaml.cs can route keyboard input
        // through the same OnCommand pipeline the IR remote uses - same
        // debounce, same dispatch, same logging. Read-only; ownership stays
        // with this class.
        internal IrSerialReader IrReader => serialReader;

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
            Log.Information("StaThreadWrapper: launching feature thread");
            ManualResetEventSlim ready = new ManualResetEventSlim(false);
            dispatcherThread = new Thread(() =>
            {
                try
                {
                    // Capture the Dispatcher for *this* thread so EndFeature can
                    // call InvokeShutdown from elsewhere to break the pump cleanly.
                    featureDispatcher = Dispatcher.CurrentDispatcher;
                    ready.Set();
                    action();

                    // Common race: EndFeature is invoked from CloseCurrWindow ->
                    // ClosePlayerWindow WHILE action() is still inside its
                    // PlayerWindow.ShowDialog modal frame. InvokeShutdown exits
                    // that frame AND marks the dispatcher Shutdown=true. action()
                    // then returns normally a few ms later (any cleanup after
                    // ShowDialog finishes). Calling Dispatcher.Run() on a
                    // shut-down dispatcher throws InvalidOperationException
                    // ("Cannot perform requested operation because the
                    // Dispatcher shut down") - harmless because it's caught
                    // below, but it clutters the log with a noisy stacktrace
                    // that looks like a real bug.
                    //
                    // HasShutdownStarted covers both "shutdown queued, not
                    // yet finished" and "shutdown finished" states - in
                    // either case there's nothing left to pump and Run()
                    // would just throw.
                    if (featureDispatcher == null || featureDispatcher.HasShutdownStarted)
                    {
                        Log.Information("StaThreadWrapper: action returned, dispatcher already shutting down - skipping Dispatcher.Run");
                    }
                    else
                    {
                        Log.Information("StaThreadWrapper: action returned, entering Dispatcher.Run");
                        Dispatcher.Run();
                        Log.Information("StaThreadWrapper: Dispatcher.Run returned, thread exiting");
                    }
                }
                catch (Exception ex)
                {
                    // Without this catch, an exception inside the action()
                    // (e.g., PlayerWindow construction failure during
                    // PlayRandomCartoons) crashes the STA thread silently
                    // - the process keeps running but the feature never
                    // resumes and there's no trace of why.
                    Log.Error(ex, "StaThreadWrapper: feature thread crashed");
                }
            });
            dispatcherThread.SetApartmentState(ApartmentState.STA);
            dispatcherThread.IsBackground = true;
            dispatcherThread.Start();
            ready.Wait();
        }

        /// <summary>
        /// Stops the feature thread started by StaThreadWrapper. Closes any
        /// windows owned by the feature thread (so Window.Closing fires and
        /// mediaPlayer / inactivityTimer get disposed), then shuts down the
        /// feature dispatcher and joins the thread.
        /// </summary>
        internal static void EndFeature()
        {
            if (dispatcherThread == null)
            {
                Log.Debug("EndFeature: no feature thread running, no-op");
                return;
            }
            Log.Information("EndFeature: closing feature windows and shutting down feature dispatcher");

            if (featureDispatcher != null && !featureDispatcher.HasShutdownStarted)
            {
                // CLOSE windows BEFORE InvokeShutdown. The original code
                // jumped straight to InvokeShutdown, which exits ShowDialog's
                // modal frame WITHOUT firing Window.Closing. Result: the
                // PlayerWindow disappeared visually, but mediaPlayer.Dispose()
                // never ran - the LibVLC audio engine kept playing in the
                // background, and the only fix was to RDP in and kill the
                // process. Closing the window first triggers the normal
                // shutdown chain (Closing handler -> mediaPlayer.Stop() /
                // Dispose() / inactivityTimer.Dispose() / saved-progress
                // write) and exits ShowDialog cleanly.
                try
                {
                    // Snapshot windows owned by the feature dispatcher.
                    // Application.Windows is thread-affine to the main
                    // dispatcher so enumerate from there.
                    System.Collections.Generic.List<Window> owned =
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var list = new System.Collections.Generic.List<Window>();
                            foreach (Window w in Application.Current.Windows)
                            {
                                if (w.Dispatcher == featureDispatcher)
                                {
                                    list.Add(w);
                                }
                            }
                            return list;
                        });

                    foreach (Window w in owned)
                    {
                        string typeName = w.GetType().Name;
                        try
                        {
                            // Synchronous Invoke so we know the Closing
                            // handler chain (incl. mediaPlayer.Dispose) has
                            // finished before we call InvokeShutdown. An
                            // async path would race the shutdown against the
                            // dispose, which is exactly the bug we're fixing.
                            featureDispatcher.Invoke(() =>
                            {
                                Log.Information("EndFeature: closing feature window '{Type}'", typeName);
                                w.Close();
                            });
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "EndFeature: closing feature window '{Type}' failed", typeName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "EndFeature: enumerating feature windows failed");
                }

                // After windows close, action() returns and StaThreadWrapper
                // either skips Dispatcher.Run (HasShutdownStarted check) or
                // is blocked inside it. InvokeShutdown breaks the latter case.
                featureDispatcher.InvokeShutdown();
            }

            dispatcherThread.Join();
            dispatcherThread = null;
            featureDispatcher = null;
            Log.Information("EndFeature: feature thread joined and cleared");
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
