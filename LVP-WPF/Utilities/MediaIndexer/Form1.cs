using System.Diagnostics;

namespace MediaIndexUtil
{
    public partial class Form1 : Form
    {
        private List<string[]> indexRenameList = new List<string[]>();
        private TreeNode rootNode = null;
        private string currentFolder = string.Empty;

        public Form1()
        {
            InitializeComponent();
            PopulateTreeView("..\\");
            TreeView1_NodeMouseClick(null, null);
        }

        private void PopulateTreeView(string path)
        {
            DirectoryInfo info = new DirectoryInfo(path);
            if (info.Exists)
            {
                rootNode = new TreeNode(info.Name);
                rootNode.Tag = info;
                GetDirectories(info.GetDirectories(), rootNode);
                treeView1.Nodes.Add(rootNode);
            }
        }

        private void GetDirectories(DirectoryInfo[] subDirs, TreeNode nodeToAddTo)
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

        void TreeView1_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs? e)
        {
            TreeNode newSelected;
            if (sender == null)
            {
                newSelected = treeView1.Nodes[0];
            }
            else
            {
                if (sender as TreeNode != null)
                {
                    newSelected = (TreeNode)sender;
                }
                else
                {
                    newSelected = e.Node;
                }
            }
            listView1.Items.Clear();

            DirectoryInfo nodeDirInfo = (DirectoryInfo)newSelected.Tag;
            currentFolder = nodeDirInfo.FullName;
            DirectoryInfo[] directories = nodeDirInfo.GetDirectories();

            Array.Sort(directories, delegate (DirectoryInfo d1, DirectoryInfo d2)
            {
                return d1.Name.CompareTo(d2.Name);
            });

            ListViewItem.ListViewSubItem[] subItems;
            ListViewItem item;
            foreach (DirectoryInfo dir in directories)
            {
                if (dir.Name.Contains("MediaIndexer")) continue;
                item = new ListViewItem(dir.Name, 0);
                subItems = new ListViewItem.ListViewSubItem[] { new ListViewItem.ListViewSubItem(item, "Directory"), new ListViewItem.ListViewSubItem(item, dir.FullName) };
                item.SubItems.AddRange(subItems);
                listView1.Items.Add(item);
            }

            FileInfo[] files = nodeDirInfo.GetFiles();
            Array.Sort(files, delegate (FileInfo f1, FileInfo f2)
            {
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

        private void ListView1_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (listView1.SelectedItems.Count == 0) return;
            ListViewItem item = listView1.SelectedItems[0];
            if (item.SubItems[1].Text == "Directory") return;

            string fullPath = item.SubItems[2].Text;
            string[] pathComponents = fullPath.Split("\\");
            string fileName = pathComponents[pathComponents.Length - 1];
            string[] fileComponents = fileName.Split('%');
            indexRenameList.Add(new string[] { fullPath, fileComponents[1].Trim(), fileName });

            ListViewItem newItem;
            ListViewItem.ListViewSubItem[] subItems;
            newItem = new ListViewItem(fileComponents[0], 1);
            subItems = new ListViewItem.ListViewSubItem[] { new ListViewItem.ListViewSubItem(newItem, fileComponents[1]) };
            newItem.SubItems.AddRange(subItems);
            listView2.Items.Add(newItem);
        }

        private void ListView2_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (listView2.SelectedItems.Count == 0) return;
            string name = e.Item.SubItems[1].Text.Trim();
            listView2.Items.Remove(e.Item);

            foreach (string[] entry in indexRenameList)
            {
                if (entry[1].Equals(name))
                {
                    indexRenameList.Remove(entry);
                    break;
                }
            }
        }

        private void RenameButton_Click(object sender, EventArgs e)
        {
            string targetIndex = textBox1.Text;
            TreeNode prevNode = treeView1.SelectedNode;
            RenameFiles(indexRenameList, targetIndex);
            listView2.Items.Clear();
            textBox1.Clear();
            TreeView1_NodeMouseClick(prevNode, null);
        }

        private void RenameFiles(List<string[]> list, string targetIndex)
        {
            Cursor.Current = Cursors.WaitCursor;
            if (checkBox1.Checked)
            {
                RenameSrtFiles();
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                string fullPath = list[i][0];
                string shortName = list[i][1];
                string longName = list[i][2];
                if (Int32.Parse(targetIndex) < 10) targetIndex = "0" + targetIndex;
                string newName = targetIndex + " % " + shortName;
                string newPath = fullPath.Replace(longName, newName);
                File.Move(fullPath, newPath);
                int newIndex = int.Parse(targetIndex);
                newIndex++;
                targetIndex = newIndex.ToString();
            }
            list.Clear();
            Cursor.Current = Cursors.Arrow;
        }

        private void RenameSrtFiles()
        {
            Cursor.Current = Cursors.WaitCursor;
            string[] files = Directory.GetFiles(currentFolder);
            Array.Sort(files);
            int lineNum = 0;
            string log = "SRT Rename for directory: " + currentFolder + "\n";
            Trace.WriteLine("SRT Rename for directory: " + currentFolder);
            for (int i = 0; i < files.Length - 1; i+=2)
            {
                string f1;
                string f2;
                string files_i;
                string files_i_1;

                if (files[i].EndsWith("srt"))
                {
                    files_i = files[i];
                    files_i_1 = files[i + 1];
                    f1 = files_i.Split(".")[0];
                    f2 = files_i_1.Split(".")[0];
                }
                else
                {
                    files_i = files[i + 1];
                    files_i_1 = files[i];
                    f1 = files_i.Split(".")[0];
                    f2 = files_i_1.Split(".")[0];
                }

                if (f1.Equals(f2))
                {
                    Trace.WriteLine(lineNum++.ToString() + " " + files_i + " " + files_i_1 + " ✓");
                    log += lineNum++.ToString() + " " + files_i + " " + files_i_1 + " ✓\n";
                }
                else
                {
                    try
                    {
                        string[] f1Parts = f1.Split("\\");
                        string[] f2Parts = f2.Split("\\");
                        string prevName = f1Parts[f1Parts.Length - 1];
                        string newName = f2Parts[f2Parts.Length - 1];
                        f1Parts[f1Parts.Length - 1] = newName;
                        string f2Join = String.Join("\\", f1Parts);
                        f2Join += ".srt";
                        File.Move(files_i, f2Join);
                        Trace.WriteLine(lineNum++.ToString() + " " + files_i + " " + files_i_1 + " ✓");
                        Trace.WriteLine("    Renamed " + prevName + " to " + newName);
                        log += lineNum++.ToString() + " " + files_i + " " + files_i_1 + " ✓\n";
                        log += "    Renamed " + prevName + " to " + newName + "\n";
                    }
                    catch
                    {
                        Trace.WriteLine(lineNum++.ToString() + " " + files_i + " " + files_i_1 + " ✕");
                        log += lineNum++.ToString() + " " + files_i + " " + files_i_1 + " ✕\n";
                    }
                }
            }

            Trace.WriteLine("Finished");
            log += "Finished";
            File.WriteAllText("log.txt", log);
            Process.Start("notepad.exe", "log.txt");
            Cursor.Current = Cursors.Arrow;
        }

        private void CompareButton_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    CompareDirectories(currentFolder, fbd.SelectedPath);
                }
            }
        }

        private void CompareDirectories(string compareDir1, string compareDir2)
        {
            Cursor.Current = Cursors.WaitCursor;
            string log = "Compare for directory: " + compareDir1 + " and " + compareDir2 + "\n";
            Trace.WriteLine("Compare for directory: " + compareDir1 + " and " + compareDir2);

            string[] showDir1 = Directory.GetDirectories(compareDir1);
            string[] showDir2 = Directory.GetDirectories(compareDir2);

            if (showDir1.Length != showDir2.Length)
            {
                Trace.WriteLine("Warning: Number of seasons not the same");
                log += "Warning: Number of seasons not the same\n";
            }

            if (showDir2.Length > showDir1.Length)
            {
                string[] temp = showDir1;
                showDir1 = showDir2;
                showDir2 = temp;
            }

            // Assume folder will be named "Season " so MediaIndexer folder always at index 0
            // Except if Extras then i = 2
            int startIndex = 1;
            // Assume never will be extras in 2nd lang folder
            if (showDir1.Contains("Extras")) {
                startIndex = 2;
            }

            int seasonIndex = 1;
            for (int i = startIndex; i < showDir1.Length; i++)
            {
                if (CompareFiles(showDir1[i], showDir2[i]))
                {
                    Trace.WriteLine("Season " + seasonIndex + " ✓" + " " + showDir1[i] + " and " + showDir2[i]);
                    log += "Season " + seasonIndex + " ✓" + " " + showDir1[i] + " and " + showDir2[i] + "\n";
                }
                else
                {
                    Trace.WriteLine("Season " + seasonIndex + " ✕" + " " + showDir1[i] + " and " + showDir2[i]);
                    log += "Season " + seasonIndex + " ✕" + " " + showDir1[i] + " and " + showDir2[i] + "\n";
                }
                seasonIndex++;
            }

            Trace.WriteLine("Finished");
            log += "Finished";
            File.WriteAllText("log.txt", log);
            Process.Start("notepad.exe", "log.txt");
            Cursor.Current = Cursors.Arrow;
        }

        private bool CompareFiles(string d1, string d2)
        {
            var fileList1 = Directory.GetFiles(d1).Where(name => !name.EndsWith(".srt"));
            var fileList2 = Directory.GetFiles(d2).Where(name => !name.EndsWith(".srt"));
            return fileList1.Count() == fileList2.Count();
        }

        private void TreeButton_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            if (currentFolder.EndsWith("\\")) currentFolder = currentFolder.Substring(0, currentFolder.Length - 1);
            string[] directoryParts = currentFolder.Split("\\");
            DirectoryInfo treeFolder = Directory.CreateDirectory(directoryParts[directoryParts.Length - 1]);
            CreateTree(currentFolder, treeFolder);
            Cursor.Current = Cursors.Arrow;
        }

        private void CreateTree(string currentDirectory, DirectoryInfo treeFolder)
        {
            string[] directories = Directory.GetDirectories(currentDirectory);
            for (int i = 0; i < directories.Length; i++)
            {
                string[] directoryNameParts = directories[i].Split("\\");
                string directoryName = directoryNameParts[directoryNameParts.Length - 1];
                DirectoryInfo treeSubFolder = treeFolder.CreateSubdirectory(directoryName);

                string[] files = Directory.GetFiles(directories[i]);
                for (int j = 0; j < files.Length; j++)
                {
                    string[] filenameParts= files[j].Split("\\");
                    string filename = filenameParts[filenameParts.Length - 1].Split(".")[0];
                    File.WriteAllText(treeSubFolder.FullName + "\\" + filename + ".txt", "blank");
                }
            }
        }

        private void OpenFolderButton_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.ShowDialog();
            string path = folderBrowserDialog1.SelectedPath;
            listView1.Items.Clear();
            listView2.Items.Clear();
            treeView1.Nodes.Clear();
            PopulateTreeView(path);
            TreeView1_NodeMouseClick(null, null);
        }
    }
}