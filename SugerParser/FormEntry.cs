using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SugerParser
{
    public partial class FormEntry : Form
    {
        public static FormEntry _Form           = null;
        private DbLoader        dbLoader        = null;
        private const int       FILE_PATH_INDEX = 1;
        private string          inputFilePath   = string.Empty;

        // main entrenace to FORM
        public FormEntry()
        {
            InitializeComponent();

            // needed to alter form components from different classes
            _Form = this;

            // enable button only when DB is ready
            report_btn.Enabled = false;

            // get command line arguments
            string[] args = Environment.GetCommandLineArgs();

            // arg0 is always application path
            // arg1 should have the DB file path
            if (args.Count() < 2)
            {
                // incorrect format
                log("Incorrect input - missing data, check arguments");
                return;
            }

            // generate new DB loader which will parse the input file into local DB
            inputFilePath = args[FILE_PATH_INDEX];
            dbLoader = new DbLoader(inputFilePath);

            if (dbLoader.isLoadCompleteSuccessfully() == true)
            {
                // activate report button
                report_btn.Enabled = true;
            }
        }

        #region Log
        public enum LogLevel
        {
            Info,
            Error
        }

        // function prints log (basic is 'info')
        // since can be called from different processes
        // need to make sure that it can update GUI variables using invoke methods
        public void log(string msg, LogLevel level = LogLevel.Info)
        {
            if (logTextBox.InvokeRequired == true)
            {
                logTextBox.Invoke(new MethodInvoker(delegate { logThreadSafe(msg, level); }));
            }
            else
            {
                logThreadSafe(msg, level);
            }
        }

        // function updates GUI, therefore, must be called on same thread
        private void logThreadSafe(string msg, LogLevel level)
        {
            // add 'enter' only if not first
            if (string.IsNullOrEmpty(logTextBox.Text) == false) logTextBox.AppendText(Environment.NewLine);

            if (level == LogLevel.Error)
            {
                logTextBox.SelectionColor = Color.Red;
            }

            if (level == LogLevel.Info)
            {
                logTextBox.SelectionColor = Color.Black;
            }

            // needed for colors
            logTextBox.SelectionStart = logTextBox.TextLength;
            logTextBox.SelectionLength = 0;

            // add text
            logTextBox.AppendText(string.Format("{0}  {1}", DateTime.Now.ToString("HH:mm:ss.fff"), msg));

            logTextBox.SelectionColor = logTextBox.ForeColor;
            //logTextBox.ScrollToCaret();
            logTextBox.Refresh();
        }
        #endregion

        // function generates output file based on user's demangss
        private void report_btn_Click(object sender, EventArgs e)
        {
            // filter outdated records
            DateTime startDate = generateStartDate();

            log(string.Format("Chosen start date: {0}", startDate.ToString()));

            // initialize DB
            dbLoader.initDb();

            // sort by times
            dbLoader.filterAndSortList(startDate);

            // verify we have records to export
            if (dbLoader.getNumberOfRecords() == 0)
            {
                log(string.Format("There are 0 records after filtering"), LogLevel.Error);
                return;
            }

            // generate output string
            string[] outStr = dbLoader.generateOutputString();

            // write to file
            string fileName = Path.GetFileNameWithoutExtension(inputFilePath);
            string filePath = Path.GetDirectoryName(inputFilePath);
            string outputPath = string.Format(@"{0}\{1}_res.txt", filePath, fileName);

            try
            {
                // delete old file of exists
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                File.WriteAllLines(outputPath, outStr);

                _Form.log(string.Format("Output file was succssfully generated with {0} records", 
                          dbLoader.getNumberOfRecords()));

                generateHyperLinkForResFile(outputPath);
            }
            catch (Exception ex)
            {
                _Form.log(string.Format("Exception occurred while generating output file"), LogLevel.Error);
                _Form.log(string.Format("Error: {0}", ex.Message), LogLevel.Error);
            }
        }

        // function generates a link of the generated res fileSS
        private void generateHyperLinkForResFile(string outputPath)
        {
            _Form.log(string.Empty);

            LinkLabel link = new LinkLabel();
            link.Text = outputPath;
            link.LinkClicked += Link_LinkClicked;
            LinkLabel.Link data = new LinkLabel.Link();
            data.LinkData = outputPath;
            link.Links.Add(data);
            link.AutoSize = true;
            link.Location = logTextBox.GetPositionFromCharIndex(logTextBox.TextLength);
            logTextBox.Controls.Add(link);
            logTextBox.AppendText(link.Text);
            logTextBox.SelectionStart = logTextBox.TextLength;
        }

        // funciton opens the link to the generated TXT file
        private void Link_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var target = e.Link.LinkData;
            System.Diagnostics.Process.Start(target.ToString());
        }

        // function creates a date based on two date picker GUI controls
        private DateTime generateStartDate()
        {
            return new DateTime(datePicker.Value.Year,
                                datePicker.Value.Month,
                                datePicker.Value.Day,
                                timePicker.Value.Hour,
                                timePicker.Value.Minute,
                                0 /* seconds */);
        }
    }
}
