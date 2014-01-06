using System;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using MySql.Data.MySqlClient;

namespace MeasurementData
{
    public class DataReadings:INotifyPropertyChanged
    {
        public const int NumberOfElementsToView = 9;
        public int MaxBufferLength = 58;
        public double TrimValuesRange = 1.99;

        public string MySqlConnectionString {get; set;}
        public string MySqlTableName {get; set;}
        public string ExternalTextFileName { get; set; }
        public string TrimValuesTextFileName { get; set; }
        public string ErrorMessage;
        private int invalidValuesCounter;

        // Declare the event 
        public event PropertyChangedEventHandler PropertyChanged;

        public Measurement Current { get; private set; }
        public Queue<Measurement> Buffer { get; private set; }
        public IEnumerable<Measurement> LastElements
        {
            get
            {
                return this.Buffer
                    .Skip(Math.Max(0, this.Buffer.Count - NumberOfElementsToView))
                    .Take(NumberOfElementsToView)
                    .AsEnumerable();
            }
        }

        public DataReadings()
        {
            this.Current = new Measurement();
            this.Buffer = new Queue<Measurement>();
            this.invalidValuesCounter = 0;
            // TEST
            //for (int i = 0; i < 10; i++)
            //{
            //    this.Buffer.Enqueue(new Measurement(i + 20.5f, i + 0.1f));
            //}
        }

        public void Add(Measurement newMeasure)
        {
            if (IsCurrentValueValid() || this.invalidValuesCounter > 5)
            {
                this.Buffer.Enqueue(this.Current);
                this.OnPropertyChanged("LastElements");
                this.invalidValuesCounter = 0;
            }
            else if (this.Current.CO2Value > 0 && this.Current.O2Value > 0)
            {
                // invalid out of range value should be trimmed but also logged
                this.AppendCurrentToLogFile();
                this.invalidValuesCounter++;
            }

            this.Current = newMeasure;
            this.OnPropertyChanged("Current");


            if (this.Buffer.Count > MaxBufferLength)
            {
                this.ReleaseBuffer();
            }
        }


        public void ReleaseBuffer()
        {
            var data = new List<Measurement>();
            while (this.Buffer.Count > NumberOfElementsToView)
            {
                data.Add(this.Buffer.Dequeue());
            }

            this.SendDataToMySql(data);
            this.AppendToFile(data);
        }

        private void SendDataToMySql(List<Measurement> data)
        {
            //var commandString = "INSERT INTO gazanalyzer VALUES (NULL, '0.1','20.0','2013-9-13 16:37:00');";
            var commandString = new System.Text.StringBuilder("INSERT INTO " + MySqlTableName + " VALUES ");
            for (int i = 0; i < data.Count; i++)
            {
                commandString.Append("(NULL, '" +
                    data[i].CO2Value.ToString("F3") + "', '" +
                    data[i].O2Value.ToString("F3") + "', '" +
                    data[i].Timestamp.ToString("yyyy-MM-dd HH:mm:ss") + ((data.Count - i > 1) ? "')," : "');"));
            }

            var dbCon = new MySqlConnection(MySqlConnectionString);
            try
            {
                dbCon.Open();
                using (dbCon)
                {
                    var cmdInsertData = new MySqlCommand(commandString.ToString(), dbCon);
                    cmdInsertData.ExecuteNonQuery();
                }
            }
            catch(MySqlException e)
            {
                this.ErrorMessage = e.Message;
            }

            dbCon.Close();
        }

        private bool IsCurrentValueValid()
        {
            // zero value is invalid
            if (this.Current.CO2Value == 0 && this.Current.O2Value == 0) return false;

            // if there is no other value to compare - it is valid
            if (this.Buffer.Count == 0 || this.TrimValuesRange < 0.01) return true;

            var lastValue = this.Buffer.Last();
            double O2delta = Math.Abs(lastValue.O2Value - this.Current.O2Value);
            double CO2delta = Math.Abs(lastValue.CO2Value - this.Current.CO2Value);

            bool isValid = (O2delta < this.TrimValuesRange && CO2delta < this.TrimValuesRange);

            return isValid;
        }

        private void AppendToFile(List<Measurement> data)
        {
            try
            {
                using (var sw = new StreamWriter(ExternalTextFileName, true, System.Text.Encoding.ASCII))
                {
                    foreach (var item in data)
                    {
                        sw.WriteLine("{0}, {1:F3}, {2:F3}",
                            item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                            item.CO2Value,
                            item.O2Value);
                    }
                }
            }
            catch(IOException e)
            {
            }
        }

        private void AppendCurrentToLogFile()
        {
            try
            {
                using (var sw = new StreamWriter(this.TrimValuesTextFileName, true, System.Text.Encoding.ASCII))
                {
                    sw.WriteLine("{0}, {1:F3}, {2:F3}",
                        this.Current.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        this.Current.CO2Value,
                        this.Current.O2Value);
                }
            }
            catch (IOException e)
            {
            }
        }

        // Create the OnPropertyChanged method to raise the event 
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
