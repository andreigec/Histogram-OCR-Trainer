using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ANDREICSLIB;
using ANDREICSLIB.ClassExtras;
using ANDREICSLIB.NewControls;

namespace HistogramOCRTrainer
{
    public partial class Form1 : Form
    {
        #region licensing

        private const string AppTitle = "Histogram OCR Trainer";
        private const double AppVersion = 0.1;
        private const String HelpString = "";

        private const String UpdatePath = "https://github.com/EvilSeven/Histogram-OCR-Trainer/zipball/master";
        private const String VersionPath = "https://raw.github.com/EvilSeven/Histogram-OCR-Trainer/master/INFO/version.txt";
        private const String ChangelogPath = "https://raw.github.com/EvilSeven/Histogram-OCR-Trainer/master/INFO/changelog.txt";

        private readonly String OtherText =
            @"©" + DateTime.Now.Year +
            @" Andrei Gec (http://www.andreigec.net)

Licensed under GNU LGPL (http://www.gnu.org/)

Zip Assets © SharpZipLib (http://www.sharpdevelop.net/OpenSource/SharpZipLib/)
";
        #endregion

        public static HistogramOCR h;

        public Form1()
        {
            InitializeComponent();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var sd = new Licensing.SolutionDetails(HelpString, AppTitle, AppVersion, OtherText, VersionPath, UpdatePath,
                                                   ChangelogPath);
            Licensing.CreateLicense(this, sd, menuStrip1);

            h = new HistogramOCR();
            RefreshLettersList();
        }

        private void RefreshLettersList()
        {
            lettersLB.Items.Clear();
            letterhistogramdisplayTB.Text = "";

            h.Letters = h.Letters.OrderBy(s => s.Letter).ToList();
            foreach (var l in h.Letters)
            {
                lettersLB.Items.Add(l.Letter);
            }
        }

        private void lettersLB_SelectedIndexChanged(object sender, EventArgs e)
        {
            letterhistogramdisplayTB.Text = "";
            var sel = lettersLB.SelectedIndex;
            if (sel == -1)
                return;

            var letter = ((char)lettersLB.Items[sel]);

            SetHistogramDisplay(h.Letters.FirstOrDefault(s => s.Letter.Equals(letter)));


        }

        private void SetHistogramDisplay(HistogramOCR.HistogramLetter hl)
        {
            if (hl == null)
                return;

            //merge arrays
            int[][] a = ArrayExtras.InstantiateArray<int>(h.HistogramWidth, h.HistogramHeight);
            int yc = hl.YValues.Count();
            int xc = hl.XValues.Count();
            for (int y = 0; y < yc; y++)
            {
                for (int v = 0; v < hl.YValues[y]; v++)
                {
                    if (y >= h.HistogramHeight || v >= h.HistogramWidth)
                        continue;
                    a[y][v] += 10;
                }
            }

            for (int x = 0; x < xc; x++)
            {
                var ymax = yc - 1;
                var ymin = ymax - hl.XValues[x];
                for (int v = ymax; v > ymin; v--)
                {
                    if (v >= h.HistogramHeight || x >= h.HistogramWidth)
                        continue;
                    a[v][x] += 100;
                }
            }

            //rotate array
            // a=ArrayExtras.RotateArray(a, h.HistogramWidth, h.HistogramHeight);
            //a = ArrayExtras.RotateArray(a, h.HistogramWidth, h.HistogramHeight);

            //print array
            for (int y = 0; y < yc; y++)
            {
                for (int x = 0; x < xc; x++)
                {
                    if (y >= h.HistogramHeight || x >= h.HistogramWidth)
                        continue;

                    var v1 = a[y][x];
                    var c = '1';
                    if (v1 == 10)
                        c = '4';
                    else if (v1 == 100)
                        c = '6';
                    else if (v1 == 110)
                        c = '8';

                    letterhistogramdisplayTB.Text += c;
                }
                letterhistogramdisplayTB.Text += Environment.NewLine;
            }
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Title = "select histogram file to load";
            ofd.Filter = SaveFileDialogExtras.createFilter("histogram file", "*.hist");
            ofd.Multiselect = false;
            ofd.InitialDirectory = Directory.GetCurrentDirectory();
            var res = ofd.ShowDialog();
            if (res != DialogResult.OK)
                return;

            var fn = ofd.FileName;

            h = HistogramOCR.DeSerialise(fn);

            RefreshLettersList();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveHist();
        }

        private void SaveHist()
        {
            var sfd = new SaveFileDialog();
            sfd.Title = "enter name/location to save histogram file";
            sfd.Filter = SaveFileDialogExtras.createFilter("histogram file", "*.hist");
            sfd.InitialDirectory = Directory.GetCurrentDirectory();
            var res = sfd.ShowDialog();
            if (res != DialogResult.OK)
                return;

            var fn = sfd.FileName;
            h.Serialise(fn);
        }

        private void TestLoadBitmap(Bitmap b)
        {
            //split into characters
            var images = h.SplitUp(b);

            //load white space straight away
            if (h.Letters.Any(s=>s.Letter.Equals(' '))==false)
            h.Train(HistogramOCR.WhiteBitmap, ' ');

            //for each image that hasnt been seen, have the player enter it
            foreach (var row in images)
            {
                foreach (var col in row)
                {
                    //try and find a perfect match
                    var c = h.PerformOCRCharacterPerfect(col);
                    //we only want things we havent seen before
                    if (c != null)
                        continue;

                    if (BitmapExtras.IsOnlyColour(col, Color.White))
                    {
                        continue;
                    }

                    //ask user for character and show
                    var gsi = new GetStringImageCompare();
                    var letters = gsi.ShowDialog("What character is this? nothing to skip", "question", col,
                                                 1);

                    if (letters == null)
                        return;

                    if (letters.Length==0)
                    {
                        continue;

                    }
                    var letter = letters.First().First();
                    if (char.IsWhiteSpace(letter))
                    {
                        MessageBox.Show("cannot be white space");
                        return;
                    }

                    if(h.Letters.Any(s=>s.Letter.Equals(letter)))
                    {
                        h.Letters.RemoveAll(s=>s.Letter.Equals(letter));
                    }
                    
                    //train
                    h.Train(col, letter);
                }
            }
        }

        private void addletters_Click(object sender, EventArgs e)
        {
            //get image
            var ofd = new OpenFileDialog();
            ofd.Title = "select image file that contains separate black letters on a white background";
            ofd.Multiselect = false;
            ofd.InitialDirectory = Directory.GetCurrentDirectory();
            var res = ofd.ShowDialog();
            if (res != DialogResult.OK)
                return;

            var fn = ofd.FileName;
            Bitmap b;
            try
            {
                b = new Bitmap(fn);
            }
            catch (Exception ex)
            {
                MessageBox.Show("error loading image:" + ex.ToString());
                return;
            }

            TestLoadBitmap(b);
            RefreshLettersList();

            res = MessageBox.Show("do you want to save?", "question", MessageBoxButtons.YesNo);
            if (res == DialogResult.Yes)
            {
                SaveHist();
            }
        }

        private void loadButtonB_Click(object sender, EventArgs e)
        {
            //get image
            var ofd = new OpenFileDialog();
            ofd.Title = "select image file that contains separate black letters on a white background";
            ofd.Multiselect = false;
            ofd.InitialDirectory = Directory.GetCurrentDirectory();
            var res = ofd.ShowDialog();
            if (res != DialogResult.OK)
                return;

            var fn = ofd.FileName;

            try
            {
                testTextBox.Text = "";
                var wf = h.PerformOCR(fn, 50);
                foreach (var row in wf)
                {
                    testTextBox.Text += row + "\r\n";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error. make sure you have the .hist file loaded");
            }

        }

    }
}
