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
    internal class LayoutPoint
    {
        private GuiModel gui;
        public bool mainWindowActive = true;
        private bool movieWindowActive = false;
        private bool tvShowWindowActive = false;
        private bool seasonWindowActive = false;
        private bool playerWindowActive = false;
        private (int x, int y) currentPoint = (0, 0);
        private (int x, int y) returnPointA = (0, 0);
        private (int x, int y) returnPointB = (0, 0);

        private List<int[]> mainWindowGrid = new List<int[]>();
        private List<Image[]> mainWindowControlGrid = new List<Image[]>();
        private object currentMainWindowControl = null;

        public (int x, int y) up = (-1, 0);
        public (int x, int y) down = (1, 0);
        public (int x, int y) left = (0, -1);
        public (int x, int y) right = (0, 1);

        public LayoutPoint(GuiModel g)
        {
            gui = g;
            BuildMainWindowGrid();
            PrintGrid();
            TcpSerialListener.SetCursorPos(20, 20);
            TcpSerialListener.DoMouseClick();
            currentMainWindowControl = mainWindowControlGrid.Count != 0 ? mainWindowControlGrid[0][0] : gui.CloseButtons[0];
            CenterMouseOverControl(currentMainWindowControl);
        }

        private void CenterMouseOverControl(object control)
        {
            if (control as Button != null)
            {
                Button button = (Button)control;
                CenterMouseOverButton(button);
            }
            else
            {
                Image image = (Image)control;
                CenterMouseOverImage(image);
            }
            
        }

        private void CenterMouseOverImage(Image image)
        {
            Point target = image.PointToScreen(new Point(0, 0));
            target.X += image.Width / 2;
            target.Y += image.Height / 2;
            TcpSerialListener.SetCursorPos((int)target.X, (int)target.Y);
        }

        private void CenterMouseOverButton(Button button)
        {
            Point target = button.PointToScreen(new Point(0, 0));
            TcpSerialListener.SetCursorPos((int)target.X, (int)target.Y);
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
            ListView[] mainWindowLists = new ListView[]{ (ListView)gui.MainGrid.Children[6],
                                                         (ListView)gui.MainGrid.Children[2],
                                                         (ListView)gui.MainGrid.Children[4] };

            for (int i = 0; i < 3; i++)
            {
                ItemContainerGenerator generator = mainWindowLists[i].ItemContainerGenerator;
                switch(i)
                {
                    case 0:
                        for (int j = 0; j < gui.Movies.Count; j++)
                        {
                            ListViewItem container = (ListViewItem)generator.ContainerFromItem(gui.Movies[j]);
                            Image img = GetChildrenByType(container, typeof(Image), "mainGridImage") as Image;
                            mainWindowControlList.Add(img);
                        }
                        break;
                    case 1:
                        for (int j = 0; j < gui.TvShows.Count; j++)
                        {
                            ListViewItem container = (ListViewItem)generator.ContainerFromItem(gui.TvShows[j]);
                            Image img = GetChildrenByType(container, typeof(Image), "mainGridImage") as Image;
                            mainWindowControlList.Add(img);
                        }
                        break;
                    case 2:
                        for (int j = 0; j < gui.Cartoons.Count; j++)
                        {
                            ListViewItem container = (ListViewItem)generator.ContainerFromItem(gui.Cartoons[j]);
                            Image img = GetChildrenByType(container, typeof(Image), "mainGridImage") as Image;
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

        private void PrintGrid()
        {
            foreach (int[] row in mainWindowGrid)
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
            PrintControlGrid();
        }

        private void PrintControlGrid()
        {
            foreach (Image[] row in mainWindowControlGrid)
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

        // https://stackoverflow.com/questions/37247724/find-controls-placed-inside-listview-wpf
        public static Visual GetChildrenByType(Visual visualElement, Type typeElement, string nameElement)
        {
            if (visualElement == null) return null;
            if (visualElement.GetType() == typeElement)
            {
                FrameworkElement fe = visualElement as FrameworkElement;
                if (fe != null)
                {
                    if (fe.Name == nameElement)
                    {
                        return fe;
                    }
                }
            }
            Visual foundElement = null;
            if (visualElement is FrameworkElement)
                (visualElement as FrameworkElement).ApplyTemplate();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(visualElement); i++)
            {
                Visual visual = VisualTreeHelper.GetChild(visualElement, i) as Visual;
                foundElement = GetChildrenByType(visual, typeElement, nameElement);
                if (foundElement != null)
                    break;
            }
            return foundElement;
        }
    }
}
