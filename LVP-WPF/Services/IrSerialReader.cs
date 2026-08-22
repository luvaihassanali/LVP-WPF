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
    ///
    /// Threading: SerialPort.DataReceived fires on a worker thread owned by
    /// the System.IO.Ports internals. Player and LayoutPoint methods touch
    /// WPF UI elements (timeline slider, button visuals, DispatcherTimer
    /// state); those calls are marshalled to the player's Dispatcher inside
    /// this class instead of relying on each callee to remember to dispatch.
    /// That keeps the threading model explicit at the boundary where worker
    /// threads cross into UI code.
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
            Log.Information("IrSerialReader ctor: Enabled={Enabled} SerialPort=COM{Port}", Enabled, AppConfig.SerialPort);
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

            if (!Enabled)
            {
                Log.Information("IrSerialReader.Initialize: disabled in config, skipping port open");
                return;
            }

            if (!TryOpenPort())
            {
                Log.Warning("No device connected to serial port (initial open failed; will retry on tick)");
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
                Log.Information("Serial port connected (COM{Port}, {Retries} retries used)",
                    AppConfig.SerialPort, OpenRetryBudget - _retriesLeft);
                return true;
            }
            catch (Exception ex)
            {
                _retriesLeft--;
                if (_retriesLeft < 0)
                {
                    Enabled = false;
                    Log.Warning("Serial port retry budget exhausted ({Budget} attempts), giving up: {Msg}",
                        OpenRetryBudget, ex.Message);
                }
                return false;
            }
        }

        // OnDataReceived runs on a worker thread owned by System.IO.Ports.
        // Wrap the whole handler in try/catch: an unhandled exception here
        // is invisible (no UI, no thread name) and silently kills the IR
        // dispatch loop until process restart. The catch logs the exception
        // with the originating command so the failure mode is at least
        // diagnosable from the log file.
        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (e.EventType != SerialData.Chars) return;

            string msg = "";
            try
            {
                SerialPort port = (SerialPort)sender;
                msg = port.ReadLine().Replace("\r", "");
                OnCommand(msg, source: "IR");
            }
            catch (Exception ex)
            {
                // Catch-all so a single broken message can't kill the serial
                // thread. Common offenders: PlayerWindow null-deref races on
                // window-close, Dispatcher.Invoke on a torn-down window,
                // partial-line reads from the IR receiver.
                Log.Error(ex, "IR handler crashed on '{Cmd}'", msg);
            }
        }

        /// <summary>
        /// Entry point shared by the IR serial port (real-hardware path) and
        /// the keyboard global handler in App.xaml.cs (dev/debug path).
        /// Performs: command logging, debounce, optional cursor hide,
        /// then dispatch through the same switch as the IR remote uses.
        ///
        /// Returns true if the command was dispatched, false if it was
        /// dropped (debounce hit or empty message).
        /// </summary>
        /// <param name="msg">The remote-style command string (up/down/left/right/enter/return/play/pause/...)</param>
        /// <param name="source">Free-form tag for the log line ("IR", "kbd", "test", etc.) so the file log shows where each command came from.</param>
        internal bool OnCommand(string msg, string source)
        {
            if (string.IsNullOrEmpty(msg)) return false;

            Log.Information("{Source} rx: '{Cmd}' (player={Player}, season={Season}, tv={Tv}, movie={Movie}, main={Main})",
                source, msg,
                TcpSerialListener.layoutPoint?.playerWindowActive,
                TcpSerialListener.layoutPoint?.seasonWindowActive,
                TcpSerialListener.layoutPoint?.tvShowWindowActive,
                TcpSerialListener.layoutPoint?.movieWindowActive,
                TcpSerialListener.layoutPoint?.mainWindowActive);

            // Drop duplicate "action" commands inside the debounce window
            // (see field comment above). Arrow keys fall through unchanged
            // so the user can still hold them to scroll.
            if (DebouncedCommands.Contains(msg))
            {
                int now = Environment.TickCount;
                if (msg == _lastActionCmd && (now - _lastActionTick) < ActionDebounceMs)
                {
                    Log.Debug("{Source} debounce: dropped duplicate '{Cmd}' ({Ms}ms since last)",
                        source, msg, now - _lastActionTick);
                    return false;
                }
                _lastActionCmd = msg;
                _lastActionTick = now;
            }

            if (CursorConfig.HideCursor)
            {
                Application.Current.Dispatcher.Invoke(new Action(() => { Mouse.OverrideCursor = Cursors.None; }));
            }

            DispatchCommand(msg);
            return true;
        }

        // Splits the case-switch out of OnDataReceived so the try/catch
        // there can wrap a single named call. Player-side commands are
        // marshalled to the player's Dispatcher in one place at the top
        // of each transport case - the player methods themselves used to
        // have partial dispatcher coverage that missed pollingTimer
        // Start/Stop and the LayoutPoint cursor warps.
        private void DispatchCommand(string msg)
        {
            LayoutPoint layoutPoint = TcpSerialListener.layoutPoint;
            switch (msg)
            {
                case "left":
                    Log.Debug("IR -> Move(left)");
                    layoutPoint.Move(layoutPoint.left);
                    break;
                case "right":
                    Log.Debug("IR -> Move(right)");
                    layoutPoint.Move(layoutPoint.right);
                    break;
                case "up":
                    Log.Debug("IR -> Move(up)");
                    layoutPoint.Move(layoutPoint.up);
                    break;
                case "down":
                    Log.Debug("IR -> Move(down)");
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
                    Log.Debug("IR -> enter (mainOrPlayer={MainOrPlayer})",
                        layoutPoint.mainWindowActive || layoutPoint.playerWindowActive);
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
                    Log.Debug("IR -> CloseCurrWindow");
                    layoutPoint.CloseCurrWindow();
                    break;
                // Transport commands wake the overlay AND warp the joystick
                // cursor onto the corresponding button so the user gets visual
                // feedback ("you just hit fast-forward -> look at the FF button
                // glow"). All wrapped in InvokeOnPlayer so the UI mutations
                // (DispatcherTimer.Start/Stop inside TogglePlayPause,
                // PointToScreen inside FocusPlayerControl) execute on the
                // player's UI thread rather than the serial-port thread that
                // OnDataReceived fires from.
                case "play":
                case "pause":
                case "stop":
                    InvokeOnPlayer("play/pause/stop", pw =>
                    {
                        pw.TogglePlayPause();
                        pw.WakeOverlay();
                        layoutPoint.FocusPlayerControl(LayoutPoint.PlayerButtonPlay);
                    });
                    break;
                case "fastforward":
                    InvokeOnPlayer("fastforward", pw =>
                    {
                        pw.SeekRelative(false);
                        pw.WakeOverlay();
                        layoutPoint.FocusPlayerControl(LayoutPoint.PlayerButtonFastForward);
                    });
                    break;
                case "rewind":
                    InvokeOnPlayer("rewind", pw =>
                    {
                        pw.SeekRelative(true);
                        pw.WakeOverlay();
                        layoutPoint.FocusPlayerControl(LayoutPoint.PlayerButtonRewind);
                    });
                    break;
                case "forward":
                    InvokeOnPlayer("forward", pw =>
                    {
                        pw.JumpToEdge(false);
                        pw.WakeOverlay();
                        layoutPoint.FocusPlayerControl(LayoutPoint.PlayerButtonForward);
                    });
                    break;
                case "backward":
                    InvokeOnPlayer("backward", pw =>
                    {
                        pw.JumpToEdge(true);
                        pw.WakeOverlay();
                        layoutPoint.FocusPlayerControl(LayoutPoint.PlayerButtonBackward);
                    });
                    break;
                case "cartoons":
                    // Guard against stacking sessions: pressing 'cartoons'
                    // while a player is already open would launch a SECOND
                    // StaThreadWrapper -> second feature thread -> second
                    // PlayerWindow -> second mediaPlayer. Only the most
                    // recent one gets tracked by MainWindow.gui.playerWindow,
                    // so on exit only one closes and the orphaned decoder
                    // keeps playing audio. That was the actual root cause
                    // of "audio persists after cartoon exit" - user pressed
                    // 'cartoons' twice (e.g., first press seemed to lag)
                    // and got two overlapping shuffles.
                    if (layoutPoint.playerWindowActive)
                    {
                        Log.Warning("IR -> cartoons IGNORED: player is already open (playerWindowActive=true)");
                        break;
                    }
                    // FocusButton is decoration only (cursor warp for the
                    // hover highlight); the actual marathon launch is a
                    // direct StaThreadWrapper call. This is the same code
                    // path the MainWindow's ShuffleButton_Click uses when
                    // the user clicks with a mouse - single source of truth
                    // for what "cartoons" does, without depending on any
                    // WPF programmatic-click path that turned out to be
                    // unreliable from the serial-worker thread.
                    Log.Information("IR -> PlayRandomCartoons");
                    if (_gui?.shuffleButton != null)
                    {
                        layoutPoint.FocusButton(_gui.shuffleButton);
                    }
                    TcpSerialListener.StaThreadWrapper(() => TvShowWindow.PlayRandomCartoons());
                    break;
                case "history-play":
                    if (layoutPoint.playerWindowActive)
                    {
                        Log.Warning("IR -> history-play IGNORED: player is already open (playerWindowActive=true)");
                        break;
                    }
                    Log.Information("IR -> PlayHistoryList");
                    if (_gui?.historyButton != null)
                    {
                        layoutPoint.FocusButton(_gui.historyButton);
                    }
                    TcpSerialListener.StaThreadWrapper(() => TvShowWindow.PlayHistoryList());
                    break;
                default:
                    Log.Warning("IR unknown command: '{Cmd}'", msg);
                    break;
            }
        }

        // Marshal a player-side action onto the player's Dispatcher (the UI
        // thread that owns the PlayerWindow's controls). Logs entry + exit so
        // it's clear in the file log when the action ran, how long it took,
        // and whether it threw. Skips entirely when the player isn't open -
        // common during fast IR-button-mashing across window transitions.
        private void InvokeOnPlayer(string actionName, Action<PlayerWindow> body)
        {
            PlayerWindow pw = _gui.playerWindow;
            if (pw == null)
            {
                Log.Warning("IR -> {Action}: player window is null, dropping", actionName);
                return;
            }
            try
            {
                int t0 = Environment.TickCount;
                pw.Dispatcher.Invoke(() => body(pw));
                Log.Debug("IR -> {Action}: completed in {Ms}ms", actionName, Environment.TickCount - t0);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "IR -> {Action}: dispatcher invoke threw", actionName);
            }
        }
    }
}
