using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Remote
{
    public partial class Form1 : Form
    {
        Samsung remote;
        public Form1()
        {
            InitializeComponent();
            //WolUtilities.SendTenMagicPackets();
            remote = new Samsung();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }
    }
}