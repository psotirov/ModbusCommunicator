using System;
using System.IO.Ports;
using Modbus_Communicator;
using MeasurementData;

namespace Modbus_Master
{
    public class CommunicationData
    {
        private ConfigData configuration;

        public bool IsEnabledAdvancedMenus
        {
            get { return this.configuration.AdvancedMenus; }
        }

        public string ComPortName
        {
            get { return this.configuration.PortName; }
            set { this.configuration.PortName = value; }
        }

        public int ComPortBaudRate
        {
            get { return this.configuration.PortBaudRate; }
            set { this.configuration.PortBaudRate = value; }
        }

        public int ComPortDataBits { get; private set; }
        public Parity ComPortParity { get; private set; }
        public StopBits ComPortStopBits { get; private set; }
        public string ComPortStatus
        {
            get { return this.comPort.modbusStatus; }
        }

        public int ModbusSlave
        {
            get { return this.configuration.ModbusSlave; }
            set { this.configuration.ModbusSlave = value; }
        }

        public int ModbusInterval
        {
            get { return this.configuration.ModbusInterval; }
            set { this.configuration.ModbusInterval = value; }
        }
        
        public string DbServer
        {
            get { return this.configuration.MySqlServer; }
            set { this.configuration.MySqlServer = value; }
        }

        public string DbDatabase
        {
            get { return this.configuration.MySqlDatabase; }
            set { this.configuration.MySqlDatabase = value; }
        }

        public string DbUser
        {
            get { return this.configuration.MySqlUser; }
            set { this.configuration.MySqlUser = value; }
        }

        public string DbPassword
        {
            get { return this.configuration.MySqlPassword; }
            set { this.configuration.MySqlPassword = value; }
        }

        public string DBTableName
        {
            get { return this.configuration.MySqlTable; }
        }

        public string ExtFileName
        {
            get { return this.configuration.ModbusDataFile; }
        }

        public int DataPackageLength
        {
            get { return this.configuration.MySqlDataPackage; }
        }

        public string TrimLogFile
        {
            get { return this.configuration.TrimLogFile; }
        }

        public int DataTrimRate
        {
            get { return this.configuration.TrimmingRate; }
        }

        public string TestCommand { get; set; }

        private ModbusRtu comPort;

        public CommunicationData()
        {
            this.configuration = new ConfigData();
            this.ComPortDataBits = 8;
            this.ComPortStopBits = StopBits.One;

            this.comPort = new ModbusRtu();
        }

        public void LoadConfiguration()
        {
            this.configuration.ReadConfigFile();
        }

        public void SaveConfiguration()
        {
            this.configuration.SaveConfigFile();
        }

        public void SetParity(string parity)
        {
            switch (parity.ToLower())
            {
                case "odd":
                    this.ComPortParity = Parity.Odd;
                    this.configuration.PortParity = "odd";
                    this.ComPortStopBits = StopBits.One;
                    break;
                case "even":
                    this.ComPortParity = Parity.Even;
                    this.configuration.PortParity = "even";
                    this.ComPortStopBits = StopBits.One;
                    break;
                default:
                    this.ComPortParity = Parity.None;
                    this.configuration.PortParity = "none";
                    // this.ComPortStopBits = StopBits.Two; // as per Modbus Specificaton
                    this.ComPortStopBits = StopBits.One; // working fine on PC COM ports
                    break;
            }
        }

        public bool TestPort()
        {
            var testPort = new ModbusRtu();
            var result = testPort.Open(
                this.ComPortName,
                this.ComPortBaudRate,
                this.ComPortDataBits,
                this.ComPortParity,
                this.ComPortStopBits);

            if (result)
            {
                result = testPort.Close();
            }

            return result;
        }

        public bool TestCommunication()
        {
            var result = this.comPort.Open(
                this.ComPortName,
                this.ComPortBaudRate,
                this.ComPortDataBits,
                this.ComPortParity,
                this.ComPortStopBits);
            if (result)
            {
                ushort testValue = 0xABCD;
                byte[] retValue = null;
                result = this.comPort.SendFc08((byte)this.ModbusSlave, (ushort)0, testValue, ref retValue);
                ushort value = (ushort)((retValue[0] << 8) + retValue[1]);
                var closeResult = this.comPort.Close();
                result = (result && closeResult && value == testValue);
            }
 
            return result;
        }

        public string RunCommand(int command, int start, int length)
        {
            var response = "";
            var result = this.comPort.Open(
                this.ComPortName,
                this.ComPortBaudRate,
                this.ComPortDataBits,
                this.ComPortParity,
                this.ComPortStopBits);

            byte[] responseData = null;
            if (result)
            {
                switch (command)
                {
                    case 2:
                        result = this.comPort.SendFc02((byte)this.ModbusSlave, (ushort)start, (ushort)length, ref responseData);
                        break;
                    case 4:
                        result = this.comPort.SendFc04((byte)this.ModbusSlave, (ushort)start, (ushort)length, ref responseData);
                        break;
                    case 8:
                        result = this.comPort.SendFc08((byte)this.ModbusSlave, (ushort)start, (ushort)length, ref responseData);
                        break;
                }

                this.comPort.Close();

                if (result)
                {
                    foreach (var item in responseData)
                    {
                        response += Convert.ToString(item, 16).ToUpper().PadLeft(2, '0') + " ";                        
                    }
                }
            }

            if (!result)
            {
                response = this.comPort.modbusStatus;
            }

            return response;
        }

        public Measurement GetMeasures()
        {
            //// Test
            //var b2 = new byte[] { 0x3d, 0x3d, 0x0e, 0x8f };
            //var b1 = new byte[] { 0x41, 0xa0, 0x1e, 0x1f };
            //if (BitConverter.IsLittleEndian)
            //{
            //    Array.Reverse(b1, 0, b1.Length);
            //    Array.Reverse(b2, 0, b2.Length);
            //}
            //float f1 = BitConverter.ToSingle(b1, 0);
            //float f2 = BitConverter.ToSingle(b2, 0);

            //if (DateTime.Now.Second > 50) f1 = f1 * 1.5f;
            //return new Measurement(f1, f2);

            var result = this.comPort.Open(
                this.ComPortName,
                this.ComPortBaudRate,
                this.ComPortDataBits,
                this.ComPortParity,
                this.ComPortStopBits);

            if (!result)
            {
                return new Measurement();
            }

            byte[] responseData = null;

            // read CO2 sequence - 4 bytes from registers 3001 and 3002  
            result = result && this.comPort.SendFc04((byte)this.ModbusSlave, 0, 2, ref responseData);
            if (!result || responseData.Length != 4)
            {
                this.comPort.Close();
                return new Measurement();
            }
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(responseData, 0, responseData.Length);
            }
            float co2 = BitConverter.ToSingle(responseData, 0);

            // read O2 sequence - 4 bytes from registers 3003 and 3004  
            result = result && this.comPort.SendFc04((byte)this.ModbusSlave, 2, 2, ref responseData);
            this.comPort.Close();

            if (!result || responseData.Length != 4)
            {
                return new Measurement();
            }
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(responseData, 0, responseData.Length);
            }
            float o2 = BitConverter.ToSingle(responseData, 0);

            return new Measurement(o2, co2);
        }
    }
}
