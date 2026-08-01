using LVP_WPF.Models;
using LVP_WPF.Services;
using LVP_WPF.Util;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace LVP_WPF.Windows
{
    public enum PrimaryWindow { Main, Movie, TvShow }
    public enum WindowOverlay { None, Season, Player, LanguageDropdown }

    public class LayoutPoint
    {
        public GuiModel gui;

        // Window state is two orthogonal axes:
        //   _primary: which underlying screen is open (Main/Movie/TvShow)
        //   _overlay: optional modal on top (Season picker, Player, lang dropdown)
        // Previously expressed as 6 separate bool fields; the bool properties
        // below remain for backward compatibility with the ~50 existing read
        // sites (LayoutPoint internal + TcpSerialListener + TvShowWindow).
        private PrimaryWindow _primary = PrimaryWindow.Main;
        private WindowOverlay _overlay = WindowOverlay.None;

        // Primary setters: only `true` is meaningful. Setting `false` is a
        // no-op (the next assignment of a different primary establishes the
        // new state). Original code did `mainWindowActive = false;` right
        // before `movieWindowActive = true;`, and the no-op semantic
        // preserves that pattern without ever leaving primary undefined.
        public bool mainWindowActive
        {
            get => _primary == PrimaryWindow.Main;
            set { if (value) _primary = PrimaryWindow.Main; }
        }
        public bool movieWindowActive
        {
            get => _primary == PrimaryWindow.Movie;
            set { if (value) _primary = PrimaryWindow.Movie; }
        }
        public bool tvShowWindowActive
        {
            get => _primary == PrimaryWindow.TvShow;
            set { if (value) _primary = PrimaryWindow.TvShow; }
        }

        // Overlay setters: both directions are meaningful. Setting `true`
        // enters the overlay, setting `false` clears any overlay to None.
        public bool seasonWindowActive
        {
            get => _overlay == WindowOverlay.Season;
            set => _overlay = value ? WindowOverlay.Season : WindowOverlay.None;
        }
        public bool playerWindowActive
        {
            get => _overlay == WindowOverlay.Player;
            set => _overlay = value ? WindowOverlay.Player : WindowOverlay.None;
        }
        public bool languageDropdownActive
        {
            get => _overlay == WindowOverlay.LanguageDropdown;
            set => _overlay = value ? WindowOverlay.LanguageDropdown : WindowOverlay.None;
        }

        public bool incomingSerialMsg = false;

        /// <summary>
        /// Called by a Window's close-button click handler AFTER the window
        /// closed itself. If the close was initiated by the user clicking the
        /// button (incomingSerialMsg == false), we still need to walk
        /// LayoutPoint's state machine back one level. If instead the close
        /// originated from a serial "return" command (which already called
        /// CloseCurrWindow and set incomingSerialMsg), we just clear the flag.
        /// </summary>
        public void NotifyWindowClosedFromUI()
        {
            if (!incomingSerialMsg)
            {
                CloseCurrWindow(false);
            }
            else
            {
                incomingSerialMsg = false;
            }
        }
        public (int x, int y) currPoint = (0, 0);
        public (int x, int y) returnPointA = (0, 0);
        public (int x, int y) returnPointB = (0, 0);

        public object currControl = null;
        public List<int[]> mainWindowGrid = new List<int[]>();
        // FrameworkElement instead of Image so the grid can hold both poster
        // tiles (Image) and the new History / Shuffle header buttons (Button).
        public List<FrameworkElement[]> mainWindowControlGrid = new List<FrameworkElement[]>();
        public Image movieBackdrop = null;
        public ComboBox movieLangComboBox = null;
        public List<ComboBoxItem> langComboBoxItems = new List<ComboBoxItem>();
        public List<Point> langComboBoxItemPts = new List<Point>();

        public int movieIndex = 0;
        public int tvIndex = 0;
        public int seasonIndex = 0;
        public int langIndex = 0;
        public List<object> tvControlList = new List<object>();
        public List<Image> seasonControlList = new List<Image>();

        // Playback-control buttons inside PlayerWindow, populated by
        // PlayerWindow_Loaded. Ordered left-to-right as they appear in the
        // bottom row: backward, rewind, play, fast-forward, forward.
        // Joystick Left/Right cycles through them; Up/Down is ignored
        // because the player has only one row of nav-targets.
        public List<object> playerControlList = new List<object>();
        public int playerIndex = 0;

        // Named slots in playerControlList - used by IR remote dispatch so
        // pressing "fastforward" warps the cursor to the FF button etc.
        public const int PlayerButtonBackward    = 0;
        public const int PlayerButtonRewind      = 1;
        public const int PlayerButtonPlay        = 2;
        public const int PlayerButtonFastForward = 3;
        public const int PlayerButtonForward     = 4;

        // True while the cursor is hidden / not yet positioned on a player
        // control. EnterPlayerNav sets it true on player open; the first
        // arrow press flips it false (and reveals the cursor at play
        // without applying a delta) so subsequent arrows step from there.
        // FocusPlayerControl also flips it false because that path warps
        // the cursor to a specific button explicitly.
        private bool playerCursorParked = true;

        // Warp the cursor to a specific button in the player overlay,
        // update currPoint/currControl, and mark the cursor as no longer
        // parked. Used by the IR remote transport keys to give visual
        // feedback ("you just pressed fast-forward -> look at the FF
        // button highlighting").
        internal void FocusPlayerControl(int index)
        {
            if (index < 0 || index >= playerControlList.Count)
            {
                Log.Warning("FocusPlayerControl: index {Idx} out of range (list size {Size})",
                    index, playerControlList.Count);
                return;
            }
            Log.Debug("FocusPlayerControl: index={Idx}", index);
            currPoint = (index, 0);
            currControl = playerControlList[index];
            playerCursorParked = false;
            CenterMouseOverControl(currControl, index, scrollViewer: null);
        }

        // Warp the cursor to the player window's close button (top-right of
        // the overlay) and wake the overlay so it's visible. This is the
        // UP-key target while the player is active - acts as a "menu"
        // shortcut for navigating to the close control.
        //
        // Pre-arms navigation state to point at the PLAY button (center of
        // the seek row) so the next L/R/DOWN keypress returns predictably
        // to play, regardless of which seek button the user was on before
        // pressing UP. Without this pre-arm, LEFT-from-close stepped to
        // (prev-1) and RIGHT-from-close stepped to (prev+1) - asymmetric
        // and unintuitive. The close button itself isn't in
        // playerControlList (it's not part of the bottom seek row), so we
        // can't navigate "from" it via the index-based MoveAlong1D path;
        // the parked-state trick is the simplest way to get a clean reset.
        internal void FocusPlayerCloseButton()
        {
            Button closeBtn = gui?.playerCloseButton;
            if (closeBtn == null)
            {
                Log.Warning("FocusPlayerCloseButton: gui.playerCloseButton is null, ignoring");
                return;
            }
            Log.Debug("FocusPlayerCloseButton: warping cursor to close button");
            // Dispatch the cursor warp to the close button's owning thread
            // (the player window's dispatcher). WarpCursorToCenter calls
            // PointToScreen, which is dispatcher-affine; this method is
            // commonly called from the serial-port thread via Move().
            closeBtn.Dispatcher.Invoke(() => WarpCursorToCenter(closeBtn));
            gui.playerWindow?.WakeOverlay();

            // Pre-arm: NEXT L/R press will see playerCursorParked=true and
            // reveal the cursor at currControl (= play button) without
            // applying a delta. Effectively "L or R from close button =
            // jump to play". MovePlayerPoint then clears parked so a
            // subsequent press steps normally from play.
            if (playerControlList.Count > PlayerButtonPlay)
            {
                currPoint = (PlayerButtonPlay, 0);
                currControl = playerControlList[PlayerButtonPlay];
                playerCursorParked = true;
            }
        }

        /// <summary>
        /// Capture each item container in <paramref name="comboBox"/> into
        /// langComboBoxItems (and optionally their on-screen Points into
        /// langComboBoxItemPts) so the joystick/IR navigator can later
        /// position the cursor over individual dropdown entries.
        /// The caller is responsible for opening/closing the dropdown -
        /// item containers are only realized while it's open.
        /// </summary>
        public void CaptureComboBoxItems(ComboBox comboBox, bool capturePositions)
        {
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                ComboBoxItem item = (ComboBoxItem)comboBox.ItemContainerGenerator.ContainerFromIndex(i);
                langComboBoxItems.Add(item);
                if (capturePositions)
                {
                    langComboBoxItemPts.Add(item.PointToScreen(new Point(0d, 0d)));
                }
            }
        }
        public List<int[]> seasonWindowGrid = new List<int[]>();
        public List<Image[]> seasonWindowControlGrid = new List<Image[]>();

        public (int x, int y) up = (-1, 0);
        public (int x, int y) down = (1, 0);
        public (int x, int y) left = (0, -1);
        public (int x, int y) right = (0, 1);

        public LayoutPoint(GuiModel g)
        {
            gui = g;
            BuildMainWindowGrid();
            ComInterop.SetCursorPos(20, 20);
            TcpSerialListener.DoMouseClick();

            // Default focus: first poster tile (matches the original behavior
            // before the History / Shuffle pseudo-rows existed). Walk forward
            // skipping pseudo-rows whose [0] holds a Button. Fall back to
            // (0,0) if there are no posters at all (empty library), and to
            // mainCloseButton if the grid itself is empty.
            if (mainWindowControlGrid.Count == 0)
            {
                currControl = gui.mainCloseButton;
                CenterMouseOverControl(currControl, 0);
                return;
            }
            int initialRow = 0;
            for (int r = 0; r < mainWindowControlGrid.Count; r++)
            {
                if (mainWindowControlGrid[r][0] is Image)
                {
                    initialRow = r;
                    break;
                }
            }
            currPoint = (initialRow, 0);
            currControl = mainWindowControlGrid[initialRow][0];
            CenterMouseOverControl(currControl, initialRow);
        }

        public void Move((int x, int y) pos)
        {
            if (playerWindowActive)
            {
                // Player has a single horizontal row of seek buttons (the
                // bottom of the overlay) plus the close button at top-right.
                // Nav map:
                //   Left/Right (pos.y != 0)  -> walk the seek row
                //   Up         (pos.x < 0)   -> focus the close button
                //                               (player "menu" - the close
                //                               control at top of overlay)
                //   Down       (pos.x > 0)   -> recenter on the play button
                //                               (center of the seek row).
                //                               Symmetric with UP-to-close;
                //                               also acts as a clean
                //                               "return to default" from
                //                               wherever the user navigated.
                // pos.y carries the horizontal delta in this codebase; see
                // the `left`/`right` tuples at the top of the class.
                if (pos.y != 0)
                {
                    Log.Debug("LayoutPoint.Move: player L/R delta={Delta}", pos.y);
                    MovePlayerPoint(pos.y);
                }
                else if (pos.x < 0)
                {
                    Log.Debug("LayoutPoint.Move: player UP -> close button");
                    FocusPlayerCloseButton();
                }
                else
                {
                    Log.Debug("LayoutPoint.Move: player DOWN -> play (recenter)");
                    FocusPlayerControl(PlayerButtonPlay);
                }
                return;
            }

            if (languageDropdownActive)
            {
                MoveLangPoint(pos.x);
            }
            else if (seasonWindowActive)
            {
                MoveSeasonPoint((pos.x, pos.y));
            }
            else if (tvShowWindowActive)
            {
                MoveTvPoint(pos.x);
            }
            else if (movieWindowActive)
            {
                MoveMoviePoint(pos.x);
            }
            else if (mainWindowActive)
            {
                MovePoint((pos.x, pos.y));
            }
        }

        public void Select(string controlName, bool isMovie = false)
        {
            Log.Debug("LayoutPoint.Select: '{Control}' (isMovie={IsMovie}, mainActive={Main}, tvActive={Tv}, movieActive={Movie}, seasonActive={Season}, playerActive={Player}, langDropdown={Lang})",
                controlName, isMovie,
                mainWindowActive, tvShowWindowActive, movieWindowActive, seasonWindowActive, playerWindowActive, languageDropdownActive);

            if (PlaybackSession.IsCartoonShuffle || PlaybackSession.IsHistoryWatch)
            {
                Log.Debug("LayoutPoint.Select: in cartoon-shuffle/history mode, just stashing returnPointA");
                returnPointA = currPoint;
                return;
            }

            if (mainWindowActive)
            {
                SelectMainWindow(isMovie);
                return;
            }

            if (controlName.Equals("languageDropdown"))
            {
                SelectLangDropdown();
                return;
            }

            SelectChildWindow(controlName);
        }

        private void SelectChildWindow(string controlName)
        {
            if (seasonWindowActive)
            {
                Log.Information("LayoutPoint: leaving SeasonWindow back to TvShowWindow");
                seasonWindowActive = false;
                seasonControlList.Clear();
                seasonWindowGrid.Clear();
                seasonWindowControlGrid.Clear();
                currPoint = returnPointB;
                currControl = tvControlList[currPoint.x];
                CenterMouseOverControl(currControl);
            }
            else if (tvShowWindowActive)
            {
                returnPointB = currPoint;
                if (controlName.Equals("SeasonWindow"))
                {
                    Log.Information("LayoutPoint: entering SeasonWindow from TvShowWindow (seasonIndex={Idx})", seasonIndex);
                    seasonWindowActive = true;
                    BuildSeasonGrid();
                    currPoint = GetCurrSeasonPoint(seasonIndex);
                    currControl = seasonWindowControlGrid[currPoint.x][currPoint.y];
                    CenterMouseOverControl(currControl, currPoint.x, MainWindow.gui.seasonScrollViewer);
                }
                if (controlName.Equals("PlayerWindow"))
                {
                    Log.Information("LayoutPoint: entering PlayerWindow from TvShowWindow");
                    playerWindowActive = true;
                    EnterPlayerNav();
                }
            }
            else if (movieWindowActive)
            {
                returnPointB = currPoint;
                if (controlName.Equals("PlayerWindow"))
                {
                    Log.Information("LayoutPoint: entering PlayerWindow from MovieWindow");
                    playerWindowActive = true;
                    EnterPlayerNav();
                }
            }
        }

        private void SelectLangDropdown()
        {
            Task.Delay(200).Wait();
            if (!languageDropdownActive)
            {
                returnPointB = currPoint;
                languageDropdownActive = true;
                currPoint = (langIndex, -1);
                currControl = langComboBoxItems[currPoint.x];
                CenterMouseOverComboBoxItem(langComboBoxItemPts[currPoint.x], (ComboBoxItem)currControl);
            }
            else
            {
                langIndex = currPoint.x;
                languageDropdownActive = false;
                currPoint = returnPointB;
                if (tvShowWindowActive)
                {
                    bool toggleVisible = TcpSerialListener.layoutPoint.tvControlList[1] is ToggleButton;
                    currPoint = toggleVisible ? (2, -1) : (1, -1);
                    currControl = tvControlList[currPoint.x];
                }
                else
                {
                    currControl = movieLangComboBox;
                }
                CenterMouseOverControl(currControl);
            }
        }

        private void SelectMainWindow(bool isMovie)
        {
            mainWindowActive = false;
            returnPointA = currPoint;
            if (isMovie)
            {
                Log.Information("LayoutPoint: MainWindow -> MovieWindow");
                movieWindowActive = true;
                movieIndex = 0;
                currPoint = (movieIndex, -1);
                currControl = movieBackdrop;
                CenterMouseOverControl(currControl);
            }
            else
            {
                Log.Information("LayoutPoint: MainWindow -> TvShowWindow");
                tvShowWindowActive = true;
                currPoint = (tvIndex, -1);
                currControl = tvControlList[currPoint.x];
                CenterMouseOverControl(currControl);
            }
        }

        internal void CloseCurrWindow(bool click = true)
        {
            Log.Debug("LayoutPoint.CloseCurrWindow: click={Click}, player={Player}, tv={Tv}, movie={Movie}, main={Main}, season={Season}, lang={Lang}",
                click, playerWindowActive, tvShowWindowActive, movieWindowActive, mainWindowActive, seasonWindowActive, languageDropdownActive);
            if (seasonWindowActive || languageDropdownActive)
            {
                Log.Debug("LayoutPoint.CloseCurrWindow: ignoring (season or lang-dropdown is active)");
                return;
            }

            if (click)
            {
                incomingSerialMsg = true;
            }
            try
            {
                if (playerWindowActive)
                {
                    Log.Information("LayoutPoint.CloseCurrWindow -> ClosePlayerWindow");
                    ClosePlayerWindow(click);
                    TcpSerialListener.EndFeature();
                }
                else if (tvShowWindowActive)
                {
                    Log.Information("LayoutPoint.CloseCurrWindow -> CloseTvWindow");
                    CloseTvWindow(click);
                }
                else if (movieWindowActive)
                {
                    Log.Information("LayoutPoint.CloseCurrWindow -> CloseMovieWindow");
                    CloseMovieWindow(click);
                }
                else if (mainWindowActive)
                {
                    Log.Information("LayoutPoint.CloseCurrWindow -> CloseMainWindow (app exit path)");
                    CloseMainWindow();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "LayoutPoint.CloseCurrWindow failed (player={Player}, tv={Tv}, movie={Movie}, main={Main})",
                    playerWindowActive, tvShowWindowActive, movieWindowActive, mainWindowActive);
            }
        }

        private async void CloseMovieWindow(bool click)
        {
            movieWindowActive = false;
            mainWindowActive = true;
            await PerformCloseToMain(click);
        }

        private async void CloseTvWindow(bool click)
        {
            langIndex = 0;
            tvShowWindowActive = false;
            mainWindowActive = true;
            tvControlList.Clear();
            tvIndex = 0;

            if (click)
            {
                // Reset the episode list scroll before falling through to the
                // shared close-to-main routine so the cursor lands on the
                // close-button at the expected screen Y.
                gui.episodeScrollViewer.Dispatcher.Invoke(() => { gui.episodeScrollViewer.ScrollToHome(); });
                WpfTreeHelpers.DoEvents();
            }

            await PerformCloseToMain(click);
        }

        // Shared tail for movie/tv close handlers: optionally simulate a
        // click on the close button, then move the cursor back to whichever
        // main-grid tile the user was on before they opened the sub-window.
        private async Task PerformCloseToMain(bool click)
        {
            if (click)
            {
                CenterMouseOverControl(gui.tvMovieCloseButton);
                await Task.Delay(200);
                TcpSerialListener.DoMouseClick();
                await Task.Delay(200);
            }

            currPoint = returnPointA;
            currControl = mainWindowControlGrid[currPoint.x][currPoint.y];
            CenterMouseOverControl(currControl);
        }

        private void ClosePlayerWindow(bool click)
        {
            playerWindowActive = false;

            // Two close mechanisms depending on mode:
            //
            //   Regular TV / movie: no feature dispatcher, so EndFeature is
            //   a no-op. The mechanism here IS the close - warp the cursor
            //   onto the player's close button, pause so the user sees the
            //   button's hover highlight (visual acknowledgment of the
            //   return-button press), then synthesize a mouse click that
            //   fires CloseButton_Click -> this.Close().
            //
            //   Cartoon shuffle / history watch: EndFeature (called next in
            //   CloseCurrWindow) closes the PlayerWindow via the feature
            //   dispatcher. The click simulation MUST be skipped - by the
            //   time DoMouseClick fires 200ms later, the player is already
            //   gone and the click falls through to MainWindow's close
            //   button at the same top-right screen position, exiting the
            //   app. But we still want the visual acknowledgment, so we
            //   warp the cursor and pause without the click.
            //
            // Synchronous (Thread.Sleep instead of await Task.Delay) is
            // deliberate: async void would let CloseCurrWindow return and
            // call EndFeature immediately, destroying the window before the
            // user sees any highlight at all. Blocking here holds the caller
            // for the ~200ms it takes to render the highlight. Callers are
            // fine to block: IR dispatch runs on a serial worker thread with
            // its own debounce, and the DeferCloseCurrWindow path for
            // MediaPlayer_EndReached hops to the main UI dispatcher whose
            // brief pause isn't visible under a full-screen player.
            bool featureMode = PlaybackSession.IsCartoonShuffle || PlaybackSession.IsHistoryWatch;

            if (click)
            {
                // WakeOverlay BEFORE the cursor warp. If auto-hide fired
                // during playback, overlayGrid.Visibility is Hidden and the
                // close button is not hit-testable - warping onto it in
                // that state doesn't fire MouseEnter, so the blue-highlight
                // acknowledgment never appears. Regular TV mode happened to
                // work by accident because the visibility-change synthetic
                // MouseMove sometimes lined up on the main UI dispatcher,
                // but cartoon/history mode (player on a separate feature
                // dispatcher) hit a race that skipped the MouseEnter. Showing
                // the overlay explicitly first makes the button hit-testable
                // by the time the warp posts its WM_MOUSEMOVE, so MouseEnter
                // fires deterministically.
                gui.playerWindow?.WakeOverlay();

                CenterMouseOverControl(gui.playerCloseButton);
                WpfTreeHelpers.DoEvents();
                Thread.Sleep(200);

                if (featureMode)
                {
                    // No click simulation - see comment above. Clear the
                    // serial-message flag directly since the click's
                    // NotifyWindowClosedFromUI (which normally clears it)
                    // never fires.
                    incomingSerialMsg = false;
                }
                else
                {
                    TcpSerialListener.DoMouseClick();
                    Thread.Sleep(200);
                }
            }

            if (movieWindowActive)
            {
                currControl = movieBackdrop;
                CenterMouseOverControl(currControl);
            }
            else if (tvShowWindowActive)
            {
                currPoint = returnPointB;
                currControl = tvControlList[currPoint.x];
                CenterMouseOverControl(currControl);
            }
            else if (PlaybackSession.IsCartoonShuffle || PlaybackSession.IsHistoryWatch)
            {
                currPoint = returnPointA;
                currControl = mainWindowControlGrid[currPoint.x][currPoint.y];
                CenterMouseOverControl(currControl);
                PlaybackSession.End();
                playerWindowActive = false;
            }
        }

        private async void CloseMainWindow()
        {
            gui.mainScrollViewer.Dispatcher.Invoke(() => { gui.mainScrollViewer.ScrollToHome(); });
            WpfTreeHelpers.DoEvents();
            CenterMouseOverControl(gui.mainCloseButton);
            await Task.Delay(200);
            TcpSerialListener.DoMouseClick();
        }

        private void MoveTvPoint(int x) =>
            MoveAlong1D(x, tvControlList, MainWindow.gui.episodeScrollViewer);

        // Walks playerControlList left/right. No scrollViewer because the
        // player has a single fixed-position row of buttons; nothing to
        // scroll into view. WakeOverlay() makes the overlayGrid visible so
        // the user can see which button they just focused (it would otherwise
        // still be hidden from a prior pollingTimer fire).
        private void MovePlayerPoint(int delta)
        {
            gui.playerWindow?.WakeOverlay();

            // First arrow press after entering the player just reveals the
            // cursor at the play button - no delta applied. Subsequent presses
            // step from there. Without this, currPoint.x is already 2 (play)
            // and the first LEFT would land on rewind without the user ever
            // seeing the cursor at play - which makes orientation confusing.
            if (playerCursorParked)
            {
                playerCursorParked = false;
                CenterMouseOverControl(currControl, currPoint.x, scrollViewer: null);
                return;
            }
            MoveAlong1D(delta, playerControlList, scrollViewer: null);
        }

        // Reset currPoint to the play button (middle of the 5-button row) on
        // each player open. Without this, currPoint carries whatever index
        // the previous window (TvShowWindow / MovieWindow) left it at, which
        // could be 0 (locking Left out immediately) or out of range.
        // Doesn't warp the cursor - PlayerWindow_Loaded explicitly hides it
        // until the user's first arrow press reveals it (see MovePlayerPoint).
        private void EnterPlayerNav()
        {
            if (playerControlList.Count == 0) return;
            int defaultIdx = playerControlList.Count / 2;  // play button
            currPoint = (defaultIdx, 0);
            currControl = playerControlList[defaultIdx];
            playerIndex = defaultIdx;
            playerCursorParked = true;
        }

        private void MoveLangPoint(int x) =>
            MoveAlong1D(x, langComboBoxItems, MainWindow.gui.langScrollViewer);

        private void MoveMoviePoint(int x) =>
            MoveAlong1D(x, new object[] { movieBackdrop, movieLangComboBox }, scrollViewer: null);

        // Shared 1-D navigation (used by movie / tv-show / language-dropdown
        // overlays where the items are a flat indexable list rather than a 2-D
        // grid). Steps currPoint.x by `delta`, clamps to range, updates currControl,
        // and centers the cursor (optionally scrolling the supplied viewer).
        private void MoveAlong1D(int delta, System.Collections.IList controls, ScrollViewer scrollViewer)
        {
            int newIndex = currPoint.x + delta;
            if (newIndex < 0 || newIndex >= controls.Count) return;
            currPoint = (newIndex, currPoint.y);
            currControl = controls[newIndex];
            CenterMouseOverControl(currControl, newIndex, scrollViewer);
        }

        // Convert a linear season-form index to (row, col) in the
        // 3-column season-picker grid, and mark that cell as the
        // currently-selected one (state value 2).
        private (int x, int y) GetCurrSeasonPoint(int seasonFormIndex)
        {
            const int columnsPerRow = 3;
            int row = seasonFormIndex / columnsPerRow;
            int col = seasonFormIndex % columnsPerRow;
            seasonWindowGrid[row][col] = 2;
            return (row, col);
        }

        public void MoveSeasonPoint((int x, int y) movePoint)
        {
            MoveInGrid(movePoint, seasonWindowGrid, seasonWindowControlGrid, columns: 3, MainWindow.gui.seasonScrollViewer, wrapVertically: false);
        }

        // ---------- Shared grid navigation helpers ----------
        //
        // Previously this file had two parallel sets of these (MoveSeasonPoint /
        // NextSeasonGridPoint / ClosestSeasonGridPoint / OutOfSeasonGridRange and
        // their MainGrid twins). Consolidated into the parameterized helpers
        // below; MovePoint and MoveSeasonPoint are now one-liners that just bind
        // the per-grid config (columns, wrap behavior, scroll viewer).

        private void MoveInGrid<T>((int x, int y) move, List<int[]> grid, List<T[]> controls, int columns, ScrollViewer scrollViewer, bool wrapVertically)
            where T : class
        {
            (int x, int y) newPoint = (currPoint.x + move.x, currPoint.y + move.y);
            if (wrapVertically)
            {
                if (newPoint.x == -1) newPoint.x = grid.Count - 1;
                if (newPoint.x == grid.Count) newPoint.x = 0;
            }
            if (IsOutOfRange(newPoint, grid, columns)) return;

            if (controls[newPoint.x][newPoint.y] == null)
            {
                (int x, int y) candidatePoint = ClosestOccupied(newPoint, controls, columns);
                if (candidatePoint.x != -1)
                {
                    newPoint = candidatePoint;
                }
                else
                {
                    newPoint = NextOccupied(newPoint, move, grid, controls, columns);
                    if (newPoint.x == -1) return;
                }
            }

            grid[newPoint.x][newPoint.y] = 2;
            grid[currPoint.x][currPoint.y] = 1;
            currPoint = newPoint;
            currControl = controls[currPoint.x][currPoint.y];
            CenterMouseOverControl(currControl, currPoint.x, scrollViewer);
        }

        private static bool IsOutOfRange((int x, int y) p, List<int[]> grid, int columns)
        {
            return p.y < 0 || p.x < 0 || p.y >= columns || p.x >= grid.Count;
        }

        // Walk one cell at a time in the requested direction until we hit
        // an occupied cell (return its coords) or leave the grid (return
        // sentinel (-1, -1)). Skipping over empty cells matters for the
        // sparse last-row case: a row with only the first two slots filled
        // shouldn't trap the cursor when the user navigates right.
        private static (int x, int y) NextOccupied<T>((int x, int y) current, (int x, int y) move, List<int[]> grid, List<T[]> controls, int columns)
            where T : class
        {
            (int x, int y) next = (current.x + move.x, current.y + move.y);
            if (IsOutOfRange(next, grid, columns)) return (-1, -1);
            if (controls[next.x][next.y] == null)
            {
                return NextOccupied(next, move, grid, controls, columns);
            }
            return next;
        }

        // From `point`, search left and right within the same row for the
        // nearest occupied cell. Returns sentinel (-1, -1) if the entire
        // row is empty (shouldn't happen in practice).
        private static (int x, int y) ClosestOccupied<T>((int x, int y) point, List<T[]> controls, int columns)
            where T : class
        {
            int low = point.y - 1;
            int high = point.y + 1;
            while (low >= 0 || high < columns)
            {
                if (low >= 0 && controls[point.x][low] != null) return (point.x, low);
                if (high < columns && controls[point.x][high] != null) return (point.x, high);
                low--;
                high++;
            }
            return (-1, -1);
        }

        // Lay out the season-picker tiles as rows of 3. Builds both the
        // occupancy grid (1 = filled, 0 = empty trailing slot in last row)
        // and the parallel control-reference grid in one pass.
        private void BuildSeasonGrid()
        {
            const int columnsPerRow = 3;
            int[] currRow = null;
            Image[] currControlRow = null;
            for (int i = 0; i < seasonControlList.Count; i++)
            {
                int col = i % columnsPerRow;
                if (col == 0)
                {
                    currRow = new int[columnsPerRow];
                    currControlRow = new Image[columnsPerRow];
                    seasonWindowGrid.Add(currRow);
                    seasonWindowControlGrid.Add(currControlRow);
                }
                currRow[col] = 1;
                currControlRow[col] = seasonControlList[i];
            }
        }

        public void MovePoint((int x, int y) movePoint)
        {
            MoveInGrid(movePoint, mainWindowGrid, mainWindowControlGrid, columns: 6, MainWindow.gui.mainScrollViewer, wrapVertically: true);
        }

        // Lay out the main screen as three independent sections (TV shows,
        // cartoons, movies), each chunked into rows of 6. Each section
        // begins on a fresh row even if the previous section's last row
        // was only partially full.
        //
        // The layout also inserts two single-cell pseudo-rows that hold the
        // History (above TV) and Shuffle (between TV and Cartoons) marathon
        // buttons - so joystick / IR-remote nav lands on them naturally.
        private void BuildMainWindowGrid()
        {
            AddButtonPseudoRow();                                      // Row 0: History
            AppendMainGridSection(gui.TvShows.Count, columnsPerRow: 6);
            AddButtonPseudoRow();                                      // between TV and Cartoons: Shuffle
            AppendMainGridSection(gui.Cartoons.Count, columnsPerRow: 6);
            AppendMainGridSection(gui.Movies.Count, columnsPerRow: 6);
            BuildMainWindowControlGrid();
        }

        // Append a row where only column 0 is occupied (1) and cols 1..N-1
        // are sentinels (0). Used to give the History / Shuffle buttons a
        // dedicated navigable cell in the otherwise poster-only grid.
        private void AddButtonPseudoRow()
        {
            int[] row = new int[MainGridColumns];
            row[0] = 1;
            mainWindowGrid.Add(row);
            mainWindowControlGrid.Add(new FrameworkElement[MainGridColumns]);
        }

        private void AppendMainGridSection(int itemCount, int columnsPerRow)
        {
            int[] currGridRow = null;
            for (int j = 0; j < itemCount; j++)
            {
                int col = j % columnsPerRow;
                if (col == 0)
                {
                    currGridRow = new int[columnsPerRow];
                    mainWindowGrid.Add(currGridRow);
                    mainWindowControlGrid.Add(new FrameworkElement[columnsPerRow]);
                }
                currGridRow[col] = 1;
            }
        }

        private const int MainGridColumns = 6;

        private void BuildMainWindowControlGrid()
        {
            // Look up the three poster ListViews by Name. Originally indexed
            // into mainGrid.Children with hardcoded slots [6, 2, 4], but those
            // shifted the moment History / Shuffle buttons were added as
            // siblings - and would shift again next time anyone touches the
            // XAML. Name-based lookup survives reordering.
            //
            // Walking them in [Movies, TvShows, Cartoons] order gives
            // mainWindowControlList as [Movies..., TvShows..., Cartoons...];
            // the grid below consumes them in a different order (TvShows first,
            // then Cartoons, then Movies), so controlIndex jumps explicitly.
            ListView tvList = null, cartoonsList = null, movieList = null;
            foreach (UIElement child in gui.mainGrid.Children)
            {
                if (child is ListView lv)
                {
                    switch (lv.Name)
                    {
                        case "TvShowListView":   tvList = lv; break;
                        case "CartoonsListView": cartoonsList = lv; break;
                        case "MovieListView":    movieList = lv; break;
                    }
                }
            }
            ListView[] lists = { movieList, tvList, cartoonsList };
            ObservableCollection<MainWindowBox>[] collections = { gui.Movies, gui.TvShows, gui.Cartoons };

            List<Image> mainWindowControlList = new List<Image>();
            for (int i = 0; i < lists.Length; i++)
            {
                ItemContainerGenerator generator = lists[i].ItemContainerGenerator;
                foreach (MainWindowBox box in collections[i])
                {
                    ListViewItem container = (ListViewItem)generator.ContainerFromItem(box);
                    Image img = WpfTreeHelpers.GetChildrenByType(container, typeof(Image), "mainGridImage") as Image;
                    mainWindowControlList.Add(img);
                }
            }

            // Row layout produced by BuildMainWindowGrid:
            //   row 0:                          History pseudo-row
            //   rows 1..1+ceil(TV/6):           TV posters
            //   row M:                          Shuffle pseudo-row
            //   rows M+1..:                     Cartoon posters
            //   rows ...:                       Movie posters

            int row = 0;
            int controlIndex = gui.Movies.Count;  // start at TvShows in source list

            // History pseudo-row.
            if (row < mainWindowGrid.Count) mainWindowControlGrid[row][0] = gui.historyButton;
            row++;

            // TV poster rows.
            row = FillSectionRows(gui.TvShows.Count, row, mainWindowControlList, ref controlIndex);

            // Shuffle pseudo-row.
            if (row < mainWindowGrid.Count) mainWindowControlGrid[row][0] = gui.shuffleButton;
            row++;

            // Cartoon poster rows.
            row = FillSectionRows(gui.Cartoons.Count, row, mainWindowControlList, ref controlIndex);

            // Movie poster rows. controlIndex resets because Movies live at the
            // start of mainWindowControlList.
            controlIndex = 0;
            FillSectionRows(gui.Movies.Count, row, mainWindowControlList, ref controlIndex);
        }

        // Fills mainWindowControlGrid[row..row+ceil(itemCount/6)] from `source`
        // starting at `controlIndex`. Returns the row index just past the last
        // filled row, so the caller can drop in the next section / pseudo-row.
        private int FillSectionRows(int itemCount, int startRow, List<Image> source, ref int controlIndex)
        {
            int rowsNeeded = (itemCount + MainGridColumns - 1) / MainGridColumns;
            int row = startRow;
            for (int r = 0; r < rowsNeeded; r++, row++)
            {
                if (row >= mainWindowGrid.Count) break;
                for (int c = 0; c < MainGridColumns; c++)
                {
                    AssignTileOrSentinel(row, c, source, ref controlIndex);
                }
            }
            return row;
        }

        // mainWindowGrid[r][c] encodes whether that cell is filled (1) or a
        // sentinel for "ragged trailing slot in last row" (0). Mirror the
        // assignment into mainWindowControlGrid: real control if filled,
        // null if sentinel. controlIndex only advances on real assignments.
        private void AssignTileOrSentinel(int row, int col, List<Image> source, ref int controlIndex)
        {
            if (mainWindowGrid[row][col] == 0)
            {
                mainWindowControlGrid[row][col] = null;
            }
            else
            {
                mainWindowControlGrid[row][col] = source[controlIndex++];
            }
        }

        /// <summary>
        /// Move the cursor to the center of <paramref name="control"/>.
        /// Three flavors:
        ///   - ComboBoxItem: scrolls the combo dropdown into view, with an
        ///     async delay because the popup needs a tick to lay out.
        ///   - Image: scrolls the main/season/tv grid into view if a scrollViewer
        ///     is supplied, then centers.
        ///   - Anything else (Button, Label, ToggleButton, ComboBox, ...):
        ///     plain center-on-screen via CenterMouseOverElement.
        /// </summary>
        private void CenterMouseOverControl(object control, int row = -1, ScrollViewer scrollViewer = null)
        {
            try
            {
                switch (control)
                {
                    case ComboBoxItem cbi:
                        CenterMouseOverComboBoxItem(cbi, row, scrollViewer);
                        break;
                    case Image img:
                        CenterMouseOverImage(img, row, scrollViewer);
                        break;
                    case Button btn when scrollViewer != null:
                        // Buttons embedded in the main nav grid (History /
                        // Shuffle pseudo-rows) need the same scroll-row-into-
                        // view treatment as poster Images do.
                        btn.Dispatcher.Invoke(() =>
                        {
                            ScrollGridRowIntoView(scrollViewer, row, btn);
                            WarpCursorToCenter(btn);
                        });
                        break;
                    case FrameworkElement fe:
                        CenterMouseOverElement(fe);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }

        /// <summary>Plain "warp cursor to the visual center of this element."</summary>
        private static void CenterMouseOverElement(FrameworkElement element)
        {
            element.Dispatcher.Invoke(() => WarpCursorToCenter(element));
        }

        private void CenterMouseOverImage(Image image, int row = -1, ScrollViewer scrollViewer = null)
        {
            image.Dispatcher.Invoke(() =>
            {
                ScrollGridRowIntoView(scrollViewer, row, image);
                WarpCursorToCenter(image);
            });
        }

        private async void CenterMouseOverComboBoxItem(ComboBoxItem comboBoxItem, int row = -1, ScrollViewer scrollViewer = null)
        {
            await Task.Delay(100);
            ScrollDropdownRowIntoView(scrollViewer, row, comboBoxItem);
            comboBoxItem.Dispatcher.Invoke(() => WarpCursorToCenter(comboBoxItem));
        }

        private static void CenterMouseOverComboBoxItem(Point p, ComboBoxItem c)
        {
            p.X += c.ActualWidth / 2;
            p.Y += c.ActualHeight / 2;
            ComInterop.SetCursorPos((int)p.X, (int)p.Y);
        }

        // Cursor-warp tail shared by every "center on control" path: take
        // the control's top-left in screen coords, add half its rendered
        // size, SetCursorPos. The caller is responsible for Dispatcher
        // marshalling because the rules differ per call site.
        //
        // Guards: PointToScreen requires the element to be connected to a
        // live PresentationSource (i.e., hosted in a Window with a valid
        // HWND). When a window has just been closed or hasn't finished
        // initializing, PointToScreen throws Win32Exception "Invalid window
        // handle" - which used to bubble up through SeasonWindow.ShowDialog
        // / TvShowWindow callbacks and crash the app. Skip the warp in
        // those cases; the user just doesn't see a cursor move.
        private static void WarpCursorToCenter(FrameworkElement element)
        {
            if (element == null) return;
            if (PresentationSource.FromVisual(element) == null) return;
            try
            {
                Point target = element.PointToScreen(new Point(0, 0));
                target.X += element.ActualWidth / 2;
                target.Y += element.ActualHeight / 2;
                ComInterop.SetCursorPos((int)target.X, (int)target.Y);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Log.Warning("WarpCursorToCenter skipped: {Msg} (element {Type})", ex.Message, element.GetType().Name);
            }
            catch (InvalidOperationException ex)
            {
                Log.Warning("WarpCursorToCenter skipped: {Msg} (element {Type})", ex.Message, element.GetType().Name);
            }
        }

        // Grid (image-tile) variant of the scroll dispatcher: row==0 goes
        // home, row==(last) goes to the bottom, anything else brings the
        // tile itself into view via the BringIntoView event.
        private void ScrollGridRowIntoView(ScrollViewer scrollViewer, int row, FrameworkElement target)
        {
            if (scrollViewer == null) return;
            if (row == 0)
            {
                scrollViewer.ScrollToHome();
            }
            else if ((seasonWindowActive && row == seasonWindowGrid.Count - 1) ||
                     (tvShowWindowActive && row == tvControlList.Count - 1) ||
                     (mainWindowActive && row == mainWindowGrid.Count - 1))
            {
                scrollViewer.ScrollToBottom();
            }
            else
            {
                gui.scrollViewerAdjust = true;
                target.BringIntoView();
            }
            WpfTreeHelpers.DoEvents();
        }

        // Dropdown variant of the scroll dispatcher (used for the language
        // ComboBox popup). Same shape as the grid version but checks
        // row==langComboBoxItems.Count for the bottom edge and dispatches
        // the scroll on the ScrollViewer's own dispatcher to be safe across
        // popup teardown.
        private void ScrollDropdownRowIntoView(ScrollViewer scrollViewer, int row, ComboBoxItem item)
        {
            if (scrollViewer == null) return;
            if (row == 0)
            {
                scrollViewer.Dispatcher.Invoke(() => scrollViewer.ScrollToHome());
            }
            else if (row == langComboBoxItems.Count)
            {
                scrollViewer.Dispatcher.Invoke(() => scrollViewer.ScrollToBottom());
            }
            else
            {
                gui.scrollViewerAdjust = true;
                item.Dispatcher.Invoke(() => item.BringIntoView());
            }
            WpfTreeHelpers.DoEvents();
        }
    }
}
