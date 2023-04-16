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
    public class LayoutPoint
    {
        public GuiModel gui;
        public bool mainWindowActive = true;
        public bool movieWindowActive = false;
        public bool tvShowWindowActive = false;
        public bool seasonWindowActive = false;
        public bool playerWindowActive = false;
        public bool lanuageDropdownActive = false;
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
            TcpSerialListener.SetCursorPos(20, 20);
            TcpSerialListener.DoMouseClick();
            currControl = mainWindowControlGrid.Count != 0 ? mainWindowControlGrid[0][0] : gui.mainCloseButton;
            CenterMouseOverControl(currControl, 0);
        }

        public void Move((int x, int y) pos)
        {
            if (playerWindowActive) return;
            if (lanuageDropdownActive)
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
            if (TvShowWindow.cartoonShuffle) return;

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

            SelectChildWindow(controlName, isMovie);
        }

        private void SelectChildWindow(string controlName, bool isMovie)
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
                if (controlName.Equals("PlayerWindow")) { playerWindowActive = true; }
            }
            else if (movieWindowActive)
            {
                returnPointB = currPoint;
                if (controlName.Equals("PlayerWindow")) { playerWindowActive = true; }
            }
        }

        private async void SelectLangDropdown()
        {
            Task.Delay(200).Wait();
            if (!lanuageDropdownActive)
            {
                returnPointB = currPoint;
                lanuageDropdownActive = true;
                currPoint = (langIndex, -1);
                currControl = langComboBoxItems[currPoint.x];
                CenterMouseOverComboBoxItem(langComboBoxItemPts[currPoint.x], (ComboBoxItem)currControl);
            }
            else
            {
                langIndex = currPoint.x;
                lanuageDropdownActive = false;
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
            if (seasonWindowActive || lanuageDropdownActive) return;
            if (click) incomingSerialMsg = true;
            try
            {
                if (mainWindowActive)
                {
                    CloseMainWindow();
                }

                if (playerWindowActive)
                {
                    ClosePlayerWindow(click);
                    return;
                }

                if (tvShowWindowActive)
                {
                    CloseTvWindow(click);
                }

                if (movieWindowActive)
                {
                    CloseMovieWindow(click);
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
                GuiModel.DoEvents();
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
                GuiModel.DoEvents();
                await Task.Delay(200);
                TcpSerialListener.DoMouseClick();
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
        }

        private async void CloseMainWindow()
        {
            gui.mainScrollViewer.Dispatcher.Invoke(() => { gui.mainScrollViewer.ScrollToHome(); });
            GuiModel.DoEvents();
            CenterMouseOverControl(gui.mainCloseButton);
            await Task.Delay(200);
            TcpSerialListener.DoMouseClick();
        }

        private void MoveTvPoint(int x)
        {
            
            int newIndex = currPoint.x + x;
            if (newIndex < 0 || newIndex >= tvControlList.Count) return;

            currPoint = (newIndex, currPoint.y);
            currControl = tvControlList[newIndex];
            CenterMouseOverControl(currControl, newIndex, MainWindow.gui.episodeScrollViewer);
        }

        private void MoveLangPoint(int x)
        {
            int newIndex = currPoint.x + x;
            if (newIndex < 0 || newIndex >= langComboBoxItems.Count) return;

            currPoint = (newIndex, currPoint.y);
            currControl = langComboBoxItems[newIndex];
            CenterMouseOverControl(currControl, currPoint.x, MainWindow.gui.langScrollViewer);
        }

        private void MoveMoviePoint(int x)
        {

            int newIndex = currPoint.x + x;
            if (newIndex < 0 || newIndex > 1) return;

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
                    if (seasonFormIndex == 0) break;
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
            (int x, int y) newPoint = (currPoint.x + movePoint.x, currPoint.y + movePoint.y);
            if (OutOfSeasonGridRange(newPoint)) return;

            if (seasonWindowControlGrid[newPoint.x][newPoint.y] == null)
            {
                (int x, int y) candidatePoint = ClosestSeasonGridPoint(newPoint);
                if (candidatePoint.x != -1)
                {
                    newPoint = candidatePoint;
                }
                else
                {
                    newPoint = NextSeasonGridPoint(newPoint, movePoint);
                    if (newPoint.x == -1) return;
                }
            }

            seasonWindowGrid[newPoint.x][newPoint.y] = 2;
            seasonWindowGrid[currPoint.x][currPoint.y] = 1;
            currPoint = newPoint;
            currControl = seasonWindowControlGrid[currPoint.x][currPoint.y];
            CenterMouseOverControl(currControl, currPoint.x, MainWindow.gui.seasonScrollViewer);
        }

        public (int x, int y) NextSeasonGridPoint((int x, int y) currentPoint, (int x, int y) movePoint)
        {
            (int x, int y) nextPoint = (currentPoint.x + movePoint.x, currentPoint.y + movePoint.y);
            if (OutOfSeasonGridRange(nextPoint)) return (-1, -1);
            if (seasonWindowControlGrid[nextPoint.x][nextPoint.y] == null)
            {
                NextSeasonGridPoint(nextPoint, movePoint);
            }
            else
            {
                return nextPoint;
            }
            return (-1, -1);
        }

        private (int x, int y) ClosestSeasonGridPoint((int x, int y) nextPoint)
        {
            int low = nextPoint.y - 1;
            int high = nextPoint.y + 1;
            while (low >= 0 || high > 3)
            {
                if (low >= 0) if (seasonWindowControlGrid[nextPoint.x][low] != null) return (nextPoint.x, low);
                if (high < 3) if (seasonWindowControlGrid[nextPoint.x][high] != null) return (nextPoint.x, high);
                low--;
                high++;
            }
            return (-1, -1);
        }

        private bool OutOfSeasonGridRange((int x, int y) testPoint)
        {
            if (testPoint.y < 0 || testPoint.x < 0 || testPoint.y >= 3 || testPoint.x >= seasonWindowGrid.Count) return true;
            return false;
        }

        private void BuildSeasonGrid()
        {
            int seasonCount = seasonControlList.Count;
            int count = 0;
            int[] currRow = null;
            Image[] currControlRow = null;
            for (int i = 0; i < seasonCount; i++)
            {
                if (count == 3) count = 0;
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
                    if (rowIndex >= seasonWindowGrid.Count) break;
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
            (int x, int y) newPoint = (currPoint.x + movePoint.x, currPoint.y + movePoint.y);
            if (newPoint.x == -1)
            {
                newPoint.x = mainWindowGrid.Count - 1;
            }
            if (newPoint.x == mainWindowGrid.Count)
            {
                newPoint.x = 0;
            }
            if (OutOfMainGridRange(newPoint)) return;

            if (mainWindowControlGrid[newPoint.x][newPoint.y] == null)
            {
                (int x, int y) candidatePoint = ClosestMainGridPoint(newPoint);
                if (candidatePoint.x != -1)
                {
                    newPoint = candidatePoint;
                }
                else
                {
                    newPoint = NextMainGridPoint(newPoint, movePoint);
                    if (newPoint.x == -1) return;
                }
            }

            mainWindowGrid[newPoint.x][newPoint.y] = 2;
            mainWindowGrid[currPoint.x][currPoint.y] = 1;
            currPoint = newPoint;
            currControl = mainWindowControlGrid[currPoint.x][currPoint.y];
            CenterMouseOverControl(currControl, currPoint.x, MainWindow.gui.mainScrollViewer);
        }

        public (int x, int y) NextMainGridPoint((int x, int y) currentPoint, (int x, int y) movePoint)
        {
            (int x, int y) nextPoint = (currentPoint.x + movePoint.x, currentPoint.y + movePoint.y);
            if (OutOfMainGridRange(nextPoint)) return (-1, -1);

            if (mainWindowControlGrid[nextPoint.x][nextPoint.y] == null)
            {
                NextMainGridPoint(nextPoint, movePoint);
            }
            else
            {
                return nextPoint;
            }
            return (-1, -1);
        }

        private (int x, int y) ClosestMainGridPoint((int x, int y) nextPoint)
        {
            int low = nextPoint.y - 1;
            int high = nextPoint.y + 1;
            while (low >= 0 || high > 6)
            {
                if (low >= 0)
                {
                    if (mainWindowControlGrid[nextPoint.x][low] != null) return (nextPoint.x, low);
                }

                if (high < 6)
                {
                    if (mainWindowControlGrid[nextPoint.x][high] != null) return (nextPoint.x, high);
                }
                low--;
                high++;
            }
            return (-1, -1);
        }

        private bool OutOfMainGridRange((int x, int y) testPoint)
        {
            if (testPoint.y < 0 || testPoint.x < 0 || testPoint.y >= 6 || testPoint.x >= mainWindowGrid.Count) return true;
            return false;
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
                    if (i == 0 && j == count - gui.Cartoons.Count) rowIndex = 0;
                    if (rowIndex == 6) rowIndex = 0;

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
                            Image img = GuiModel.GetChildrenByType(container, typeof(Image), "mainGridImage") as Image;
                            mainWindowControlList.Add(img);
                        }
                        break;
                    case 1:
                        for (int j = 0; j < gui.TvShows.Count; j++)
                        {
                            ListViewItem container = (ListViewItem)generator.ContainerFromItem(gui.TvShows[j]);
                            Image img = GuiModel.GetChildrenByType(container, typeof(Image), "mainGridImage") as Image;
                            mainWindowControlList.Add(img);
                        }
                        break;
                    case 2:
                        for (int j = 0; j < gui.Cartoons.Count; j++)
                        {
                            ListViewItem container = (ListViewItem)generator.ContainerFromItem(gui.Cartoons[j]);
                            Image img = GuiModel.GetChildrenByType(container, typeof(Image), "mainGridImage") as Image;
                            mainWindowControlList.Add(img);
                        }
                        break;
                }
            }

            int totalTvShows = gui.TvShows.Count + gui.Cartoons.Count;
            for (int i = 0; i < totalTvShows; i++)
            {
                if (i == totalTvShows - gui.Cartoons.Count) count = 6;

                if (count == 6)
                {
                    rowIndex++;
                    if (rowIndex >= mainWindowGrid.Count) break;
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
                    if (rowIndex >= mainWindowGrid.Count) break;
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

        private void CenterMouseOverControl(object control, int row = -1, ScrollViewer scrollViewer = null)
        {
            if (control as ComboBoxItem != null)
            {
                ComboBoxItem comboBoxItem = (ComboBoxItem)control;
                CenterMouseOverComboBoxItem(comboBoxItem, row, scrollViewer);
            }
            if (control as ComboBox != null)
            {
                ComboBox comboBox = (ComboBox)control;
                CenterMouseOverComboBox(comboBox);
            }
            if (control as Button != null)
            {
                Button button = (Button)control;
                CenterMouseOverButton(button);
            }
            if (control as Image != null)
            {
                Image image = (Image)control;
                CenterMouseOverImage(image, row, scrollViewer);
            }
            if (control as ToggleButton != null)
            {
                ToggleButton tb = (ToggleButton)control;
                CenterMouseOverToggleButton(tb);
            }
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
                    GuiModel.DoEvents();
                }

                Point target = image.PointToScreen(new Point(0, 0));
                target.X += image.ActualWidth / 2;
                target.Y += image.ActualHeight / 2;
                TcpSerialListener.SetCursorPos((int)target.X, (int)target.Y);
            });
        }

        private void CenterMouseOverButton(Button button)
        {
            button.Dispatcher.Invoke(() =>
            {
                Point target = button.PointToScreen(new Point(0, 0));
                target.X += button.ActualWidth / 2;
                target.Y += button.ActualHeight / 2;
                TcpSerialListener.SetCursorPos((int)target.X, (int)target.Y);
            });
        }

        private void CenterMouseOverToggleButton(ToggleButton tb)
        {
            tb.Dispatcher.Invoke(() =>
            {
                Point target = tb.PointToScreen(new Point(0, 0));
                target.X += tb.ActualWidth / 2;
                target.Y += tb.ActualHeight / 2;
                TcpSerialListener.SetCursorPos((int)target.X, (int)target.Y);
            });
        }

        private void CenterMouseOverComboBox(ComboBox comboBox)
        {
            comboBox.Dispatcher.Invoke(() =>
            {
                Point target = comboBox.PointToScreen(new Point(0, 0));
                target.X += comboBox.ActualWidth / 2;
                target.Y += comboBox.ActualHeight / 2;
                TcpSerialListener.SetCursorPos((int)target.X, (int)target.Y);
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
                GuiModel.DoEvents();
            }

            comboBoxItem.Dispatcher.Invoke(() =>
            {
                Point target = comboBoxItem.PointToScreen(new Point(0d, 0d));
                target.X += comboBoxItem.ActualWidth / 2;
                target.Y += comboBoxItem.ActualHeight / 2;
                TcpSerialListener.SetCursorPos((int)target.X, (int)target.Y);
            });
        }

        private void CenterMouseOverComboBoxItem(Point p, ComboBoxItem c)
        {
            p.X += c.ActualWidth / 2;
            p.Y += c.ActualHeight / 2;
            TcpSerialListener.SetCursorPos((int)p.X, (int)p.Y);
        }

        private void PrintGrid()
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
        }

    }
}
