using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace KinectAnalysis
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            loadImages();
        }

        // Depth data (in millimeters) read out from binary depth file.
        ushort[] depthData;

        // Infrared image corresponding to depth data.
        Bitmap irImage;

        // Colored depth image that is created from depth data.
        Bitmap coloredDepthImage = null;

        // True when user is selecting a rectangle on the image.
        private bool IsSelecting = false;

        // The pixel coordinates that the user is selecting.
        private int X0, Y0, X1, Y1;

        // Base directory where depth and IR files are stored.
        string baseDirectory = @"C:\Users\Kyle\Documents\Kinectv2\KinectAnalysis\test_set";

        // Binary file to containing depth data (unsigned 16 bit) in millimeters.
        string depthDataFileName = "DepthData.bin";

        // File name of infrared image.
        string irFileName = "IR.jpg";

        // Called when load images button is clicked.
        private void loadImages()
        {
            string depthDataPath = Path.Combine(baseDirectory, depthDataFileName);

            string irFilePath = Path.Combine(baseDirectory, irFileName);

            try
            {
                // Load infrared image.
                irImage = new Bitmap(Image.FromFile(irFilePath));

                // Copy raw depth values (in millimeters) to buffer.
                byte[] depthBytes = File.ReadAllBytes(depthDataPath);
                depthData = new ushort[depthBytes.Length / 2]; // every ushort is 2 bytes
                Buffer.BlockCopy(depthBytes, 0, depthData, 0, depthBytes.Length);

                coloredDepthImage = createColoredDepthImage();
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show("All files not present or correctly named.");
            }

            // Update images on form.
            depthPictureBox.Image = coloredDepthImage;
            irPictureBox.Image = irImage;
        }

        private Bitmap createColoredDepthImage()
        {
            // Use irImage for depth/height information since it's from the same sensor as the depth data.
            Bitmap coloredBitmap = new Bitmap(irImage);

            int depthImageWidth = irImage.Width;
            int depthImageHeight = irImage.Height;

            for (int y = 0; y < depthImageHeight; y++)
            {
                for (int x = 0; x < depthImageWidth; x++)
                {
                    int depth = depthData[y * depthImageWidth + x];

                    // Make color value 0 (black) if no depth information is available.
                    double value = (depth == 0) ? 0 : 1;

                    // Full saturation.
                    double saturation = 1;

                    // Convert depth to hue in degrees
                    depth -= 500;
                    double hue = depth * (360.0 / 500.0);

                    int r, g, b;
                    HsvToRgb(hue, saturation, value, out r, out g, out b);

                    coloredBitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
                }
            }

            return coloredBitmap;
        }

        private void showDepthStats()
        {
            if (depthData == null || depthData.Length == 0)
            {
                MessageBox.Show("No depth data.");
                return;
            }

            int depthImageWidth = irImage.Width;
            int depthImageHeight = irImage.Height;

            int numBadReadings = 0;

            List<int> goodReadings = new List<int>();

            for (int y = Y0; y < Y1; y++)
            {
                for (int x = X0; x < X1; x++)
                {
                    int depth = depthData[y * depthImageWidth + x];

                    if (depth == 0)
                    {
                        numBadReadings++;
                        continue;
                    }

                    goodReadings.Add(depth);
                }
            }

            if (goodReadings.Count == 0)
            {
                MessageBox.Show("No useful readings.");
            }
            else
            {
                goodReadings.Sort();

                double top10average = 0;
                try
                {
                    top10average = goodReadings.GetRange(0, goodReadings.Count / 20).Average();
                }
                catch (InvalidOperationException) 
                {
                    top10average = goodReadings.Average();
                }
                MessageBox.Show(String.Format("Good: {0}\nBad: {1}\nMax: {2}\nMin: {3}\nAvg: {4}\nTop 10%: {5}", 
                    goodReadings.Count, numBadReadings, goodReadings.Max(), goodReadings.Min(), (int)goodReadings.Average(), (int)top10average));
            }
        }

        private void irPictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            IsSelecting = true;

            // Save the start point.
            X0 = e.X;
            Y0 = e.Y;
        }

        private void irPictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            // Do nothing it we're not selecting an area.
            if (!IsSelecting) { return; }

            // Save the new point.
            X1 = e.X;
            Y1 = e.Y;

            // Make a Bitmap to display the selection rectangle.
            Bitmap bm = new Bitmap(irImage);

            // Draw the rectangle.
            using (Graphics gr = Graphics.FromImage(bm))
            {
                gr.DrawRectangle(Pens.Red,
                    Math.Min(X0, X1), Math.Min(Y0, Y1),
                    Math.Abs(X0 - X1), Math.Abs(Y0 - Y1));
            }

            // Display the temporary bitmap.
            irPictureBox.Image = bm;
        }

        private void irPictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            // Do nothing it we're not selecting an area.
            if (!IsSelecting) { return; }

            IsSelecting = false;

            // Draw the rectangle.
            using (Graphics gr = Graphics.FromImage(irImage))
            {
                gr.DrawRectangle(Pens.Red,
                    Math.Min(X0, X1), Math.Min(Y0, Y1),
                    Math.Abs(X0 - X1), Math.Abs(Y0 - Y1));
            }

            // Display the result.
            irPictureBox.Image = irImage;

            showDepthStats();
        }

        void HsvToRgb(double h, double S, double V, out int r, out int g, out int b)
        {
            double H = h;
            while (H < 0) { H += 360; };
            while (H >= 360) { H -= 360; };
            double R, G, B;
            if (V <= 0)
            { R = G = B = 0; }
            else if (S <= 0)
            {
                R = G = B = V;
            }
            else
            {
                double hf = H / 60.0;
                int i = (int)Math.Floor(hf);
                double f = hf - i;
                double pv = V * (1 - S);
                double qv = V * (1 - S * f);
                double tv = V * (1 - S * (1 - f));
                switch (i)
                {

                    // Red is the dominant color

                    case 0:
                        R = V;
                        G = tv;
                        B = pv;
                        break;

                    // Green is the dominant color

                    case 1:
                        R = qv;
                        G = V;
                        B = pv;
                        break;
                    case 2:
                        R = pv;
                        G = V;
                        B = tv;
                        break;

                    // Blue is the dominant color

                    case 3:
                        R = pv;
                        G = qv;
                        B = V;
                        break;
                    case 4:
                        R = tv;
                        G = pv;
                        B = V;
                        break;

                    // Red is the dominant color

                    case 5:
                        R = V;
                        G = pv;
                        B = qv;
                        break;

                    // Just in case we overshoot on our math by a little, we put these here. Since its a switch it won't slow us down at all to put these here.

                    case 6:
                        R = V;
                        G = tv;
                        B = pv;
                        break;
                    case -1:
                        R = V;
                        G = pv;
                        B = qv;
                        break;

                    // The color is not defined, we should throw an error.

                    default:
                        //LFATAL("i Value error in Pixel conversion, Value is %d", i);
                        R = G = B = V; // Just pretend its black/white
                        break;
                }
            }
            r = ((int)(R * 255.0));
            g = ((int)(G * 255.0));
            b = ((int)(B * 255.0));

            r = Math.Min(r, 255);
            g = Math.Min(g, 255);
            b = Math.Min(b, 255);

        }
    }



}
