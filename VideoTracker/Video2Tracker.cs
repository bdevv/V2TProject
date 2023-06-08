using AForge.Controls;
using AForge.Video.DirectShow;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

namespace VideoTracker
{

    public partial class Video2Tracker : Form
    {
        List<string> deviceIDs;
        string bmpFilePath = "";
        int width = 0;
        int height = 0;
        Thread trd;
        bool stopFlag = false;
        VideoCaptureDevice currentDevice;
        int sampleRate = 20;
        static string iniFilePath = "config.ini";
        static string pixelFilePath = "pixelsize.ini";
        delegate void WorkDelegate(float aspectRatio);
        WorkDelegate workDelegate;
        float currentaspectRatio;
        bool isMeasure = false;
        bool isStartPoint = false;
        
        Point startPoint = new Point(0, 0);
        Point endPoint = new Point(0, 0);
        Bitmap currentBmp = null;
        public Video2Tracker()
        {
            InitializeComponent();
            measurePictureBox.Hide();
            measurePictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            workDelegate = new WorkDelegate(DoWork);
            currentaspectRatio = (float)videoSourcePlayer1.Width / videoSourcePlayer1.Height;
            deviceIDs = new List<string>();
            saveFileDialog1.Filter = "bmp files(*.bmp)|*.bmp";
            FileStream fs = File.Open(iniFilePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
            var inireader = new StreamReader(fs);
            initCamera();
            while (!inireader.EndOfStream)
            {
                string settingtmp = inireader.ReadLine();
                if (settingtmp.Contains("bmpFilePath"))
                {
                    txtBmpPath.Text = settingtmp.Substring(settingtmp.IndexOf("=") + 1);
                    bmpFilePath = txtBmpPath.Text;
                }
                else if (settingtmp.Contains("sampleRate"))
                {
                    txtSampleRate.Text = settingtmp.Substring(settingtmp.IndexOf("=") + 1);
                    try
                    {
                        sampleRate = int.Parse(txtSampleRate.Text);

                    }
                    catch (FormatException ex)
                    {
                        sampleRate = 20; // default = 20
                        return;
                    }
                }
                else if (settingtmp.Contains("width"))
                {
                    txtWidth.Text = settingtmp.Substring(settingtmp.IndexOf("=") + 1);
                    try
                    {
                        width = int.Parse(txtWidth.Text);
                    }
                    catch (FormatException ex)
                    {
                        width = 1280; // default = 1280
                        return;
                    }
                }
                else if (settingtmp.Contains("height"))
                {
                    txtHeight.Text = settingtmp.Substring(settingtmp.IndexOf("=") + 1);
                    try
                    {
                        height = int.Parse(txtHeight.Text);
                    }
                    catch (FormatException ex)
                    {
                        height = 720; // default = 720
                        return;
                    }
                }
                else if (settingtmp.Contains("cameraName"))
                {
                    string cameraName = settingtmp.Substring(settingtmp.IndexOf("=") + 1);
                    if (cameraName != "")
                        cmbCamera.Text = cameraName;
                }
                else if (settingtmp.Contains("pixelSize"))
                {
                    string pixelSize = settingtmp.Substring(settingtmp.IndexOf("=") + 1);
                    if (pixelSize != "")
                        txtPixelSize.Text = pixelSize;
                }
            }
            for (int i = 0; i < cmbResolution.Items.Count; i++)
            {
                string itemstring = (string)cmbResolution.Items[i];
                string[] itemary = itemstring.Split('*');
                if (itemary[0] == width.ToString() && itemary[1] == height.ToString())
                {
                    cmbResolution.SelectedIndex = i;
                    break;
                }
            }
            if (cmbResolution.SelectedIndex == -1)
            {
                cmbResolution.SelectedIndex = cmbResolution.Items.Count - 1;
                txtHeight.Enabled = true;
                txtWidth.Enabled = true;
            }
            txtActualPixels.Text = (double.Parse(txtPixelSize.Text) * double.Parse(txtDistance.Text)).ToString();
            saveIniFile();

        }
        private void DoWork(float aspectRatio)
        {
            // Calculate the aspect ratio of the video frame

            if (currentaspectRatio != aspectRatio)
            {
                // Calculate the maximum size of the scaled video frame that fits within the control's bounds
                int maxWidth = videoSourcePlayer1.ClientSize.Width;
                int maxHeight = videoSourcePlayer1.ClientSize.Height;
                int scaledWidth = maxWidth;
                int scaledHeight = (int)(scaledWidth / aspectRatio);
                if (scaledHeight > maxHeight)
                {
                    scaledHeight = maxHeight;
                    scaledWidth = (int)(scaledHeight * aspectRatio);
                }
                videoSourcePlayer1.Size = new Size(scaledWidth, scaledHeight);
                currentaspectRatio = aspectRatio;
            }

        }
        private void saveIniFile()
        {
            string saveIniString = "pixelSize=" + txtPixelSize.Text + '\n';
            saveIniString += "bmpFilePath=" + (bmpFilePath == "" ? @"C:\Tracker\bmp\MevisNDI_State.bmp" : bmpFilePath) + '\n';
            sampleRate = int.Parse(txtSampleRate.Text);
            saveIniString += "sampleRate=" + (sampleRate == 0 ? 20 : sampleRate) + '\n';
            saveIniString += "width=" + (width == 0 ? 1280 : width) + '\n';
            saveIniString += "height=" + (height == 0 ? 720 : height) + '\n';
            saveIniString += "cameraName=" + cmbCamera.Text + '\n';

            File.WriteAllText(iniFilePath, saveIniString);
            string pixelsize = "pixelSize=" + txtPixelSize.Text + '\n';
            if (bmpFilePath != "")
            {
                try
                {
                    File.WriteAllText(bmpFilePath.Substring(0, bmpFilePath.LastIndexOf('\\') + 1) + pixelFilePath, pixelsize);
                }
                catch { }
            }
        }
        private void initCamera()
        {
            FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (videoDevices.Count == 0)
            {
                return;
            }
            for (int i = 0; i < videoDevices.Count; i++)
            {
                cmbCamera.Items.Add(videoDevices[i].Name);
                deviceIDs.Add(videoDevices[i].MonikerString);
            }
        }
        private void btnInitialize_Click(object sender, EventArgs e)
        {
            FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (videoDevices.Count == 0)
            {
                return;
            }
            for (int i = 0; i < videoDevices.Count; i++)
            {
                cmbCamera.Items.Add(videoDevices[i].Name);
                deviceIDs.Add(videoDevices[i].MonikerString);
            }
            btnStart.Enabled = true;
            cmbCamera.SelectedIndex = 0;

        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = "Starting thread.";
            if (cmbCamera.Items.Count == 0)
            {
                MessageBox.Show("No Camera Selected", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string deviceId = deviceIDs[cmbCamera.SelectedIndex];
            // create video source
            currentDevice = new VideoCaptureDevice(deviceId);

            videoSourcePlayer1.VideoSource = currentDevice;
            var videoCapabilities = currentDevice.VideoCapabilities;
            if (videoCapabilities.Count() > 0)
                currentDevice.VideoResolution = currentDevice.VideoCapabilities.First();      //First is FHD resolution

            var snapVabalities = currentDevice.SnapshotCapabilities;
            if (snapVabalities.Count() > 0)
                currentDevice.SnapshotResolution = currentDevice.SnapshotCapabilities.Last();

            currentDevice.Start();
            Thread.Sleep(1500);

            trd = new Thread(new ThreadStart(this.VideoThread));
            trd.IsBackground = true;
            trd.Start();
            stopFlag = false;
            btnStop.Enabled = true;
            cmbCamera.Enabled = false;
            btnMeasure.Enabled = true;
            toolStripStatusLabel1.Text = "© Andres Bruna";
        }
        private void VideoThread()
        {
            while (!stopFlag)
            {
                long prevTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
                Bitmap originalBitmap = videoSourcePlayer1.GetCurrentVideoFrame();
                if (originalBitmap == null)
                {
                    Thread.Sleep(1000 / sampleRate);
                    continue;
                }
                // Create a new bitmap with the desired resolution
                Bitmap newBitmap = new Bitmap(width, height, originalBitmap.PixelFormat);// Draw the original bitmap onto the new bitmap with the desired resolution

                float aspectRatio = (float)originalBitmap.Width / (float)originalBitmap.Height;
                try
                {
                    videoSourcePlayer1.Invoke(workDelegate, aspectRatio);
                }
                catch (Exception ex) { }
                using (Graphics graphics = Graphics.FromImage(newBitmap))
                {
                    graphics.DrawImage(originalBitmap, new Rectangle(0, 0, newBitmap.Width, newBitmap.Height));
                }// Save the new bitmap to a file

                using (MemoryStream memory = new MemoryStream())
                {
                    newBitmap.Save(memory, ImageFormat.Bmp);
                    try
                    {
                        File.WriteAllBytes(bmpFilePath, memory.ToArray());
                    }
                    catch (Exception ex)
                    {
                        Thread.Sleep(1000 / sampleRate);
                        continue;
                    }


                }

                if (DateTime.Now.Second % 30 == 0)
                {
                    GC.Collect();
                }
                long timeDiff = (new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds()) - prevTime;
                if (1000 / sampleRate - (int)timeDiff > 0)
                {
                    Thread.Sleep(1000 / sampleRate - (int)timeDiff);
                }
                else
                    Thread.Sleep(1000 / sampleRate);

            }

        }
        private void btnStop_Click(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = "Stopping thread";
            stopFlag = true;
            Thread.Sleep(100);
            trd = null;
            GC.Collect();
            currentDevice.Stop();
            Thread.Sleep(1000);
            btnStop.Enabled = false;
            btnMeasure.Enabled = false;
            cmbCamera.Enabled = true;
            toolStripStatusLabel1.Text = "© Andres Bruna";
        }

        private void cmbResolution_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedText = cmbResolution.Text;
            string[] dimensions = selectedText.Split('*');
            if (cmbResolution.SelectedIndex == cmbResolution.Items.Count - 1)
            {
                txtWidth.Enabled = true;
                txtHeight.Enabled = true;
            }
            else
            {
                width = int.Parse(dimensions[0]);
                height = int.Parse(dimensions[1]);
                txtWidth.Text = width.ToString();
                txtHeight.Text = height.ToString();
                txtWidth.Enabled = false;
                txtHeight.Enabled = false;
            }
            saveIniFile();
        }

        private void txtWidth_TextChanged(object sender, EventArgs e)
        {
            try
            {
                width = int.Parse(txtWidth.Text);
                if (width <= 0 || width > 1920)
                    width = 1280;
                txtWidth.Text = width.ToString();
            }
            catch (Exception ex)
            {
                width = 1280;
            }

            saveIniFile();
        }

        private void txtHeight_TextChanged(object sender, EventArgs e)
        {
            try
            {
                height = int.Parse(txtHeight.Text);
                if (height <= 0 || height > 1080)
                    height = 720;
                txtHeight.Text = height.ToString();
            }
            catch (Exception ex)
            {
                height = 720;
            }
            saveIniFile();
        }

        private void txtSampleRate_TextChanged(object sender, EventArgs e)
        {
            saveIniFile();
        }

        private void txtBmpPath_TextChanged(object sender, EventArgs e)
        {
            saveIniFile();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            if (saveFileDialog1.ShowDialog() == DialogResult.Cancel)
                return;
            bmpFilePath = saveFileDialog1.FileName;
            txtBmpPath.Text = bmpFilePath;
            saveIniFile();
        }

        private void txtWidth_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void txtHeight_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }

        }

        private void txtSampleRate_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void cmbCamera_SelectedIndexChanged(object sender, EventArgs e)
        {
            saveIniFile();
        }

        private void txtPixelSize_TextChanged(object sender, EventArgs e)
        {
            saveIniFile();
        }

        private void Video2Tracker_FormClosing(object sender, FormClosingEventArgs e)
        {
            stopFlag = true;
            Thread.Sleep(1000);
            System.Environment.Exit(0);
        }

        private void btnMeasure_Click(object sender, EventArgs e)
        {
            if (!isMeasure)
            {
                toolStripStatusLabel1.Text = "Left mouse click and drag to measure distances. Edit Distance[mm] and click Calc button to calculate a new Pixel Size.";
                if (stopFlag == false)
                    btnStop.Enabled = false;
                
                currentBmp = videoSourcePlayer1.GetCurrentVideoFrame();
                if (currentBmp != null)
                {

                    btnMeasure.Text = "Resume";
                    
                    measurePictureBox.Show();
                    videoSourcePlayer1.Hide();
                    measurePictureBox.Image = currentBmp;
                    measurePictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
                }
            }

            else
            {
                toolStripStatusLabel1.Text = "© Andres Bruna";
                if (stopFlag == false)
                    btnStop.Enabled = true;
                
                measurePictureBox.Hide();
                videoSourcePlayer1.Show();
                btnMeasure.Text = "Measure";
            }
            if (txtActualPixels.Text != "" && txtDistance.Text != "" && isMeasure == false)
                btnCalc.Enabled = true;
            else
                btnCalc.Enabled = false;
            isMeasure = !isMeasure;
        }

        private void Video2Tracker_Load(object sender, EventArgs e)
        {

        }

        private void measurePictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (isMeasure)
            {
                startPoint = e.Location;
                isStartPoint = true;
            }
        }

        private void measurePictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMeasure && isStartPoint == true)
            {
                endPoint = e.Location;
                Graphics graphics = measurePictureBox.CreateGraphics();

                measurePictureBox.Invalidate(false);
                graphics.DrawLine(new Pen(Color.Red, 2), startPoint, endPoint);
                double length = Math.Sqrt((startPoint.X - endPoint.X) * (startPoint.X - endPoint.X) + (startPoint.Y - endPoint.Y) * (startPoint.Y - endPoint.Y));
                double pictureboxRatio = ((double)currentBmp.Width) / measurePictureBox.Width;
                double actualPixels = length * pictureboxRatio;
                txtActualPixels.Text = ((int)actualPixels).ToString();

            }

        }

        private void measurePictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (isMeasure && isStartPoint)
            {
                endPoint = e.Location;
                isStartPoint = false;
                Graphics graphics = measurePictureBox.CreateGraphics();
                graphics.DrawLine(new Pen(Color.Red, 2), startPoint, endPoint);
                double length = Math.Sqrt((startPoint.X - endPoint.X) * (startPoint.X - endPoint.X) + (startPoint.Y - endPoint.Y) * (startPoint.Y - endPoint.Y));
                double pictureboxRatio = ((double)currentBmp.Width) / measurePictureBox.Width;
                double actualPixels = length * pictureboxRatio;
                txtActualPixels.Text = ((int)actualPixels).ToString();
                txtDistance.Text = Math.Round(actualPixels * double.Parse(txtPixelSize.Text),4).ToString();
                if (txtActualPixels.Text != "" && txtDistance.Text != "" && isMeasure == true)
                    btnCalc.Enabled = true;
                else
                    btnCalc.Enabled = false;
            }

        }

        private void btnCalc_Click(object sender, EventArgs e)
        {
            DialogResult dialogResult = MessageBox.Show("You will change PixelSize of the Output Image. Continue?", "Confirm", MessageBoxButtons.YesNo,MessageBoxIcon.Warning);
            if (dialogResult == DialogResult.Yes)
            {
                txtPixelSize.Text = (Math.Round(double.Parse(txtDistance.Text) / double.Parse(txtActualPixels.Text), 4)).ToString();

            }
            else if (dialogResult == DialogResult.No)
            {
                return;

            }
        }

        private void statusStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }
    }
}
