using System.Diagnostics;

namespace MediaIndexUtil
{
    public partial class Form1 : Form
    {
        private List<string[]> indexRenameList = new List<string[]>();
        private TreeNode rootNode = null;
        private string currentFolder = String.Empty;
        private string log = String.Empty;

        public Form1()
        {
            string path = MediaHub.Properties.Settings.Default.path;
            if (path.Equals(String.Empty) || !Directory.Exists(path)) path = "..\\.";
            InitializeComponent();
            PopulateTreeView(path);
            TreeView1_NodeMouseClick(null, null);
            treeView1.ExpandAll();
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
                if (dir.Name.Contains("MediaHub")) continue;
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
            //listView1.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
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
                if (checkBox2.Checked || checkBox3.Checked) { MessageBox.Show("Choose only 1"); }
                RenameSrtFiles();
                return;
            }

            if (checkBox2.Checked)
            {
                if (checkBox3.Checked || checkBox1.Checked) { MessageBox.Show("Choose only 1"); }
                RenameLangFiles();
                return;
            }

            if (checkBox3.Checked)
            {
                if (checkBox2.Checked || checkBox1.Checked) { MessageBox.Show("Choose only 1"); }
                RenameCopyFiles();
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

        private void RenameLangFiles()
        {
            Cursor.Current = Cursors.WaitCursor;
            string targetFolder = currentFolder.Replace("\\en\\", "\\it\\");
            string[] files = Directory.GetFiles(currentFolder).Where(name => !name.EndsWith(".srt")).ToArray();
            Array.Sort(files);
            string[] targetFiles = Directory.GetFiles(targetFolder).Where(name => !name.EndsWith(".srt")).ToArray();
            Array.Sort(targetFiles);
            string log = "LANG Rename for directory: " + currentFolder + " and " + targetFolder + "\n";
            Trace.WriteLine("LANG Rename for directory: " + currentFolder + " and " + targetFolder);

            int lineNum = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string[] f1Parts = files[i].Split("\\");
                string[] f2Parts = targetFiles[i].Split("\\");
                string prevName = f2Parts[f2Parts.Length - 1];
                string newName = f1Parts[f1Parts.Length - 1];

                if (newName.Equals(prevName))
                {
                    Trace.WriteLine(lineNum++.ToString() + " " + newName + " " + prevName + " ✓");
                    log += lineNum.ToString() + " " + newName + " " + prevName + " ✓\n";
                }
                else
                {
                    string f2 = targetFiles[i];
                    string f1 = files[i];
                    try
                    {
                        f2Parts[f2Parts.Length - 1] = newName;
                        string f2Join = String.Join("\\", f2Parts);
                        File.Move(f2, f2Join);
                        Trace.WriteLine(lineNum++.ToString() + " Renamed " + prevName + " to " + newName + " ✓");
                        log += lineNum.ToString() + " Renamed " + prevName + " to " + newName + " ✓\n";
                    }
                    catch
                    {
                        Trace.WriteLine(lineNum++.ToString() + " Error" + prevName + " " + newName + " ✕");
                        log += lineNum.ToString() + " Error" + prevName + " " + newName + " ✕\n";
                    }
                }
            }

            Trace.WriteLine("Finished");
            log += "Finished";
            File.WriteAllText("log.txt", log);
            Process.Start("notepad.exe", "log.txt");
            Cursor.Current = Cursors.Arrow;
        }

        private void RenameCopyFiles()
        {
            Cursor.Current = Cursors.WaitCursor;
            string targetFolder = currentFolder.Replace("\\en\\", "\\it\\");

            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    targetFolder = fbd.SelectedPath;
                }
            }

            string[] files = Directory.GetFiles(currentFolder).Where(name => !name.EndsWith(".srt")).ToArray();
            Array.Sort(files);
            string[] targetFiles = Directory.GetFiles(targetFolder).Where(name => !name.EndsWith(".srt")).ToArray();
            Array.Sort(targetFiles);
            string log = "COPY Rename for directory: " + currentFolder + " and " + targetFolder + "\n";
            Trace.WriteLine("COPY Rename for directory: " + currentFolder + " and " + targetFolder);

            int lineNum = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string[] f1Parts = files[i].Split("\\");
                string[] f2Parts = targetFiles[i].Split("\\");
                string prevName = f2Parts[f2Parts.Length - 1];
                string newName = f1Parts[f1Parts.Length - 1];

                if (newName.Equals(prevName))
                {
                    Trace.WriteLine(lineNum++.ToString() + " " + newName + " " + prevName + " ✓");
                    log += lineNum.ToString() + " " + newName + " " + prevName + " ✓\n";
                }
                else
                {
                    string f2 = targetFiles[i];
                    string f1 = files[i];
                    try
                    {
                        f2Parts[f2Parts.Length - 1] = newName;
                        string f2Join = String.Join("\\", f2Parts);
                        File.Move(f2, f2Join);
                        Trace.WriteLine(lineNum++.ToString() + " Renamed " + prevName + " to " + newName + " ✓");
                        log += lineNum.ToString() + " Renamed " + prevName + " to " + newName + " ✓\n";
                    }
                    catch
                    {
                        Trace.WriteLine(lineNum++.ToString() + " Error" + prevName + " " + newName + " ✕");
                        log += lineNum.ToString() + " Error" + prevName + " " + newName + " ✕\n";
                    }
                }
            }

            Trace.WriteLine("Finished");
            log += "Finished";
            File.WriteAllText("log.txt", log);
            Process.Start("notepad.exe", "log.txt");
            Cursor.Current = Cursors.Arrow;
        }

        private void RenameSrtFiles()
        {
            Cursor.Current = Cursors.WaitCursor;
            string[] files = Directory.GetFiles(currentFolder);
            Array.Sort(files);
            string log = "SRT Rename for directory: " + currentFolder + "\n";
            Trace.WriteLine("SRT Rename for directory: " + currentFolder);

            int lineNum = 0;
            for (int i = 0; i < files.Length - 1; i += 2)
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
                    log += lineNum.ToString() + " " + files_i + " " + files_i_1 + " ✓\n";
                }
                else
                {
                    string[] f1Parts = f1.Split("\\");
                    string[] f2Parts = f2.Split("\\");
                    string prevName = f1Parts[f1Parts.Length - 1];
                    string newName = f2Parts[f2Parts.Length - 1];
                    try
                    {
                        ;
                        f1Parts[f1Parts.Length - 1] = newName;
                        string f2Join = String.Join("\\", f1Parts);
                        f2Join += ".srt";
                        File.Move(files_i, f2Join);
                        Trace.WriteLine(lineNum++.ToString() + " " + prevName + " " + newName + " ✓");
                        log += lineNum.ToString() + " " + prevName + " " + newName + " ✓\n";
                    }
                    catch
                    {
                        Trace.WriteLine(lineNum++.ToString() + " " + prevName + " " + newName + " ✕");
                        log += lineNum.ToString() + " " + prevName + " " + newName + " ✕\n";
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
            log = "Compare for directory: " + compareDir1 + " and " + compareDir2 + "\n";
            Trace.WriteLine("Compare for directory: " + compareDir1 + " and " + compareDir2);

            string[] showDir1 = Directory.GetDirectories(compareDir1);
            string[] showDir2 = Directory.GetDirectories(compareDir2);

            if (showDir1.Length != showDir2.Length)
            {
                Trace.WriteLine("Warning: Number of seasons not the same");
                log += "Warning: Number of seasons not the same\n";
            }

            foreach (string path in showDir1)
            {
                if (path.Contains("Extras"))
                {
                    List<string> temp = new List<string>(showDir1);
                    temp.Remove(path);
                    showDir1 = temp.ToArray();
                    break;
                }
            }

            foreach (string path in showDir2)
            {
                if (path.Contains("Extras"))
                {
                    List<string> temp = new List<string>(showDir2);
                    temp.Remove(path);
                    showDir2 = temp.ToArray();
                    break;
                }
            }

            Array.Sort(showDir1, SeasonComparer);
            Array.Sort(showDir2, SeasonComparer);

            int seasonIndex = 1;
            for (int i = 0; i < showDir1.Length; i++)
            {
                if (CompareFiles(showDir1[i], showDir2[i], ref log))
                {
                    Trace.WriteLine("Season " + seasonIndex + " ✓" + " " + showDir1[i] + " and " + showDir2[i]);
                    log += "Season " + seasonIndex + " ✓" + " " + showDir1[i] + " and " + showDir2[i] + "\n\n";
                }
                else
                {
                    Trace.WriteLine("Season " + seasonIndex + " ✕" + " " + showDir1[i] + " and " + showDir2[i]);
                    log += "Season " + seasonIndex + " ✕" + " " + showDir1[i] + " and " + showDir2[i] + "\n\n";
                }
                seasonIndex++;
            }

            Trace.WriteLine("Finished");
            log += "Finished";
            File.WriteAllText("log.txt", log);
            Process.Start("notepad.exe", "log.txt");
            Cursor.Current = Cursors.Arrow;
        }

        private int SeasonComparer(string seasonB, string seasonA)
        {
            if (seasonB.Contains("Extras"))
            {
                return -1;
            }
            else if (seasonA.Contains("Extras"))
            {
                return 1;
            }
            string[] seasonValuePathA = seasonA.Split();
            string[] seasonValuePathB = seasonB.Split();
            int seasonValueA = Int32.Parse(seasonValuePathA[seasonValuePathA.Length - 1]);
            int seasonValueB = Int32.Parse(seasonValuePathB[seasonValuePathB.Length - 1]);
            if (seasonValueA == seasonValueB) return 0;
            if (seasonValueA < seasonValueB) return 1;
            return -1;
        }

        private bool CompareFiles(string d1, string d2, ref string log)
        {
            string[] fileList1 = Directory.GetFiles(d1).Where(name => !name.EndsWith(".srt")).ToArray();
            string[] fileList2 = Directory.GetFiles(d2).Where(name => !name.EndsWith(".srt")).ToArray();
            Array.Sort(fileList1, CompareIndex);
            Array.Sort(fileList2, CompareIndex);

            var min = fileList1.Length < fileList2.Length ? fileList1.Length : fileList2.Length;
            for (int i = 0; i < min; i++)
            {
                string[] n1Parts = fileList1[i].Split("\\");
                string n1 = n1Parts[n1Parts.Length - 1];
                n1 = n1.Split('.')[0];
                string[] n2Parts = fileList2[i].Split("\\");
                string n2 = n2Parts[n2Parts.Length - 1];
                n2 = n2.Split('.')[0];
                if (!n1.Trim().Equals(n2.Trim(), StringComparison.InvariantCultureIgnoreCase))
                {
                    log += "Not a match: " + n1 + " and " + n2 + "\n";
                    Trace.WriteLine("Not a match: " + n1 + " and " + n2);
                }
            }
            log += fileList1.Length + " " + fileList2.Length + "\n";
            Trace.WriteLine(fileList1.Length + " " + fileList2.Length);
            return fileList1.Length == fileList2.Length;
        }

        private int CompareIndex(string s1, string s2)
        {
            string[] s1Parts = s1.Split('%');
            string[] s2Parts = s2.Split('%');
            string[] s3Parts = s1Parts[s1Parts.Length - 2].Split('\\');
            string[] s4Parts = s2Parts[s2Parts.Length - 2].Split('\\');

            string s5Part = s3Parts[s3Parts.Length - 1];
            string s6Part = s4Parts[s4Parts.Length - 1];
            if (s5Part.Contains("#")) s5Part = s5Part.Split('#')[0];
            if (s6Part.Contains("#")) s6Part = s6Part.Split('#')[0];

            int indexA = -1;
            int indexB = -2;
            try
            {
                indexA = Int32.Parse(s5Part);
                indexB = Int32.Parse(s6Part);
            }
            catch (Exception ex)
            {
                MessageBox.Show(s1 + " " + s2 + " " + ex.Message);
            }
            if (indexA == indexB)
            {
                return 0;
            }
            else if (indexA > indexB)
            {
                return 1;
            }
            else
            {
                return -1;
            }
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
                    string[] filenameParts = files[j].Split("\\");
                    filenameParts = filenameParts[filenameParts.Length - 1].Split(".");
                    string filename = filenameParts[0];
                    string ext = filenameParts[1];
                    if (ext.Equals("srt"))
                    {
                        File.WriteAllText(treeSubFolder.FullName + "\\" + filename + ".srt", "blank");
                    }
                    else
                    {
                        File.WriteAllText(treeSubFolder.FullName + "\\" + filename + ".txt", "blank");
                    }
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
            treeView1.ExpandAll();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            MediaHub.Properties.Settings.Default.path = currentFolder;
            MediaHub.Properties.Settings.Default.Save();
        }
    }

    public class NoHScrollTree : TreeView
    {
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x8000; // TVS_NOHSCROLL
                return cp;
            }
        }
    }
}