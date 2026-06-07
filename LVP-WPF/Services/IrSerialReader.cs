using LVP_WPF.Windows;
using Serilog;
using System;
using System.Collections.Generic;
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

        // Some IR remotes emit a single button press as 2+ serial lines (held
        // button repeat code, or hardware-level bounce). For arrow keys that's
        // a feature - the user wants to scroll. For one-shot actions like
        // ENTER, transport keys, etc., it produces phantom double-clicks:
        // first "enter" opens SeasonWindow, second "enter" lands on whatever
        // the cursor was warped to (a tile) and closes it again. Track the
        // last action command + tick; ignore a repeat within the window.
        private const int ActionDebounceMs = 300;
        private static readonly HashSet<string> DebouncedCommands = new HashSet<string>
        {
            "enter", "return",
            "play", "pause", "stop",
            "fastforward", "rewind", "forward", "backward",
            "cartoons", "history-play"
        };
        private string _lastActionCmd = "";
        private int _lastActionTick = 0;

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

            if (!TryOpenPort())
            {
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
            TryOpenPort();
        }

        // Attempt to open _serialPort. Returns true on success; on failure
        // ticks down the retry budget and disables further attempts if
        // exhausted. Initialize logs a warning on first failure; CheckConnection
        // stays silent because it can fire repeatedly while the device is
        // unplugged.
        private bool TryOpenPort()
        {
            try
            {
                _serialPort.Open();
                Log.Information("Serial port connected");
                return true;
            }
            catch
            {
                _retriesLeft--;
                if (_retriesLeft < 0) Enabled = false;
                return false;
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (e.EventType != SerialData.Chars) return;

            SerialPort port = (SerialPort)sender;
            string msg = port.ReadLine().Replace("\r", "");
            Log.Information(msg);

            // Drop duplicate "action" commands inside the debounce window
            // (see field comment above). Arrow keys fall through unchanged
            // so the user can still hold them to scroll.
            if (DebouncedCommands.Contains(msg))
            {
                int now = Environment.TickCount;
                if (msg == _lastActionCmd && (now - _lastActionTick) < ActionDebounceMs)
                {
                    Log.Debug("IR debounce: dropped duplicate '{Cmd}' ({Ms}ms since last)",
                        msg, now - _lastActionTick);
                    return;
                }
                _lastActionCmd = msg;
                _lastActionTick = now;
            }

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
                    // ENTER activates whichever control the cursor is currently
                    // over - same model as the main / tv-show / season windows
                    // and as the joystick's physical click button. The player
                    // previously special-cased this to call TogglePlayPause
                    // unconditionally, but that broke joystick nav onto the
                    // new seek buttons (the click never reached them). The IR
                    // remote's dedicated "play"/"pause"/"stop" keys below
                    // still toggle play/pause without needing cursor position.
                    if (layoutPoint.mainWindowActive || layoutPoint.playerWindowActive)
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
                // All transport commands wake the overlay AND warp the joystick
                // cursor onto the corresponding button so the user gets visual
                // feedback ("you just hit fast-forward -> look at the FF button
                // glow"). WakeOverlay runs after the action so it overrides
                // TogglePlayPause's play-branch hide. FocusPlayerControl warps
                // the cursor and updates LayoutPoint's currPoint so a subsequent
                // arrow press steps from this button, not from somewhere stale.
                case "play":
                case "pause":
                case "stop":
                    _gui.playerWindow.TogglePlayPause();
                    _gui.playerWindow.WakeOverlay();
                    layoutPoint.FocusPlayerControl(LayoutPoint.PlayerButtonPlay);
                    break;
                case "fastforward":
                    _gui.playerWindow.SeekRelative(false);
                    _gui.playerWindow.WakeOverlay();
                    layoutPoint.FocusPlayerControl(LayoutPoint.PlayerButtonFastForward);
                    break;
                case "rewind":
                    _gui.playerWindow.SeekRelative(true);
                    _gui.playerWindow.WakeOverlay();
                    layoutPoint.FocusPlayerControl(LayoutPoint.PlayerButtonRewind);
                    break;
                case "forward":
                    _gui.playerWindow.JumpToEdge(false);
                    _gui.playerWindow.WakeOverlay();
                    layoutPoint.FocusPlayerControl(LayoutPoint.PlayerButtonForward);
                    break;
                case "backward":
                    _gui.playerWindow.JumpToEdge(true);
                    _gui.playerWindow.WakeOverlay();
                    layoutPoint.FocusPlayerControl(LayoutPoint.PlayerButtonBackward);
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
