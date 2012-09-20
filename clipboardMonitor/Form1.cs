using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Data.SQLite;
using System.IO;

namespace clipboardMonitor
{
    public partial class mainWindow : Form
    {
        [DllImport("User32.dll")]
		    protected static extern int SetClipboardViewer(int hWndNewViewer);
        [DllImport("User32.dll", CharSet = CharSet.Auto)]
            public static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);
        [DllImport("User32.dll", CharSet = CharSet.Auto)]
            public static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);
        IntPtr nextClipboardViewer; //holds first window in clipboard chain
        public static SQLiteConnection db;
        public mainWindow()
        {
            InitializeComponent();
            nextClipboardViewer = (IntPtr)SetClipboardViewer((int) this.Handle); //gets next in chain

            //setup database connection
            db = new SQLiteConnection(@"Data Source=clipDB.s3db");
            db.Open();

            populateTreeMenu();
        }
        protected void newClipBoardData()
        {
            if (db.State == System.Data.ConnectionState.Open)
            {
                //insert capture record
                SQLiteCommand insertCmd = new SQLiteCommand(db);
                insertCmd.CommandText = @"INSERT INTO capture (timestamp) VALUES('"+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+"')";
                int rowsUpdated = insertCmd.ExecuteNonQuery();
                
                insertCmd.CommandText = @"SELECT last_insert_rowid()"; //get id from capture record to use as foreign key
                long captureID = (long) insertCmd.ExecuteScalar();

                IDataObject clipData = Clipboard.GetDataObject();
                string[] formats = clipData.GetFormats();
                if (clipData.GetData(DataFormats.Html) != null)
                {
                    string html = (string)Clipboard.GetData(@"text\HTML");
                    if (html != null)
                    {
                        insertCmd.CommandText = @"INSERT INTO htmlData (captureID, text) VALUES('" + captureID + "', (@html))";
                        insertCmd.Parameters.Add("@html", DbType.String, html.Length).Value = html;
                        insertCmd.ExecuteNonQuery();
                    }
                }
                if (Clipboard.ContainsText())//insert text data
                {
                    insertCmd.CommandText = @"INSERT INTO textData (captureID, text) VALUES('" + captureID + "', '" + Clipboard.GetText().Replace("'", "''") + "')";
                    rowsUpdated = insertCmd.ExecuteNonQuery();
                }
                if (Clipboard.ContainsImage())
                {
                    System.IO.MemoryStream memStream = new System.IO.MemoryStream() ;
                    Clipboard.GetImage().Save(memStream, System.Drawing.Imaging.ImageFormat.Jpeg); //save image into stream
                    byte[] imgData = new byte[memStream.Length];
                    memStream.Seek(0, SeekOrigin.Begin);
                    memStream.Read(imgData, 0, (int) memStream.Length); //write stream onto imgData
                    //insertCmd.CommandText = @"INSERT INTO imageData (captureID, image) VALUES('" +captureID + "', '";// + imgData + "')";
                    insertCmd.CommandText = @"INSERT INTO imageData (captureID, image) VALUES('" +captureID + "', (@image))";


                    //Writes to file for testing
                    //FileStream fs = File.OpenWrite("toSQL.jpg");
                    //fs.Write(imgData, 0, imgData.Length);
                    //fs.Close();
                    //


                    //for (int i = 0; i < imgData.Length; i++)
                    //    insertCmd.CommandText += imgData[i]; //adds image data to command
                    insertCmd.Parameters.Add("@image", DbType.Binary, imgData.Length).Value = imgData;
                    //insertCmd.CommandText += "')";
                    rowsUpdated = insertCmd.ExecuteNonQuery();
                }
            }
            populateTreeMenu();
        }
        protected override void WndProc(ref System.Windows.Forms.Message m) //override wndProc
        {
            //as defined in winuser.h, these are the messages we want to receive
            const int WM_DRAWCLIPBOARD = 0x308;
            const int WM_CHANGECBCHAIN = 0x030D;

            switch (m.Msg)
            {
                case WM_DRAWCLIPBOARD: //if clipboard contents change
                    //textBox1.Text = Clipboard.GetText();
                    newClipBoardData(); //process new CB
                    SendMessage(nextClipboardViewer, m.Msg, m.WParam, m.LParam); //pass CB data to next in chain
                    break;

                case WM_CHANGECBCHAIN: //if clipboard chain changes
                    if (m.WParam == nextClipboardViewer) //check if window being removed is next in chain
                        nextClipboardViewer = m.LParam; //update next in chain
                    else
                        SendMessage(nextClipboardViewer, m.Msg, m.WParam, m.LParam); //pass message along chain
                    break;

                default:
                    base.WndProc(ref m);
                    break;
            }
        }
        protected void populateTreeMenu()
        {
            treeMenu.Nodes.Clear(); //clear all nodes for an update
            //List<TreeNode> nodes = new List<TreeNode>();
            SQLiteCommand selectCmd = new SQLiteCommand(db);
            selectCmd.CommandText = @"SELECT captureID, timestamp FROM capture";
            SQLiteDataReader sqlReader = selectCmd.ExecuteReader();
            while(sqlReader.Read())
            {
                TreeNode newNode = new TreeNode(sqlReader.GetString(1));
                newNode.Tag = (int)sqlReader.GetInt32(0);
                treeMenu.Nodes.Add(newNode);
            }
        }
        /*private void button2_Click(object sender, EventArgs e) //gets image for entered captureID
        {
            SQLiteCommand selectCmd = new SQLiteCommand(db);
            selectCmd.CommandText = @"SELECT image FROM imageData WHERE captureID = '" + imageIDbox.Text + "'";
            byte[] imgData = (System.Byte[])selectCmd.ExecuteScalar();
            //byte[] imgData = Convert.FromBase64String(imgString);
            System.IO.MemoryStream memStream = new System.IO.MemoryStream(imgData); 
            memStream.Write(imgData, 0, imgData.Length); //puts image data into stream
            try { 
                pictureBox1.Image = Image.FromStream(memStream); 
            }
            catch(System.ArgumentException){ 
                Console.WriteLine("Invalid image!");
            }
        }*/
        
        void formatSelector_SelectedValueChanged(object sender, System.EventArgs e)
        {
            SQLiteCommand selectCmd = new SQLiteCommand(db);
            switch ((string) formatSelector.Text)
            {
                case "Text":
                    selectCmd.CommandText = @"SELECT text FROM textData WHERE captureID = '" + treeMenu.SelectedNode.Tag + "'";
                    resultTextBox.Text = (string)selectCmd.ExecuteScalar();
                    resultTextBox.BringToFront();
                    break;
                case "HTML":
                    selectCmd.CommandText = @"SELECT text FROM htmlData WHERE captureID = '" + treeMenu.SelectedNode.Tag + "'";
                    resultTextBox.Text = (string)selectCmd.ExecuteScalar();
                    resultTextBox.BringToFront();
                    break;
                case "Render HTML":
                    selectCmd.CommandText = @"SELECT text FROM htmlData WHERE captureID = '" + treeMenu.SelectedNode.Tag + "'";
                    string html = (string)selectCmd.ExecuteScalar();
                    webBrowser1.DocumentText = html;
                    webBrowser1.BringToFront();
                    break;
                default:
                    Console.WriteLine("Unrecognized value on combo box");
                    break;

            }
        }
        private void treeMenu_AfterSelect(object sender, TreeViewEventArgs e)
        {
            SQLiteCommand selectCmd = new SQLiteCommand(db);
            formatSelector.Visible = false;

            //show text
            selectCmd.CommandText = @"SELECT text FROM textData WHERE captureID = '" + e.Node.Tag +"'";
            resultTextBox.Text = (string) selectCmd.ExecuteScalar();
            resultTextBox.BringToFront();
            
            //Add html option
            selectCmd.CommandText = @"SELECT text FROM htmlData WHERE captureID = '" + e.Node.Tag + "'";
            string html = (string) selectCmd.ExecuteScalar();
            if (html != null)
            {
                formatSelector.Visible = true;
                formatSelector.Text = "Text";
                formatSelector.Items.Clear();
                formatSelector.Items.Add("Text");
                formatSelector.Items.Add("HTML");
                formatSelector.Items.Add("Render HTML");
            }


            //show image
            selectCmd.CommandText = @"SELECT image FROM imageData WHERE captureID = '" + e.Node.Tag + "'";
            byte[] imgData = (System.Byte[])selectCmd.ExecuteScalar();
            //byte[] imgData = Convert.FromBase64String(imgString);
            if (imgData != null) //only if image found
            {
                System.IO.MemoryStream memStream = new System.IO.MemoryStream(imgData);
                memStream.Write(imgData, 0, imgData.Length); //puts image data into stream
                try
                {
                    pictureBox1.Image = Image.FromStream(memStream);
                    pictureBox1.BringToFront();
                }
                catch (System.ArgumentException)
                {
                    Console.WriteLine("Invalid image!");
                }
            }
        }

        private void clearCaptureButton_Click(object sender, EventArgs e)
        {
            if (db.State == System.Data.ConnectionState.Open)
            {
                SQLiteCommand clearCmd = new SQLiteCommand(db);
                clearCmd.CommandText = @"DELETE FROM capture; 
                                        DELETE FROM htmlData; 
                                        DELETE FROM imageData; 
                                        DELETE FROM textData; 
                                        DELETE FROM unicodeData;";
                int rowsDeleted = clearCmd.ExecuteNonQuery();
                System.Windows.Forms.MessageBox.Show(rowsDeleted + " Records deleted.");
                treeMenu.Nodes.Clear();

            }
            else
            {
                System.Windows.Forms.MessageBox.Show("Database error");
            }
        }
    }
}
