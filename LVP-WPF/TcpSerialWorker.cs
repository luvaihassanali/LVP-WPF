using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO.Ports;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace LVP_WPF
{
    internal class TcpSerialWorker
    {
        [DllImport("User32.dll")]
        private static extern bool SetCursorPos(int X, int Y);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const int MOUSEEVENTF_RIGHTUP = 0x10;
        private const int MOUSEEVENTF_WHEEL = 0x0800;

        private string serverIp = ConfigurationManager.AppSettings["Esp8266Ip"];
        private int serverPort = 3000;
        private bool connectionEstablished = false;
        private bool workerThreadRunning = false;
        private bool hideCursor = bool.Parse(ConfigurationManager.AppSettings["Esp8226HideCursor"]);
        private int joystickX;
        private int joystickY;
        //private Layout 
        private DispatcherTimer pollingTimer;
        private SerialPort serialPort;
        private bool serialPortEnabled = bool.Parse(ConfigurationManager.AppSettings["ComPortEnabled"]);
        private TcpClient tcpClient;
        private Thread workerThread = null;

        public TcpSerialWorker()
        {
            // Cursor hide
        }

        public void InitializeSerialPort()
        {
            //this.layoutController = lc;
            serialPort = new SerialPort();
            string portNumber = ConfigurationManager.AppSettings["comPort"];
            serialPort.PortName = "COM" + portNumber;
            serialPort.BaudRate = 9600;
            serialPort.DataBits = 8;
            serialPort.Parity = Parity.None;
            serialPort.StopBits = StopBits.One;
            serialPort.Handshake = Handshake.None;
            serialPort.DataReceived += SerialPort_DataReceived;
            if (serialPortEnabled)
            {
                try
                {
                    serialPort.Open();
                    //MainForm.Log("Connected to serial port");
                }
                catch
                {
                    //MainForm.Log("No device connected to serial port");
                }
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort serialPort = (SerialPort)sender;
            if (e.EventType == SerialData.Chars)
            {
                string msg = serialPort.ReadLine();
                msg = msg.Replace("\r", "");
                //MainForm.Log("Serial port: " + msg);
                // Cursor hide
                switch (msg)
                {
                    case "power":
                        // Send cursor to centre of screen
                        SetCursorPos(960, 540);
                        DoMouseClick();
                        break;
                    case "stop":
                    case "pause":
                    case "play":
                        try
                        {
                            //PlayerForm pf = (PlayerForm)GetForm("PlayerForm");
                            //pf.InitiatePause();
                        }
                        catch (Exception ex)
                        {
                            //MainForm.Log("Serial port case 2: " + ex.ToString());
                        }
                        break;
                    case "up":
                        //layoutController.MovePointPosition(layoutController.up);
                        break;
                    case "down":
                        //layoutController.MovePointPosition(layoutController.down);
                        break;
                    case "right":
                        //layoutController.MovePointPosition(layoutController.right);
                        break;
                    case "left":
                        //layoutController.MovePointPosition(layoutController.left);
                        break;
                    case "enter":
                        //if (layoutController.onMainForm)
                        {
                            DoMouseClick();
                        }
                        //else
                        {
                            DoMouseClick();
                            //layoutController.Select(String.Empty);
                        }
                        break;
                    case "back":
                        try
                        {
                            //layoutController.CloseCurrentForm();
                        }
                        catch (Exception ex)
                        {
                            //MainForm.Log("Serial port back recv (layoutController.CloseCurrentForm()): " + ex.ToString());
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        private void CheckSerialConnection()
        {
            if (serialPortEnabled)
            {
                if (serialPort != null)
                {
                    if (!serialPort.IsOpen)
                    {
                        try
                        {
                            serialPort.Open();
                            //MainForm.Log("Reconnected to serial port");
                        }
                        catch
                        {
                            //Log("Serial port disconnected");
                        }
                    }
                }
            }
        }

        private void DoWork()
        {
            pollingTimer = new DispatcherTimer();
            pollingTimer.Interval = TimeSpan.FromSeconds(6);
            pollingTimer.Tick += PollingTimer_Tick;

            while (workerThreadRunning)
            {
                CheckForServer();
                ConnectToServer();
            }
        }

        private void StartTimer()
        {
            pollingTimer.IsEnabled = true;
            pollingTimer.Start();
        }

        private void StopTimer()
        {
            pollingTimer.IsEnabled = false;
            pollingTimer.Stop();
        }

        private void PollingTimer_Tick(object? sender, EventArgs e)
        {
            //Log("Polling timer stopped");
            pollingTimer.IsEnabled = false;
            pollingTimer.Stop();

            StopImmediately();
            Start();
        }

        private void ConnectToServer()
        {
            //Log("Initializing TCP connection");
            try
            {
                tcpClient = new TcpClient();
                bool success = false;
                IAsyncResult result = null;

                result = tcpClient.BeginConnect(serverIp, serverPort, null, null);
                success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));

                while (!success)
                {
                    //Log("Cannot connect to server. Trying again");
                    return;
                }

                Byte[] data = System.Text.Encoding.ASCII.GetBytes("zzzz");
                NetworkStream stream = null;

                try
                {
                    stream = tcpClient.GetStream();
                    //Log("Connected.");
                    Thread.Sleep(1000);
                }
                catch (System.InvalidOperationException)
                {
                    //Log("Server not ready. Trying again");
                    return;
                }

                stream.Write(data, 0, data.Length);
                //Log("Sent init");
                StartTimer();

                while (true)
                {
                    int i;
                    Byte[] bytes = new Byte[256];
                    String buffer = null;

                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        buffer = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                        //Log("Received: " + buffer.Replace("\r\n", ""));

                        if (buffer.Contains("initack"))
                        {
                            //Log("initack received");
                            /*if (MainForm.hideCursor)
                            {
                                mainForm.Invoke(new MethodInvoker(delegate
                                {
                                    for (int j = 0; j < MainForm.cursorCount; j++)
                                    {
                                        Cursor.Show();
                                    }
                                    MainForm.cursorCount = 0;
                                }));
                            }*/
                            StopTimer();
                            StartTimer();
                        }

                        if (buffer.Contains("ka"))
                        {
                            StopTimer();
                            //Log("Sending ack");
                            data = System.Text.Encoding.ASCII.GetBytes("ack");
                            stream = tcpClient.GetStream();
                            stream.Write(data, 0, data.Length);
                            StartTimer();
                        }

                        if (!buffer.Contains("ok") && !buffer.Contains("ka") && !buffer.Contains("initack"))
                        {
                            ParseTcpDataIn(buffer);
                        }
                    }

                    //Log("Stream end. Press any key");
                    stream.Close();
                    tcpClient.EndConnect(result);
                    tcpClient.Close();
                }
            }
            catch (Exception e)
            {
                //Log("MouseWorker_ConnectToServerException: " + e.Message);
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

        private void CheckForServer()
        {
            //Log("Pinging server...");
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
                    reply = pingSender.Send(serverIp, timeout, buffer, options);
                }
                catch
                { }

                if (reply != null && reply.Status == IPStatus.Success)
                {
                    //Log("Ping success");
                    connectionEstablished = true;
                }
                else
                {
                    //Log("Destination host unreachable");
                    CheckSerialConnection();
                }

                try
                {
                    Thread.Sleep(1000);
                }
                catch
                { }
            }
        }

        private void ParseTcpDataIn(string data)
        {
            /*if (MainForm.cursorCount != 0)
            {
                mainForm.Invoke(new MethodInvoker(delegate
                {
                    for (int j = 0; j < MainForm.cursorCount; j++)
                    {
                        Cursor.Show();
                    }
                    MainForm.cursorCount = 0;
                }));
            }*/

            string[] dataSplit = data.Split(',');
            if (dataSplit.Length > 6)
            {
                //Log("Error. Message incorrect format: " + data);
                return;
            }
            joystickX = Int32.Parse(dataSplit[0]);
            joystickY = Int32.Parse(dataSplit[1]);
            int joystickBtnState = Int32.Parse(dataSplit[2]);
            int scrollBtnState = Int32.Parse(dataSplit[4].Replace("\r\n", ""));
            int clickBtnState = Int32.Parse(dataSplit[3].Replace("\r\n", ""));

            if (scrollBtnState == 0 && clickBtnState == 0)
            {
                System.Diagnostics.Process.Start("taskmgr.exe");
            }

            if (joystickBtnState == 0 || clickBtnState == 0)
            {
                DoMouseClick();
                return;
            }

            if (scrollBtnState == 0)
            {
                joystickY = joystickY * 2;
                mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)joystickY, 0);
            }
            else
            {
                DoMouseMove();
            }
        }

        void DoMouseMove()
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
                Point currPos = Mouse.GetPosition(Application.Current.MainWindow);
                SetCursorPos((int)currPos.X + joystickX / divisor, (int)currPos.Y + joystickY / divisor);
                Thread.Sleep(1);
            }

        }

        static public void DoMouseClick()
        {
            Point currPos = Mouse.GetPosition(Application.Current.MainWindow);
            uint X = (uint)currPos.X;
            uint Y = (uint)currPos.Y;
            mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, X, Y, 0, 0);
        }

        static public void DoMouseRightClick()
        {
            Point currPos = Mouse.GetPosition(Application.Current.MainWindow);
            uint X = (uint)currPos.X;
            uint Y = (uint)currPos.Y;
            mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, X, Y, 0, 0);
        }

        public void Start()
        {
            try
            {
                if (workerThread == null)
                {
                    workerThread = new Thread(new ThreadStart(this.DoWork));
                    workerThread.IsBackground = true;
                    workerThread.Name = "LocalVideoPlayer mouse thread";
                    workerThreadRunning = true;
                    workerThread.Start();
                }
            }
            catch (Exception e)
            {
                //MainForm.Log(e.Message);
            }
        }

        public void Stop()
        {
            Stop(10000);
        }

        public void Stop(int stopTimeout)
        {
            if (serialPort != null)
            {
                serialPort.Close();
                serialPort.Dispose();
            }

            if (pollingTimer != null)
            {
                if (pollingTimer.IsEnabled) pollingTimer.Stop();
                pollingTimer.IsEnabled = false;
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
                Join(stopTimeout, workerThread);

                if (workerThread.IsAlive)
                {
                    StopImmediately();
                }
                tcpClient = null;
                workerThread = null;
            }
        }

        public void StopImmediately()
        {
            if (pollingTimer != null)
            {
                if (pollingTimer.IsEnabled) pollingTimer.Stop();
                pollingTimer.IsEnabled = false;
                pollingTimer = null;
            }

            if (tcpClient != null)
            {
                tcpClient.Close();
                tcpClient.Dispose();
            }

            if (workerThread != null)
            {
                workerThread.Abort();
                workerThread.Join();
                workerThread = null;
            }
        }

        // Check if current thread needs to sleep
        public void Join(int timeoutMs, Thread workerToWatch)
        {
            DateTime endDateTimeOut = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (DateTime.UtcNow < endDateTimeOut && workerToWatch.IsAlive)
            {
                Thread.Sleep(100);
            }
        }
    }
}
