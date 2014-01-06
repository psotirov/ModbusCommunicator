using System;
using System.IO.Ports;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MeasurementData;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Modbus_Master
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CommunicationData modbusPort;
        private CommunicationData testedPort;
        private DataReadings data;
        private DispatcherTimer dispatcherTimer;


        public MainWindow()
        {
            InitializeComponent();
            this.data = new DataReadings();
            this.DataContext = this.data;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Get a list of serial port names.
            string[] ports = null;
            try
            {
                ports = SerialPort.GetPortNames();
            }
            catch (Exception)
            {
                cbPortNames.Items.Add("No available COM Ports");
            }
            
            // Put each available port name into the ComboBox control. 
            foreach (string port in ports)
            {
                var cbItem = new ComboBoxItem();
                cbItem.Content = port;
                cbPortNames.Items.Add(cbItem);
            }

            // read configuration file
            modbusPort = new CommunicationData();
            modbusPort.LoadConfiguration();

            // set visual items if advanced menus are enabled and available
            if (!modbusPort.IsEnabledAdvancedMenus || tbiConnection == null)
            {
                // tbiCommunication.Visibility = Visibility.Hidden;
                // tbiConnection.Visibility = Visibility.Hidden;
                return;
            }
            
            tbiCommunication.Visibility = Visibility.Visible;
            tbiConnection.Visibility = Visibility.Visible;

            var portIdx = Array.IndexOf(ports, modbusPort.ComPortName);
            cbPortNames.SelectedIndex = (portIdx>=0)?portIdx:0;

            tbSlaveId.Text = modbusPort.ModbusSlave.ToString();
            tbInterval.Text = modbusPort.ModbusInterval.ToString();
            tbDbServer.Text = modbusPort.DbServer;
            tbDbName.Text = modbusPort.DbDatabase;
            tbDbUser.Text = modbusPort.DbUser;
            tbDbPassword.Text = modbusPort.DbPassword;
        }

        private void ButtonTest_Click(object sender, RoutedEventArgs e)
        {
            var portName = (cbPortNames.SelectedItem != null) ? ((ComboBoxItem)cbPortNames.SelectedItem).Content.ToString() : "";
            var baudRateString = (cbPortSpeeds.SelectedItem != null) ? ((ComboBoxItem)cbPortSpeeds.SelectedItem).Content.ToString() : "";
            var baudRate = int.Parse(baudRateString);
            var parity = (cbPortParity.SelectedItem != null) ? ((ComboBoxItem)cbPortParity.SelectedItem).Content.ToString() : "";

            var testPort = new CommunicationData();
            testPort.ComPortName = portName;
            testPort.ComPortBaudRate = baudRate;
            testPort.SetParity(parity);
            var test = testPort.TestPort();
            if (lblConnResult != null)
            {
                lblConnResult.Content = (test) ? "Communication OK" : "Communication Failed";
            }

            testedPort = (test) ? testPort : null;
            if (btnApply != null)
            {
                btnApply.IsEnabled = test;
            }
        }

        private void ButtonApply_Click(object sender, RoutedEventArgs e)
        {
            bool needToSaveConfig = false;
            if (testedPort != null && tbSlaveId != null)
            {
                modbusPort.ComPortName = testedPort.ComPortName;
                modbusPort.ComPortBaudRate = testedPort.ComPortBaudRate;
                modbusPort.SetParity(testedPort.ComPortParity.ToString());
                var slaveId = 0;
                int.TryParse(tbSlaveId.Text, out slaveId);
                if (slaveId > 0 && slaveId < 248 && modbusPort.ModbusSlave != slaveId)
                {
                    modbusPort.ModbusSlave = slaveId;
                    needToSaveConfig = true;
                }

                if (tbDbServer != null && modbusPort.DbServer != tbDbServer.Text)
                {
                    modbusPort.DbServer = tbDbServer.Text.Trim();
                    needToSaveConfig = true;
                }

                if (tbDbName != null && modbusPort.DbDatabase != tbDbName.Text)
                {
                    modbusPort.DbDatabase = tbDbName.Text.Trim();
                    needToSaveConfig = true;
                }

                if (tbDbUser != null && modbusPort.DbUser != tbDbUser.Text)
                {
                    modbusPort.DbUser = tbDbUser.Text.Trim();
                    needToSaveConfig = true;
                }
                
                if (tbDbPassword != null && modbusPort.DbPassword != tbDbPassword.Text)
                {
                    modbusPort.DbPassword = tbDbPassword.Text.Trim();
                    needToSaveConfig = true;
                }
            }

            if (needToSaveConfig)
	        {
		        modbusPort.SaveConfiguration();
	        }

            testedPort = null;
            DisableApplyButton();
        }

        private void SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DisableApplyButton();
        }


        private void DisableApplyButton()
        {
            if (btnApply != null)
            {
                btnApply.IsEnabled = false;
            }

            if (lblConnResult != null)
            {
                lblConnResult.Content = "";
            }
        }

        private void ButtonTestConnection_Click(object sender, RoutedEventArgs e)
        {
            if (lblCommResult == null)
            {
                return;
            }

            modbusPort.TestCommunication();
            lblCommResult.Content = "Connection status: " + modbusPort.ComPortStatus;  
        }

        private void TabItemCommunication_GotFocus(object sender, RoutedEventArgs e)
        {
            if (lblCommResult != null)
            {
                lblCommResult.Content = "Connection status: " + modbusPort.ComPortStatus;
            }
        }

        private void ButtonRunCommand_Click(object sender, RoutedEventArgs e)
        {
            var commandString = (cbModbusCommand.SelectedItem != null) ? ((ComboBoxItem)cbModbusCommand.SelectedItem).Content.ToString() : "00";
            int command = int.Parse(commandString.Substring(0, 2));

            int start = 0;
            int length = 0;
            try
            {
                start = Convert.ToUInt16(tbCommandStart.Text, 16);
                length = Convert.ToUInt16(tbCommandLength.Text, 16);
            }
            catch (Exception)
            {
                // wrong number
                if (tbCommandResult != null)
                {
                    tbCommandResult.Text = "Wrong hexadecimal values in start and/or length fields";
                }
                return;   
            }

            string result = modbusPort.RunCommand(command, start, length);
            if (tbCommandResult != null)
            {
                tbCommandResult.Text = result;
            }
        }

        private void ButtonStartMeasure_Click(object sender, RoutedEventArgs e)
        {
            int interval = 0;
            if (tbInterval == null || !int.TryParse(tbInterval.Text, out interval) || interval <= 0)
            {
                tbInterval.Text = "? " + modbusPort.ModbusInterval.ToString();
                return;
            }

            if (interval != modbusPort.ModbusInterval)
            {
                modbusPort.ModbusInterval = interval;
                modbusPort.SaveConfiguration();
            }

            if (btnStop != null)
            {
                btnStop.IsEnabled = true;
            }

            this.data.MySqlConnectionString = 
                "Server=" + modbusPort.DbServer + 
                ";Database=" + modbusPort.DbDatabase + 
                ";Uid="+modbusPort.DbUser +
                ";Pwd="+ modbusPort.DbPassword + ";";

            this.data.MySqlTableName = modbusPort.DBTableName;
            this.data.ExternalTextFileName = modbusPort.ExtFileName;
            this.data.TrimValuesTextFileName = modbusPort.TrimLogFile;
            this.data.MaxBufferLength = modbusPort.DataPackageLength + DataReadings.NumberOfElementsToView - 1;
            this.data.TrimValuesRange = modbusPort.DataTrimRate/100.0;
 
            btnStart.IsEnabled = false;
            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(OnIntervalReached);
            dispatcherTimer.Interval = new TimeSpan(interval * 10000000); // one second is 10 000 000 ticks
            dispatcherTimer.Start();
        }

        private void ButtonStopMeasure_Click(object sender, RoutedEventArgs e)
        {
            if (btnStart != null)
            {
                btnStart.IsEnabled = true;
            }

            btnStop.IsEnabled = false;
            if (dispatcherTimer != null && dispatcherTimer.IsEnabled)
            {
                dispatcherTimer.Stop();
                dispatcherTimer.Tick -= OnIntervalReached;
            }
        }

        private void OnIntervalReached(object sender, EventArgs e)
        {
            var readings = this.modbusPort.GetMeasures();
            if (readings != null && this.data != null)
            {
                this.data.Add(readings);
            }

            if (this.data.ErrorMessage != null)
            {
                MessageBox.Show(this.data.ErrorMessage);
                this.data.ErrorMessage = null;
            }
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            if (contactMail != null)
            {
                Uri uri = contactMail.NavigateUri;

                if (uri != null)
                {
                    System.Diagnostics.Process.Start(uri.ToString());
                }
            }
        }
    }
}
