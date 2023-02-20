﻿using LVP_WPF.Windows;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
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
    public class TcpSerialListener
    {
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("User32.dll")]
        public static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern int ShowCursor(bool show);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const int MOUSEEVENTF_RIGHTUP = 0x10;
        private const int MOUSEEVENTF_WHEEL = 0x0800;

        private bool connectionEstablished = false;
        private string esp8266ServerIp = ConfigurationManager.AppSettings["Esp8266Ip"];
        private int esp8266ServerPort = 3000;
        private bool esp8266Enabled = bool.Parse(ConfigurationManager.AppSettings["Esp8226Enabled"]);
        private bool workerThreadRunning = false;
        private int joystickX;
        private int joystickY;
        private GuiModel gui;
        public LayoutPoint layoutPoint;
        static System.Timers.Timer pollingTimer = null;
        private SerialPort serialPort;
        private bool serialPortEnabled = bool.Parse(ConfigurationManager.AppSettings["SerialPortEnabled"]);
        private TcpClient tcpClient;
        private Thread workerThread = null;

        public TcpSerialListener(GuiModel g)
        {
            gui = g;
            layoutPoint = new LayoutPoint(g);
            if (GuiModel.hideCursor) Application.Current.Dispatcher.Invoke(new Action(() => { Mouse.OverrideCursor = Cursors.None; }));
        }

        public void StartThread()
        {
            if (serialPortEnabled) InitializeSerialPort();
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
            while (workerThreadRunning && (esp8266Enabled || serialPortEnabled))
            {
                PollConnections();
            }
        }

        private void PollConnections()
        {
            if (esp8266Enabled) DebugLog("Pinging server...");
            connectionEstablished = false;

            Ping pingSender = new Ping();
            PingOptions options = new PingOptions();
            options.DontFragment = true;
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
                        DebugLog("Destination host unreachable");
                    }
                }

                CheckSerialConnection();
                try { Task.Delay(500).Wait(); }
                catch { }
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
                success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                
                while (!success)
                {
                    DebugLog("Cannot connect to server. Trying again");
                    return;
                }

                byte[] data = Encoding.ASCII.GetBytes("zzzz");
                NetworkStream stream = null;
                try
                {
                    stream = tcpClient.GetStream();
                    DebugLog("Connected.");
                }
                catch (Exception ex)
                {
                    DebugLog("Server not ready. Trying again (" + ex.Message + ")");
                    return;
                }

                stream.Write(data, 0, data.Length);
                DebugLog("Sent init");
                StartTimer();

                while (true)
                {
                    int i;
                    byte[] bytes = new byte[256];
                    string buffer = null;

                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0 && workerThreadRunning)
                    {
                        buffer = Encoding.ASCII.GetString(bytes, 0, i);
                        DebugLog("Received: " + buffer.Replace("\r\n", ""));

                        if (buffer.Contains("initack"))
                        {
                            DebugLog("initack received");
                            TcpSerialListener.SetCursorPos(GuiModel.hideCursorX, GuiModel.hideCursorY);
                            if (GuiModel.hideCursor) Application.Current.Dispatcher.Invoke(new Action(() => { Mouse.OverrideCursor = Cursors.Arrow; }));
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

                        if (!buffer.Contains("ok") && !buffer.Contains("ka") && !buffer.Contains("initack"))
                        {
                            ParseTcpDataIn(buffer);
                        }
                    }

                    DebugLog("Stream end. Press any key");
                    stream.Close();
                    tcpClient.EndConnect(result);
                    tcpClient.Close();
                }
            }
            catch (Exception e)
            {
                DebugLog("MouseWorker_ConnectToServerException: " + e.Message);
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

        private void ParseTcpDataIn(string data)
        {
            if (GuiModel.hideCursor) Application.Current.Dispatcher.Invoke(new Action(() => { Mouse.OverrideCursor = Cursors.Arrow; }));
            
            string[] dataSplit = data.Split(',');
            if (dataSplit.Length > 6)
            {
                DebugLog("Error. Message incorrect format: " + data);
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
                joystickY = joystickY * 4;
                mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)joystickY, 0);
            }
            else
            {
                DoMouseMove();
            }
        }

        async void DoMouseMove()
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
                POINT currPos;
                GetCursorPos(out currPos);
                uint X = (uint)currPos.X;
                uint Y = (uint)currPos.Y;
                SetCursorPos((int)currPos.X + joystickX / divisor, (int)currPos.Y + joystickY / divisor);
                await Task.Delay(1);
            }

        }

        static public void DoMouseClick()
        {
            POINT currPos;
            GetCursorPos(out currPos);
            uint X = (uint)currPos.X;
            uint Y = (uint)currPos.Y;
            mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, X, Y, 0, 0);
        }

        static public void DoMouseRightClick()
        {
            POINT currPos;
            GetCursorPos(out currPos);
            uint X = (uint)currPos.X;
            uint Y = (uint)currPos.Y;
            mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, X, Y, 0, 0);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator Point(POINT point)
            {
                return new Point(point.X, point.Y);
            }
        }

        public void InitializeSerialPort()
        {
            string portNumber = ConfigurationManager.AppSettings["SerialPort"];
            serialPort = new SerialPort();
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
                    GuiModel.Log("Connected");
                }
                catch
                {
                    GuiModel.Log("No device connected");
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
                GuiModel.Log(msg);
                if (GuiModel.hideCursor) Application.Current.Dispatcher.Invoke(new Action(() => { Mouse.OverrideCursor = Cursors.None; }));
                switch (msg)
                {
                    case "left":
                        layoutPoint.Move(layoutPoint.left);
                        break;
                    case "right":
                        layoutPoint.Move(layoutPoint.right);
                        break;
                    case "up":
                        layoutPoint.Move(layoutPoint.up);
                        break;
                    case "down":
                        layoutPoint.Move(layoutPoint.down);
                        break;
                    case "enter":
                        if (layoutPoint.playerWindowActive)
                        {
                            gui.playerWindow.TcpSerialListener_PlayPause();
                        }
                        else if (layoutPoint.mainWindowActive)
                        {
                            DoMouseClick();
                        }
                        else
                        {
                            DoMouseClick();
                            layoutPoint.Select(String.Empty);
                        }
                        break;
                    case "return":
                        layoutPoint.CloseCurrWindow();
                        break;
                    case "play":
                    case "pause":
                        gui.playerWindow.TcpSerialListener_PlayPause();
                        break;
                    case "stop":
                        gui.playerWindow.TcpSerialListener_PlayPause();
                        break;
                    case "fastforward":
                        gui.playerWindow.TcpSerialListener_Seek(false);
                        break;
                    case "rewind":
                        gui.playerWindow.TcpSerialListener_Seek(true);
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
                            GuiModel.Log("Connected");
                        }
                        catch
                        {
                            GuiModel.Log("No device connected");
                        }
                    }
                }
            }
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
            pollingTimer.Enabled = false;
            pollingTimer.Stop();
        }

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
