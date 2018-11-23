using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using static SugerParser.FormEntry;

namespace SugerParser
{
    // class responsible for converting the input file into a list of GloucoseData records
    class DbLoader
    {
        // private members
        private List<GlucoseData>   glucoseDataList     = new List<GlucoseData>();
        private List<GlucoseData>   origDataList        = new List<GlucoseData>();
        private string              dateParseFormat     = "yyyy/MM/dd HH:mm";
        private string              userName            = string.Empty;
        private string              columnNames         = string.Empty;
        private const int           ID_INDEX            = 0;
        private const int           TIME_INDEX          = 1;
        private const int           RECORD_TYPE_INDEX   = 2;
        private const int           GLUCOS_DATA_INDEX   = 3;
        private bool                bLoadSuccess        = false;

        // ctor
        public DbLoader(string filePath)
        {
            if (File.Exists(filePath) == false)
            {
                _Form.log(string.Format("{0} - file not found, check first argument", filePath), LogLevel.Error);
                return;
            }

            if (parseFileToDb(filePath) == false)
            {
                _Form.log(string.Format("{0} - file parse failed", filePath), LogLevel.Error);
                return;
            }

            _Form.log(string.Format("{0} - file was successfully parsed", filePath));

            _Form.log(string.Format("There are {0} records since {1} till {2}", 
                      glucoseDataList.Count(),
                      glucoseDataList.First().recordTime.ToString(),
                      glucoseDataList.Last().recordTime.ToString()));

            // since we dont have a way to fail the ctor, we'll use this flag as indication
            bLoadSuccess = true;
        }

        // function returns the current number of records
        public int getNumberOfRecords() { return glucoseDataList.Count(); }

        // function returns true in case the DbLoader has completed successfully
        // and file was successfully parsed
        public bool isLoadCompleteSuccessfully() { return bLoadSuccess; }

        // function parses the input file into local DB
        // assumption: filePath is valid
        private bool parseFileToDb(string filePath)
        {
            int         counter = 0;
            string      line    = string.Empty;
            string[]    tmpArr;

            // read the file and parse line by line
            StreamReader file = new StreamReader(filePath);
            while ((line = file.ReadLine()) != null)
            {
                // format:
                // [0]    user name
                // [1]    columns names
                // [2..n] data
                if (counter > 1)
                {
                    tmpArr = line.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    // avoid invalid record (no need to assert here)
                    if (validRecord(tmpArr) == true)
                    {
                        // protect by try/catch due to external file parsing
                        try
                        {
                            GlucoseData glucoseData = new GlucoseData();

                            // ID
                            glucoseData.id = Int32.Parse(tmpArr[ID_INDEX]);

                            // date
                            glucoseData.recordTime = DateTime.ParseExact(tmpArr[TIME_INDEX].ToString(), dateParseFormat, CultureInfo.InvariantCulture);

                            // safe since validRecord validates data integrity
                            RecordType recordType = (RecordType)Int32.Parse(tmpArr[RECORD_TYPE_INDEX]);
                            glucoseData.recordType = recordType;
                            int glucoseValue = Int32.Parse(tmpArr[GLUCOS_DATA_INDEX]);

                            switch (recordType)
                            {
                                case RecordType.AutoScan:
                                    glucoseData.historicGlucose = glucoseValue;
                                    break;
                                case RecordType.ManualScan:
                                    glucoseData.scanGlucose = glucoseValue;
                                    break;
                                case RecordType.StripScan:
                                    glucoseData.stripGlocuse = glucoseValue;
                                    break;
                            }

                            // add the record into the global list
                            glucoseDataList.Add(glucoseData);
                        }
                        catch (Exception e)
                        {
                            _Form.log(string.Format("Record parsing failed, see exception"), LogLevel.Error);
                            _Form.log(string.Format("Error: {0}", e.Message), LogLevel.Error);
                        }
                    }
                }
                else
                {
                    // store first two lines, since will be needed later (in report generation)
                    if (counter == 0)
                    {
                        // the first line has the user name
                        userName = line;
                    }
                    else
                    {
                        // the second line contanes the column names
                        columnNames = line;
                    }
                }

                counter++;
            }

            file.Close();

            _Form.log(string.Format("There were {0} lines found", counter));
            _Form.log(string.Format("There were {0} valid records found", glucoseDataList.Count()));

            // save a copy of the list
            origDataList = new List<GlucoseData>(glucoseDataList);

            return true;
        }

        // fucntion clones the original DB into the glucoseDataList
        // so generate report can be ran multiple times
        // (glucoseDaraList is being modified dueing report generation)
        public void initDb()
        {
            glucoseDataList = new List<GlucoseData>(origDataList);
        }

        // function generates string based on glucoseDataList
        public string[] generateOutputString()
        {
            List<string> outStr = new List<string>();

            // add the user and column names
            outStr.Add(userName);
            outStr.Add(columnNames);

            // add all data from the list
            foreach (GlucoseData item in glucoseDataList)
            {
                // the needed vaue can be in one of the 3 records (historic/scan/strip)
                // search for the one having it and use it
                int glucoseVal = item.historicGlucose + item.scanGlucose + item.stripGlocuse;

                // use hardcoded "manual scan", since this is required by app
                outStr.Add(string.Format("{0}\t{1}\t{2}\t\t{3}\t\t\t\t\t\t\t\t\t\t\t\t\t\t",
                           item.id,
                           item.recordTime.ToString(dateParseFormat),
                           (int)RecordType.ManualScan,
                           glucoseVal));
            }

            return outStr.ToArray();
        }

        // function filters out all outdated recordes and sorts by date
        public void filterAndSortList(DateTime startDate)
        {
            glucoseDataList = glucoseDataList.Where(x => x.recordTime >= startDate)
                                             .OrderBy(x => x.recordTime)
                                             .ToList();
        }

        // function returns true in case the record is valid
        // format:
        // [0] ID
        // [1] time
        // [2] record type - must be one of the enum RecordType
        // [3] glocuse data
        private bool validRecord(string[] tmpArr)
        {
            // check length
            if (tmpArr.Count() != 4)
            {
                return false;
            }

            // check record type
            int recordTypeInt = -1;
            if (Int32.TryParse(tmpArr[RECORD_TYPE_INDEX], out recordTypeInt) == true)
            {
                if (Enum.IsDefined(typeof(RecordType), recordTypeInt) == true)
                {
                    // found
                    return true;
                }
            }

            return false;
        }

        // enum holding the valid record data types
        // types which are not here are considered to be invalid
        public enum RecordType
        {
            AutoScan = 0,
            ManualScan = 1,
            StripScan = 2
        }

        // record data
        public class GlucoseData
        {
            public GlucoseData()
            {
                // init all variables
                id              = 0;
                recordTime      = default(DateTime);
                recordType      = RecordType.AutoScan;
                historicGlucose = 0;
                scanGlucose     = 0;
                stripGlocuse    = 0;
            }

            public int id;
            public DateTime recordTime;
            public RecordType recordType;
            public int historicGlucose;     // auto scan    - index 3
            public int scanGlucose;         // manual scan  - index 4
            public int stripGlocuse;        // strip scan   - index 12
        }
    }
}
