using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

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
        public (int x, int y) currPoint = (0, 0);
        public (int x, int y) returnPointA = (0, 0);
        public (int x, int y) returnPointB = (0, 0);

        public object currControl = null;
        public List<int[]> mainWindowGrid = new List<int[]>();
        public List<Image[]> mainWindowControlGrid = new List<Image[]>();
        public Image movieBackdrop = null;

        public int tvIndex = 0;
        public int seasonIndex = 0;
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
            if (seasonWindowActive)
            {
                MoveSeasonPoint((pos.x, pos.y));
            }
            else if (tvShowWindowActive)
            {
                MoveTvPoint(pos.x);
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
                mainWindowActive = false;
                returnPointA = currPoint;
                if (isMovie)
                {
                    movieWindowActive = true;
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
                return;
            }

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
                    PrintGrid(); PrintControlGrid();
                    CenterMouseOverControl(currControl);
                }
                if (controlName.Equals("PlayerWindow")) { playerWindowActive = true; }
            }
            else if (movieWindowActive)
            {
                returnPointB = currPoint;
                if (controlName.Equals("PlayerWindow")) { playerWindowActive = true; }
            }
        }

        internal async void CloseCurrWindow()
        {
            try
            {
                if (mainWindowActive)
                {
                    // To-do: scrolling ?
                    CenterMouseOverControl(gui.mainCloseButton);
                    TcpSerialListener.DoMouseClick();
                }

                if (playerWindowActive)
                {
                    playerWindowActive = false;
                    //gui.playerWindow.overlayGrid.Visibility = Visibility.Visible;
                    CenterMouseOverControl(gui.playerCloseButton);
                    TcpSerialListener.DoMouseClick();
                    await Task.Delay(500);

                    if (movieWindowActive)
                    {
                        movieWindowActive = false;
                        mainWindowActive = true;
                        currPoint = returnPointA;
                        currControl = mainWindowControlGrid[currPoint.x][currPoint.y];
                        CenterMouseOverControl(currControl);
                    }
                    else if (tvShowWindowActive)
                    {
                        currPoint = returnPointB;
                        currControl = tvControlList[currPoint.x];
                        CenterMouseOverControl(currControl);
                    }
                    return;
                }

                if (seasonWindowActive)
                {
                    TcpSerialListener.DoMouseClick();
                    seasonWindowActive = false;
                    seasonControlList.Clear();
                    seasonWindowGrid.Clear();
                    seasonWindowControlGrid.Clear();
                    currPoint = returnPointB;
                    currControl = tvControlList[currPoint.x];
                    CenterMouseOverControl(currControl);
                    return;
                } 
                else if (tvShowWindowActive)
                {
                    tvControlList.Clear();
                    tvIndex = 0;
                    tvShowWindowActive = false;
                    mainWindowActive = true;

                    /*tvWindowMainPanel.Invoke(new MethodInvoker(delegate
                    {
                        tvWindowMainPanel.AutoScrollPosition = new Point(0, 0);
                        AdjustScrollBar();
                        tvWindowClose.Visible = true;
                    }));*/

                    CenterMouseOverControl(gui.tvMovieCloseButton);
                    TcpSerialListener.DoMouseClick();
                    currPoint = returnPointA;
                    currControl = mainWindowControlGrid[currPoint.x][currPoint.y];
                    CenterMouseOverControl(currControl);
                }
                else if (movieWindowActive)
                {
                    movieWindowActive = false;
                    movieWindowActive = true;
                    CenterMouseOverControl(gui.tvMovieCloseButton);
                    TcpSerialListener.DoMouseClick();
                    currPoint = returnPointA;
                    currControl = mainWindowControlGrid[currPoint.x][currPoint.y];
                    CenterMouseOverControl(currControl);
                }
            }
            catch (Exception ex)
            {
                GuiModel.Log(ex.Message);
            }
        }

        private void MoveTvPoint(int x)
        {
            int newIndex = currPoint.x + x;
            if (newIndex < 0 || newIndex >= tvControlList.Count) return;

            currPoint = (newIndex, currPoint.y);
            currControl = tvControlList[newIndex];
            CenterMouseOverControl(currControl, newIndex, MainWindow.gui.episodeScrollViewer);
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
                if (low >= 0)
                {
                    if (seasonWindowControlGrid[nextPoint.x][low] != null) return (nextPoint.x, low);
                }

                if (high < 3)
                {
                    if (seasonWindowControlGrid[nextPoint.x][high] != null) return (nextPoint.x, high);
                }
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
            PrintGrid();
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
                if (i == totalTvShows - gui.Cartoons.Count)
                {
                    count = 6;
                }

                if (count == 6)
                {
                    rowIndex++;
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
            if (control as Button != null)
            {
                Button button = (Button)control;
                CenterMouseOverButton(button);
            }
            else if (control as Image != null)
            {
                Image image = (Image)control;
                CenterMouseOverImage(image, row, scrollViewer);
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
                    else if (row == mainWindowGrid.Count - 1)
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

        private void PrintGrid()
        {
            List<int[]> ctrl = seasonWindowActive ? seasonWindowGrid : mainWindowGrid;
            foreach (int[] row in ctrl)
            {
                Trace.Write("[ ");
                for (int i = 0; i < row.Length; i++)
                {
                    Trace.Write(row[i]);
                    if (i != row.Length - 1)
                    {
                        Trace.Write(", ");
                    }
                }
                Trace.WriteLine(" ]");
            }
            Trace.WriteLine(Environment.NewLine);
        }

        private void PrintControlGrid()
        {
            List<Image[]> ctrl = seasonWindowActive ? seasonWindowControlGrid : mainWindowControlGrid;
            foreach (Image[] row in ctrl)
            {
                Trace.Write("[ ");
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
                    Trace.Write(itemName);

                    if (i != row.Length - 1)
                    {
                        Trace.Write(", ");
                    }
                }
                Trace.WriteLine(" ]");
            }
            Trace.WriteLine(Environment.NewLine);
        }

    }
}
