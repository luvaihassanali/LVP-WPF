namespace MediaIndexUtil
{
	//To-do: show changed index
	//To-do: Undo
    public partial class Form1 : Form
    {
        List<string[]> list = new List<string[]>();

        public Form1()
        {
            InitializeComponent();
            PopulateTreeView();
            treeView1_NodeMouseClick(null, null);
        }

        private void PopulateTreeView()
        {
            TreeNode rootNode;

            DirectoryInfo info = new DirectoryInfo(@"../");
            if (info.Exists)
            {
                rootNode = new TreeNode(info.Name);
                rootNode.Tag = info;
                GetDirectories(info.GetDirectories(), rootNode);
                treeView1.Nodes.Add(rootNode);
            }
        }

        private void GetDirectories(DirectoryInfo[] subDirs,
            TreeNode nodeToAddTo)
        {
            TreeNode aNode;
            DirectoryInfo[] subSubDirs;
            foreach (DirectoryInfo subDir in subDirs)
            {
                aNode = new TreeNode(subDir.Name, 0, 0);
                aNode.Tag = subDir;
                aNode.ImageKey = "folder";
                subSubDirs = subDir.GetDirectories();
                if (subSubDirs.Length != 0)
                {
                    GetDirectories(subSubDirs, aNode);
                }
                nodeToAddTo.Nodes.Add(aNode);
            }
        }

        void treeView1_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs? e)
        {
            TreeNode newSelected;
            if (sender == null)
            {
                newSelected = treeView1.Nodes[0];
            }
            else
            {
                newSelected = e.Node;
            }
            listView1.Items.Clear();

            DirectoryInfo nodeDirInfo = (DirectoryInfo)newSelected.Tag;
            ListViewItem.ListViewSubItem[] subItems;
            ListViewItem item;

            DirectoryInfo[] dirs = nodeDirInfo.GetDirectories();
            Array.Sort(dirs, delegate (DirectoryInfo d1, DirectoryInfo d2) {
                return d1.Name.CompareTo(d2.Name);
            });
            foreach (DirectoryInfo dir in dirs)
            {
                item = new ListViewItem(dir.Name, 0);
                subItems = new ListViewItem.ListViewSubItem[] { new ListViewItem.ListViewSubItem(item, "Directory"), new ListViewItem.ListViewSubItem(item, dir.FullName) };
                item.SubItems.AddRange(subItems);
                listView1.Items.Add(item);
            }
            FileInfo[] files = nodeDirInfo.GetFiles();
            Array.Sort(files, delegate (FileInfo f1, FileInfo f2) {
                return f1.Name.CompareTo(f2.Name);
            });
            foreach (FileInfo file in files)
            {
                item = new ListViewItem(file.Name, 1);
                subItems = new ListViewItem.ListViewSubItem[] { new ListViewItem.ListViewSubItem(item, "File"), new ListViewItem.ListViewSubItem(item, file.FullName) };
                item.SubItems.AddRange(subItems);
                listView1.Items.Add(item);
            }

            listView1.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        private void listView1_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (listView1.SelectedItems.Count == 0)
                return;

            ListViewItem item = listView1.SelectedItems[0];

            if (item.SubItems[1].Text == "Directory")
                return;

            string[] entry = new string[2];
            string fullPath = item.SubItems[2].Text;
            string[] pathComponents = fullPath.Split("\\");
            string fileName = pathComponents[pathComponents.Length - 1];
            string[] fileComponents = fileName.Split('%');

            list.Add(new string[] { fullPath, fileComponents[1].Trim(), fileName });

            ListViewItem newItem;
            ListViewItem.ListViewSubItem[] subItems;
            newItem = new ListViewItem(fileComponents[0], 1);
            subItems = new ListViewItem.ListViewSubItem[] { new ListViewItem.ListViewSubItem(newItem, fileComponents[1]) };
            newItem.SubItems.AddRange(subItems);
            listView2.Items.Add(newItem);
        }

        private void listView2_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (listView2.SelectedItems.Count == 0)
                return;

            string name = e.Item.SubItems[1].Text.Trim();
            listView2.Items.Remove(e.Item);
            foreach(string[] entry in list)
            {
                if(entry[1].Equals(name))
                {
                    list.Remove(entry);
                    break;
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string targetIndex = textBox1.Text;

            RenameFiles(list, targetIndex);

            listView1.Items.Clear();
            treeView1.Nodes.Clear();
            PopulateTreeView();
            listView2.Items.Clear();
            textBox1.Clear();
            treeView1_NodeMouseClick(null, null);

        }

        private void RenameFiles(List<string[]> list, string targetIndex)
        {
            for (int i = 0; i < list.Count; i++)
            {
                string fullPath = list[i][0];
                string shortName = list[i][1];
                string longName = list[i][2];
                string newName = targetIndex + " % " + shortName;
                string newPath = fullPath.Replace(longName, newName);
                File.Move(fullPath, newPath);
                int newIndex = int.Parse(targetIndex);
                newIndex++;
                targetIndex = newIndex.ToString(); 
            }

            list.Clear();
        }
    }
}