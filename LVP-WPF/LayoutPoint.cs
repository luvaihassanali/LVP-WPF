using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public (int x, int y) up = (-1, 0);
        public (int x, int y) down = (1, 0);
        public (int x, int y) left = (0, -1);
        public (int x, int y) right = (0, 1);

        public LayoutPoint(GuiModel g)
        {
            gui = g;
            //BuildMainGrid(false);
            //BuildMainGrid(true);
        }
    }
}
