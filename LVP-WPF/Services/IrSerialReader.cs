using LVP_WPF.Windows;
using Serilog;
using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Input;

namespace LVP_WPF.Services
{
    /// <summary>
    /// Listens for IR-remote commands arriving as text lines on a serial port.
    /// Each line is a single command word (up/down/left/right/enter/play/...);
    /// the handler dispatches into LayoutPoint (for navigation) or PlayerWindow
    /// (for playback control).
    ///
    /// Was previously the bottom half of TcpSerialListener; pulled out so the
    /// TCP/joystick code in TcpSerialListener stays focused on that protocol.
    /// </summary>
    internal sealed class IrSerialReader
    {
        private const int OpenRetryBudget = 20;

        private readonly GuiModel _gui;
        private SerialPort _serialPort;
        private int _retriesLeft = OpenRetryBudget;

        public bool Enabled { get; private set; }

        public IrSerialReader(GuiModel gui)
        {
            _gui = gui;
            Enabled = AppConfig.SerialPortEnabled;
        }

        /// <summary>
        /// Opens the configured COM port and subscribes to DataReceived.
        /// If the port can't be opened, the retry budget ticks down; once
        /// exhausted, Enabled flips false and CheckConnection becomes a no-op.
        /// </summary>
        public void Initialize()
        {
            _serialPort = new SerialPort
            {
                PortName = $"COM{AppConfig.SerialPort}",
                BaudRate = 9600,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Handshake = Handshake.None
            };
            _serialPort.DataReceived += OnDataReceived;

            if (!Enabled) return;

            try
            {
                _serialPort.Open();
                Log.Information("Serial port connected");
            }
            catch
            {
                _retriesLeft--;
                if (_retriesLeft < 0)
                {
                    Enabled = false;
                }
                Log.Warning("No device connected to serial port");
            }
        }

        /// <summary>
        /// Re-opens the port if a previous Initialize/CheckConnection failed
        /// and the user has since plugged the IR receiver back in. No-op if
        /// already open, disabled, or the retry budget is exhausted.
        /// </summary>
        public void CheckConnection()
        {
            if (!Enabled || _serialPort == null || _serialPort.IsOpen) return;

            try
            {
                _serialPort.Open();
                Log.Information("Serial port connected");
            }
            catch
            {
                _retriesLeft--;
                if (_retriesLeft < 0)
                {
                    Enabled = false;
                }
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (e.EventType != SerialData.Chars) return;

            SerialPort port = (SerialPort)sender;
            string msg = port.ReadLine().Replace("\r", "");
            Log.Information(msg);

            if (CursorConfig.HideCursor)
            {
                Application.Current.Dispatcher.Invoke(new Action(() => { Mouse.OverrideCursor = Cursors.None; }));
            }

            LayoutPoint layoutPoint = TcpSerialListener.layoutPoint;
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
                        _gui.playerWindow.TogglePlayPause();
                    }
                    else if (layoutPoint.mainWindowActive)
                    {
                        TcpSerialListener.DoMouseClick();
                    }
                    else
                    {
                        TcpSerialListener.DoMouseClick();
                        if (!layoutPoint.seasonWindowActive)
                        {
                            layoutPoint.Select(String.Empty);
                        }
                    }
                    break;
                case "return":
                    layoutPoint.CloseCurrWindow();
                    break;
                case "play":
                case "pause":
                case "stop":
                    _gui.playerWindow.TogglePlayPause();
                    break;
                case "fastforward":
                    _gui.playerWindow.SeekRelative(false);
                    break;
                case "rewind":
                    _gui.playerWindow.SeekRelative(true);
                    break;
                case "forward":
                    _gui.playerWindow.JumpToEdge(false);
                    break;
                case "backward":
                    _gui.playerWindow.JumpToEdge(true);
                    break;
                case "cartoons":
                    TcpSerialListener.StaThreadWrapper(() =>
                    {
                        TvShowWindow.PlayRandomCartoons();
                    });
                    break;
                case "history-play":
                    TcpSerialListener.StaThreadWrapper(() =>
                    {
                        TvShowWindow.PlayHistoryList();
                    });
                    break;
            }
        }
    }
}
