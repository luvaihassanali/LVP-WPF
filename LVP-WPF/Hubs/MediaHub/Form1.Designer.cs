namespace MediaIndexUtil
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            imageList1 = new ImageList(components);
            splitContainer1 = new SplitContainer();
            splitContainer2 = new SplitContainer();
            OpenFolder_Button = new Button();
            treeView1 = new NoHScrollTree();
            listView1 = new ListView();
            columnHeader1 = new ColumnHeader();
            columnHeader3 = new ColumnHeader();
            columnHeader2 = new ColumnHeader();
            panel1 = new Panel();
            checkBox3 = new CheckBox();
            Compare_Button = new Button();
            Tree_Button = new Button();
            checkBox2 = new CheckBox();
            textBox1 = new TextBox();
            Rename_Button = new Button();
            checkBox1 = new CheckBox();
            listView2 = new ListView();
            columnHeader4 = new ColumnHeader();
            columnHeader6 = new ColumnHeader();
            folderBrowserDialog1 = new FolderBrowserDialog();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer2).BeginInit();
            splitContainer2.Panel1.SuspendLayout();
            splitContainer2.Panel2.SuspendLayout();
            splitContainer2.SuspendLayout();
            panel1.SuspendLayout();
            SuspendLayout();
            // 
            // imageList1
            // 
            imageList1.ColorDepth = ColorDepth.Depth8Bit;
            imageList1.ImageStream = (ImageListStreamer)resources.GetObject("imageList1.ImageStream");
            imageList1.TransparentColor = Color.Transparent;
            imageList1.Images.SetKeyName(0, "icons8-folder-250.png");
            imageList1.Images.SetKeyName(1, "icons8-document-250.png");
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 0);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(splitContainer2);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(panel1);
            splitContainer1.Panel2.Controls.Add(listView2);
            splitContainer1.Size = new Size(1097, 518);
            splitContainer1.SplitterDistance = 809;
            splitContainer1.TabIndex = 0;
            // 
            // splitContainer2
            // 
            splitContainer2.Dock = DockStyle.Fill;
            splitContainer2.Location = new Point(0, 0);
            splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            splitContainer2.Panel1.Controls.Add(OpenFolder_Button);
            splitContainer2.Panel1.Controls.Add(treeView1);
            // 
            // splitContainer2.Panel2
            // 
            splitContainer2.Panel2.Controls.Add(listView1);
            splitContainer2.Size = new Size(809, 518);
            splitContainer2.SplitterDistance = 274;
            splitContainer2.TabIndex = 0;
            // 
            // OpenFolder_Button
            // 
            OpenFolder_Button.Cursor = Cursors.Hand;
            OpenFolder_Button.Location = new Point(205, 490);
            OpenFolder_Button.Name = "OpenFolder_Button";
            OpenFolder_Button.Size = new Size(66, 25);
            OpenFolder_Button.TabIndex = 1;
            OpenFolder_Button.Text = "Browse";
            OpenFolder_Button.UseVisualStyleBackColor = true;
            OpenFolder_Button.Click += OpenFolderButton_Click;
            // 
            // treeView1
            // 
            treeView1.Dock = DockStyle.Fill;
            treeView1.ImageIndex = 0;
            treeView1.ImageList = imageList1;
            treeView1.Location = new Point(0, 0);
            treeView1.Name = "treeView1";
            treeView1.SelectedImageIndex = 0;
            treeView1.Size = new Size(274, 518);
            treeView1.TabIndex = 0;
            treeView1.NodeMouseClick += TreeView1_NodeMouseClick;
            // 
            // listView1
            // 
            listView1.Columns.AddRange(new ColumnHeader[] { columnHeader1, columnHeader3, columnHeader2 });
            listView1.Dock = DockStyle.Fill;
            listView1.Location = new Point(0, 0);
            listView1.Name = "listView1";
            listView1.Size = new Size(531, 518);
            listView1.SmallImageList = imageList1;
            listView1.TabIndex = 0;
            listView1.UseCompatibleStateImageBehavior = false;
            listView1.View = View.Details;
            listView1.ItemSelectionChanged += ListView1_ItemSelectionChanged;
            // 
            // columnHeader1
            // 
            columnHeader1.Text = "Name";
            columnHeader1.Width = 160;
            // 
            // columnHeader3
            // 
            columnHeader3.Text = "Type";
            // 
            // columnHeader2
            // 
            columnHeader2.Text = "Path";
            columnHeader2.Width = 300;
            // 
            // panel1
            // 
            panel1.BorderStyle = BorderStyle.FixedSingle;
            panel1.Controls.Add(checkBox3);
            panel1.Controls.Add(Compare_Button);
            panel1.Controls.Add(Tree_Button);
            panel1.Controls.Add(checkBox2);
            panel1.Controls.Add(textBox1);
            panel1.Controls.Add(Rename_Button);
            panel1.Controls.Add(checkBox1);
            panel1.Location = new Point(0, 0);
            panel1.Name = "panel1";
            panel1.Size = new Size(284, 58);
            panel1.TabIndex = 7;
            // 
            // checkBox3
            // 
            checkBox3.AutoSize = true;
            checkBox3.Cursor = Cursors.Hand;
            checkBox3.Location = new Point(89, 31);
            checkBox3.Name = "checkBox3";
            checkBox3.Size = new Size(57, 19);
            checkBox3.TabIndex = 7;
            checkBox3.Text = "COPY";
            checkBox3.UseVisualStyleBackColor = true;
            // 
            // Compare_Button
            // 
            Compare_Button.Cursor = Cursors.Hand;
            Compare_Button.Location = new Point(3, 29);
            Compare_Button.Name = "Compare_Button";
            Compare_Button.Size = new Size(66, 25);
            Compare_Button.TabIndex = 4;
            Compare_Button.Text = "Compare";
            Compare_Button.UseVisualStyleBackColor = true;
            Compare_Button.Click += CompareButton_Click;
            // 
            // Tree_Button
            // 
            Tree_Button.Cursor = Cursors.Hand;
            Tree_Button.Location = new Point(3, 2);
            Tree_Button.Name = "Tree_Button";
            Tree_Button.Size = new Size(66, 25);
            Tree_Button.TabIndex = 5;
            Tree_Button.Text = "Tree";
            Tree_Button.UseVisualStyleBackColor = true;
            Tree_Button.Click += TreeButton_Click;
            // 
            // checkBox2
            // 
            checkBox2.AutoSize = true;
            checkBox2.Cursor = Cursors.Hand;
            checkBox2.Location = new Point(89, 6);
            checkBox2.Name = "checkBox2";
            checkBox2.Size = new Size(57, 19);
            checkBox2.TabIndex = 6;
            checkBox2.Text = "LANG";
            checkBox2.UseVisualStyleBackColor = true;
            // 
            // textBox1
            // 
            textBox1.Cursor = Cursors.IBeam;
            textBox1.Location = new Point(214, 30);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(65, 23);
            textBox1.TabIndex = 2;
            textBox1.TextAlign = HorizontalAlignment.Right;
            // 
            // Rename_Button
            // 
            Rename_Button.Cursor = Cursors.Hand;
            Rename_Button.Location = new Point(214, 2);
            Rename_Button.Name = "Rename_Button";
            Rename_Button.Size = new Size(66, 25);
            Rename_Button.TabIndex = 1;
            Rename_Button.Text = "Rename";
            Rename_Button.UseVisualStyleBackColor = true;
            Rename_Button.Click += RenameButton_Click;
            // 
            // checkBox1
            // 
            checkBox1.AutoSize = true;
            checkBox1.Cursor = Cursors.Hand;
            checkBox1.Location = new Point(152, 6);
            checkBox1.Name = "checkBox1";
            checkBox1.Size = new Size(44, 19);
            checkBox1.TabIndex = 3;
            checkBox1.Text = "SRT";
            checkBox1.UseVisualStyleBackColor = true;
            // 
            // listView2
            // 
            listView2.Columns.AddRange(new ColumnHeader[] { columnHeader4, columnHeader6 });
            listView2.Dock = DockStyle.Bottom;
            listView2.Location = new Point(0, 62);
            listView2.Name = "listView2";
            listView2.Size = new Size(284, 456);
            listView2.SmallImageList = imageList1;
            listView2.TabIndex = 1;
            listView2.UseCompatibleStateImageBehavior = false;
            listView2.View = View.Details;
            listView2.ItemSelectionChanged += ListView2_ItemSelectionChanged;
            // 
            // columnHeader4
            // 
            columnHeader4.Text = "Index";
            // 
            // columnHeader6
            // 
            columnHeader6.Text = "Name";
            columnHeader6.Width = 212;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(1097, 518);
            Controls.Add(splitContainer1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Form1";
            Text = "Media Hub";
            FormClosing += Form1_FormClosing;
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            splitContainer2.Panel1.ResumeLayout(false);
            splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer2).EndInit();
            splitContainer2.ResumeLayout(false);
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        private ImageList imageList1;
        private SplitContainer splitContainer1;
        private SplitContainer splitContainer2;
        private ListView listView1;
        private ColumnHeader columnHeader1;
        private ListView listView2;
        private ColumnHeader columnHeader4;
        private ColumnHeader columnHeader6;
        private ColumnHeader columnHeader2;
        private Button Rename_Button;
        private TextBox textBox1;
        private ColumnHeader columnHeader3;
        private CheckBox checkBox1;
        private Button Compare_Button;
        private Button Tree_Button;
        private Button OpenFolder_Button;
        private FolderBrowserDialog folderBrowserDialog1;
        private CheckBox checkBox2;
        private Panel panel1;
        private NoHScrollTree treeView1;
        private CheckBox checkBox3;
    }
}