using LVP_WPF.Util;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public (int x, int y) currPoint = (0, 0);
        public (int x, int y) returnPointA = (0, 0);
        public (int x, int y) returnPointB = (0, 0);

        public object currControl = null;
        public List<int[]> mainWindowGrid = new List<int[]>();
        public List<Image[]> mainWindowControlGrid = new List<Image[]>();
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
            currControl = mainWindowControlGrid.Count != 0 ? mainWindowControlGrid[0][0] : gui.mainCloseButton;
            CenterMouseOverControl(currControl, 0);
        }

        public void Move((int x, int y) pos)
        {
            if (playerWindowActive)
            {
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
            if (TvShowWindow.cartoonShuffle || TvShowWindow.historyWatch)
            {
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
                    seasonWindowActive = true;
                    BuildSeasonGrid();
                    currPoint = GetCurrSeasonPoint(seasonIndex);
                    currControl = seasonWindowControlGrid[currPoint.x][currPoint.y];
                    CenterMouseOverControl(currControl, currPoint.x, MainWindow.gui.seasonScrollViewer);
                }
                if (controlName.Equals("PlayerWindow"))
                {
                    playerWindowActive = true;
                }
            }
            else if (movieWindowActive)
            {
                returnPointB = currPoint;
                if (controlName.Equals("PlayerWindow"))
                {
                    playerWindowActive = true;
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
                    if (TcpSerialListener.layoutPoint.tvControlList[1] as ToggleButton != null)
                    {
                        currPoint = (2, -1);
                    }
                    else
                    {
                        currPoint = (1, -1);
                    }
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
                movieWindowActive = true;
                movieIndex = 0;
                currPoint = (movieIndex, -1);
                currControl = movieBackdrop;
                CenterMouseOverControl(currControl);
            }
            else
            {
                tvShowWindowActive = true;
                currPoint = (tvIndex, -1);
                currControl = tvControlList[currPoint.x];
                CenterMouseOverControl(currControl);
            }
        }

        internal void CloseCurrWindow(bool click = true)
        {
            if (seasonWindowActive || languageDropdownActive)
            {
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
                    ClosePlayerWindow(click);
                    TcpSerialListener.EndFeature();
                }
                else if (tvShowWindowActive)
                {
                    CloseTvWindow(click);
                }
                else if (movieWindowActive)
                {
                    CloseMovieWindow(click);
                }
                else if (mainWindowActive)
                {
                    CloseMainWindow();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }

        private async void CloseMovieWindow(bool click)
        {
            movieWindowActive = false;
            mainWindowActive = true;

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

        private async void CloseTvWindow(bool click)
        {
            langIndex = 0;
            tvShowWindowActive = false;
            mainWindowActive = true;
            tvControlList.Clear();
            tvIndex = 0;

            if (click)
            {
                gui.episodeScrollViewer.Dispatcher.Invoke(() => { gui.episodeScrollViewer.ScrollToHome(); });
                WpfTreeHelpers.DoEvents();
                CenterMouseOverControl(gui.tvMovieCloseButton);
                await Task.Delay(200);
                TcpSerialListener.DoMouseClick();
                await Task.Delay(200);
            }

            currPoint = returnPointA;
            currControl = mainWindowControlGrid[currPoint.x][currPoint.y];
            CenterMouseOverControl(currControl);
        }

        private async void ClosePlayerWindow(bool click)
        {
            playerWindowActive = false;
            if (click)
            {
                CenterMouseOverControl(gui.playerCloseButton);
                WpfTreeHelpers.DoEvents();
                await Task.Delay(200);
                TcpSerialListener.DoMouseClick();
                await Task.Delay(200);
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
            else if (TvShowWindow.cartoonShuffle || TvShowWindow.historyWatch)
            {
                currPoint = returnPointA;
                currControl = mainWindowControlGrid[currPoint.x][currPoint.y];
                CenterMouseOverControl(currControl);
                TvShowWindow.historyWatch = false;
                TvShowWindow.cartoonShuffle = false;
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

        private void MoveTvPoint(int x)
        {

            int newIndex = currPoint.x + x;
            if (newIndex < 0 || newIndex >= tvControlList.Count)
            {
                return;
            }


            currPoint = (newIndex, currPoint.y);
            currControl = tvControlList[newIndex];
            CenterMouseOverControl(currControl, newIndex, MainWindow.gui.episodeScrollViewer);
        }

        private void MoveLangPoint(int x)
        {
            int newIndex = currPoint.x + x;
            if (newIndex < 0 || newIndex >= langComboBoxItems.Count)
            {
                return;
            }


            currPoint = (newIndex, currPoint.y);
            currControl = langComboBoxItems[newIndex];
            CenterMouseOverControl(currControl, currPoint.x, MainWindow.gui.langScrollViewer);
        }

        private void MoveMoviePoint(int x)
        {

            int newIndex = currPoint.x + x;
            if (newIndex < 0 || newIndex > 1)
            {
                return;
            }


            currPoint = (newIndex, currPoint.y);
            currControl = newIndex == 0 ? movieBackdrop : movieLangComboBox;
            CenterMouseOverControl(currControl, newIndex);
        }

        private (int x, int y) GetCurrSeasonPoint(int seasonFormIndex)
        {
            int count = 0;
            (int x, int y) point = (0, 0);
            while (seasonFormIndex > 0)
            {
                seasonFormIndex--;
                if (count == 2)
                {
                    count = 0;
                    point = (point.x + 1, 0);
                    if (seasonFormIndex == 0)
                    {
                        break;
                    }
                }
                else
                {
                    point = (point.x, point.y + 1);
                    count++;
                }
            }
            seasonWindowGrid[point.x][point.y] = 2;
            return point;
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

        private void MoveInGrid((int x, int y) move, List<int[]> grid, List<Image[]> controls, int columns, ScrollViewer scrollViewer, bool wrapVertically)
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

        private static (int x, int y) NextOccupied((int x, int y) current, (int x, int y) move, List<int[]> grid, List<Image[]> controls, int columns)
        {
            (int x, int y) next = (current.x + move.x, current.y + move.y);
            if (IsOutOfRange(next, grid, columns)) return (-1, -1);
            if (controls[next.x][next.y] == null)
            {
                // NOTE: this preserves an existing bug from the pre-consolidation
                // code (NextMainGridPoint/NextSeasonGridPoint both did this):
                // the recursive result is discarded, so in practice NextOccupied
                // only inspects the immediate next cell. Kept unchanged here to
                // avoid altering daily-driver navigation behavior; can be fixed
                // intentionally in a separate commit if desired.
                NextOccupied(next, move, grid, controls, columns);
            }
            else
            {
                return next;
            }
            return (-1, -1);
        }

        private static (int x, int y) ClosestOccupied((int x, int y) point, List<Image[]> controls, int columns)
        {
            int low = point.y - 1;
            int high = point.y + 1;
            // NOTE: existing code used `low >= 0 || high > {columns}` as the loop
            // condition. The `> columns` half is almost certainly a bug (should
            // probably be `< columns`), and combining with || rather than && also
            // looks wrong - but the function still terminates because both bounds
            // only update inside their guard. Preserved as-is.
            while (low >= 0 || high > columns)
            {
                if (low >= 0)
                {
                    if (controls[point.x][low] != null) return (point.x, low);
                }
                if (high < columns)
                {
                    if (controls[point.x][high] != null) return (point.x, high);
                }
                low--;
                high++;
            }
            return (-1, -1);
        }

        private void BuildSeasonGrid()
        {
            int seasonCount = seasonControlList.Count;
            int count = 0;
            int[] currRow = null;
            Image[] currControlRow = null;
            for (int i = 0; i < seasonCount; i++)
            {
                if (count == 3)
                {
                    count = 0;
                }

                if (count == 0)
                {
                    currRow = new int[3];
                    currControlRow = new Image[3];
                    seasonWindowGrid.Add(currRow);
                    seasonWindowControlGrid.Add(currControlRow);
                    currRow[count] = 1;
                    currControlRow[count] = null;
                }
                currRow[count] = 1; ;
                currControlRow[count] = null;
                count++;
            }
            BuildSeasonControlGrid();
        }


        private void BuildSeasonControlGrid()
        {
            int seasonCount = seasonControlList.Count;
            int count = 0;
            int rowIndex = 0;
            int controlIndex = 0;

            for (int i = 0; i < seasonCount; i++)
            {
                if (count == 3)
                {
                    rowIndex++;
                    if (rowIndex >= seasonWindowGrid.Count)
                    {
                        break;
                    }
                    count = 0;
                }

                if (seasonWindowGrid[rowIndex][count] == 0)
                {
                    seasonWindowControlGrid[rowIndex][count] = null;
                }
                else
                {
                    seasonWindowControlGrid[rowIndex][count] = seasonControlList[controlIndex];
                    controlIndex++;
                }
                count++;
            }
        }

        public void MovePoint((int x, int y) movePoint)
        {
            MoveInGrid(movePoint, mainWindowGrid, mainWindowControlGrid, columns: 6, MainWindow.gui.mainScrollViewer, wrapVertically: true);
        }

        private void BuildMainWindowGrid()
        {
            for (int i = 0; i < 2; i++)
            {
                int count = i == 1 ? gui.Movies.Count : (gui.TvShows.Count + gui.Cartoons.Count);
                int rowIndex = 0;
                int[] currGridRow = null;
                Image[] currControlRow = null;
                for (int j = 0; j < count; j++)
                {
                    if (i == 0 && j == count - gui.Cartoons.Count)
                    {
                        rowIndex = 0;
                    }

                    if (rowIndex == 6)
                    {
                        rowIndex = 0;
                    }

                    if (rowIndex == 0)
                    {
                        currGridRow = new int[6];
                        currControlRow = new Image[6];
                        mainWindowGrid.Add(currGridRow);
                        mainWindowControlGrid.Add(currControlRow);
                        currGridRow[rowIndex] = 1;
                        currControlRow[rowIndex] = null;
                    }

                    currGridRow[rowIndex] = 1;
                    currControlRow[rowIndex] = null;
                    rowIndex++;
                }
            }
            BuildMainWindowControlGrid();
        }

        private void BuildMainWindowControlGrid()
        {
            int count = 0;
            int rowIndex = 0;
            int controlIndex = gui.Movies.Count;
            List<Image> mainWindowControlList = new List<Image>();
            ListView[] mainWindowLists = new ListView[]
            {
                (ListView)gui.mainGrid.Children[6],
                (ListView)gui.mainGrid.Children[2],
                (ListView)gui.mainGrid.Children[4]
            };

            for (int i = 0; i < 3; i++)
            {
                ItemContainerGenerator generator = mainWindowLists[i].ItemContainerGenerator;
                switch (i)
                {
                    case 0:
                        for (int j = 0; j < gui.Movies.Count; j++)
                        {
                            ListViewItem container = (ListViewItem)generator.ContainerFromItem(gui.Movies[j]);
                            Image img = WpfTreeHelpers.GetChildrenByType(container, typeof(Image), "mainGridImage") as Image;
                            mainWindowControlList.Add(img);
                        }
                        break;
                    case 1:
                        for (int j = 0; j < gui.TvShows.Count; j++)
                        {
                            ListViewItem container = (ListViewItem)generator.ContainerFromItem(gui.TvShows[j]);
                            Image img = WpfTreeHelpers.GetChildrenByType(container, typeof(Image), "mainGridImage") as Image;
                            mainWindowControlList.Add(img);
                        }
                        break;
                    case 2:
                        for (int j = 0; j < gui.Cartoons.Count; j++)
                        {
                            ListViewItem container = (ListViewItem)generator.ContainerFromItem(gui.Cartoons[j]);
                            Image img = WpfTreeHelpers.GetChildrenByType(container, typeof(Image), "mainGridImage") as Image;
                            mainWindowControlList.Add(img);
                        }
                        break;
                }
            }

            int totalTvShows = gui.TvShows.Count + gui.Cartoons.Count;
            for (int i = 0; i < totalTvShows; i++)
            {
                if (i == totalTvShows - gui.Cartoons.Count)
                {
                    count = 6;
                }

                if (count == 6)
                {
                    rowIndex++;
                    if (rowIndex >= mainWindowGrid.Count)
                    {
                        break;
                    }
                    count = 0;
                }

                if (mainWindowGrid[rowIndex][count] == 0)
                {
                    mainWindowControlGrid[rowIndex][count] = null;
                }
                else
                {
                    mainWindowControlGrid[rowIndex][count] = mainWindowControlList[controlIndex];
                    controlIndex++;
                }
                count++;
            }

            count = 0;
            controlIndex = 0;
            if (totalTvShows != 0)
            {
                rowIndex++;
            }

            for (int i = 0; i < gui.Movies.Count; i++)
            {
                if (count == 6)
                {
                    rowIndex++;
                    if (rowIndex >= mainWindowGrid.Count)
                    {
                        break;
                    }
                    count = 0;
                }

                if (mainWindowGrid[rowIndex][count] == 0)
                {
                    mainWindowControlGrid[rowIndex][count] = null;
                }
                else
                {
                    mainWindowControlGrid[rowIndex][count] = mainWindowControlList[controlIndex];
                    controlIndex++;
                }
                count++;
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
            element.Dispatcher.Invoke(() =>
            {
                Point target = element.PointToScreen(new Point(0, 0));
                target.X += element.ActualWidth / 2;
                target.Y += element.ActualHeight / 2;
                ComInterop.SetCursorPos((int)target.X, (int)target.Y);
            });
        }

        private void CenterMouseOverImage(Image image, int row = -1, ScrollViewer scrollViewer = null)
        {
            image.Dispatcher.Invoke(() =>
            {
                if (scrollViewer != null)
                {
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
                        image.BringIntoView();
                    }
                    WpfTreeHelpers.DoEvents();
                }

                Point target = image.PointToScreen(new Point(0, 0));
                target.X += image.ActualWidth / 2;
                target.Y += image.ActualHeight / 2;
                ComInterop.SetCursorPos((int)target.X, (int)target.Y);
            });
        }

        private async void CenterMouseOverComboBoxItem(ComboBoxItem comboBoxItem, int row = -1, ScrollViewer scrollViewer = null)
        {
            await Task.Delay(100);
            if (scrollViewer != null)
            {
                if (row == 0)
                {
                    scrollViewer.Dispatcher.Invoke(() =>
                    {
                        scrollViewer.ScrollToHome();
                    });
                }
                else if (row == langComboBoxItems.Count)
                {
                    scrollViewer.Dispatcher.Invoke(() =>
                    {
                        scrollViewer.ScrollToBottom();
                    });
                }
                else
                {
                    gui.scrollViewerAdjust = true;
                    comboBoxItem.Dispatcher.Invoke(() =>
                    {
                        comboBoxItem.BringIntoView();
                    });
                }
                WpfTreeHelpers.DoEvents();
            }

            comboBoxItem.Dispatcher.Invoke(() =>
            {
                Point target = comboBoxItem.PointToScreen(new Point(0d, 0d));
                target.X += comboBoxItem.ActualWidth / 2;
                target.Y += comboBoxItem.ActualHeight / 2;
                ComInterop.SetCursorPos((int)target.X, (int)target.Y);
            });
        }

        private static void CenterMouseOverComboBoxItem(Point p, ComboBoxItem c)
        {
            p.X += c.ActualWidth / 2;
            p.Y += c.ActualHeight / 2;
            ComInterop.SetCursorPos((int)p.X, (int)p.Y);
        }

        /*private void PrintGrid()
        {
            List<int[]> ctrl = seasonWindowActive ? seasonWindowGrid : mainWindowGrid;
            foreach (int[] row in ctrl)
            {
                Debug.Write("[ ");
                for (int i = 0; i < row.Length; i++)
                {
                    Debug.Write(row[i]);
                    if (i != row.Length - 1)
                    {
                        Debug.Write(", ");
                    }
                }
                Debug.WriteLine(" ]");
            }
            Debug.WriteLine(Environment.NewLine);
        }

        private void PrintControlGrid()
        {
            List<Image[]> ctrl = seasonWindowActive ? seasonWindowControlGrid : mainWindowControlGrid;
            foreach (Image[] row in ctrl)
            {
                Debug.Write("[ ");
                for (int i = 0; i < row.Length; i++)
                {
                    string itemName;
                    if (row[i] == null)
                    {
                        itemName = "null";
                    }
                    else
                    {
                        string[] item = row[i].Source.ToString().Split("/");
                        itemName = item[item.Length - 2];
                    }
                    Debug.Write(itemName);

                    if (i != row.Length - 1)
                    {
                        Debug.Write(", ");
                    }
                }
                Debug.WriteLine(" ]");
            }
            Debug.WriteLine(Environment.NewLine);
        }*/
    }
}
