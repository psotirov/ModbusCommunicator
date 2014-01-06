using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Modbus_Master
{
    public class ConfigData
    {
        private const string ConfigFile = "Config.xml";

        public bool AdvancedMenus { get; set; }

        public string PortName { get; set; }
        public int PortBaudRate { get; set; }
        public string PortParity { get; set; }

        public int ModbusSlave { get; set; }
        public string ModbusDataFile { get; set; }
        public int ModbusInterval { get; set; }
        public int TrimmingRate { get; set; }
        public string TrimLogFile { get; set; }

        public string MySqlServer { get; set; }
        public string MySqlDatabase { get; set; }
        public string MySqlUser { get; set; }
        public string MySqlPassword { get; set; }
        public string MySqlTable { get; set; }
        public int MySqlDataPackage { get; set; }

        public ConfigData()
        {
            // Deafult values if no configuration file or item is missing
            this.AdvancedMenus = true;

            this.PortName = "COM1";
            this.PortBaudRate = 19200;
            this.PortParity = "none";

            this.ModbusSlave = 1;
            this.ModbusDataFile = "measurements.txt";
            this.TrimLogFile = "trimvalues.txt";
            this.ModbusInterval = 5;
            this.TrimmingRate = 30;

            this.MySqlServer = "localhost";
            this.MySqlUser = "root";
            this.MySqlPassword = "";
            this.MySqlDatabase = "test";
            this.MySqlTable = "measurements";
            this.MySqlDataPackage = 5;
        }

        public void ReadConfigFile()
        {
            XElement configDoc = null;
            try
            {
                configDoc = XDocument.Load(ConfigFile).Root;
            }
            catch (IOException)
            {
                return;
            }

            // checks for advanced menus flag attribute
            this.AdvancedMenus = 
                (configDoc.HasAttributes &&
                configDoc.Attribute("advanced") != null &&
                configDoc.Attribute("advanced").Value == "1");

            // reads "Port" section - com port parameters
            var portData = configDoc.Element("port");
            if (portData != null)
            {
                if (portData.Element("name") != null)
                {
                    this.PortName = portData.Element("name").Value;
                }

                if (portData.Element("speed") != null)
                {
                    int speed = 0;
                    int.TryParse(portData.Element("speed").Value, out speed);
                    if (speed > 0)
	                {
                        this.PortBaudRate = speed;		 
	                }
                }

                if (portData.Element("parity") != null)
                {
                    this.PortName = portData.Element("name").Value;
                }
            }

            // reads "Modbus" secton - communication details
            var modbusData = configDoc.Element("modbus");
            if (modbusData != null)
            {
                if (modbusData.Element("slave") != null)
                {
                    int slave = 0;
                    int.TryParse(modbusData.Element("slave").Value, out slave);
                    if (slave > 0 && slave < 248)
                    {
                        this.ModbusSlave = slave;
                    }
                }

                if (modbusData.Element("interval") != null)
                {
                    int interval = 0;
                    int.TryParse(modbusData.Element("interval").Value, out interval);
                    if (interval > 0)
                    {
                        this.ModbusInterval = interval;
                    }
                }

                if (modbusData.Element("trimrate") != null)
                {
                    int interval = 0;
                    int.TryParse(modbusData.Element("trimrate").Value, out interval);
                    if (interval >= 0 && interval < 10000)
                    {
                        this.TrimmingRate = interval;
                    }
                }

                if (modbusData.Element("externalfile") != null)
                {
                    var filename =  modbusData.Element("externalfile").Value;
                    if (filename.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
                    {
                        this.ModbusDataFile = filename;
                    }
                }

                if (modbusData.Element("trimfile") != null)
                {
                    var filename = modbusData.Element("trimfile").Value;
                    if (filename.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
                    {
                        this.TrimLogFile = filename;
                    }
                }
            }

            // reads "MySQL" section - MySQL database connection
            var mySqlData = configDoc.Element("mysql");
            if (mySqlData != null)
            {
                if (mySqlData.Element("server") != null)
                {
                    this.MySqlServer = mySqlData.Element("server").Value;
                }

                if (mySqlData.Element("database") != null)
                {
                    this.MySqlDatabase = mySqlData.Element("database").Value;
                }

                if (mySqlData.Element("user") != null)
                {
                    this.MySqlUser = mySqlData.Element("user").Value;
                }

                if (mySqlData.Element("password") != null)
                {
                    this.MySqlPassword = mySqlData.Element("password").Value;
                }

                if (mySqlData.Element("table") != null)
                {
                    this.MySqlTable = mySqlData.Element("table").Value;
                }

                if (mySqlData.Element("datapackage") != null)
                {
                    int interval = 0;
                    int.TryParse(mySqlData.Element("datapackage").Value, out interval);
                    if (interval > 0)
                    {
                        this.MySqlDataPackage = interval;
                    }
                }
            }
        }

        public void SaveConfigFile()
        {
            var configDoc = new XDocument( 
                new XDeclaration("1.0","utf-8","yes"),
                new XElement("configuration", 
                    new XAttribute("advanced", (this.AdvancedMenus)?"1":"0"),
                    new XElement("port",
                        new XElement("name", this.PortName),
                        new XElement("speed", this.PortBaudRate),
                        new XElement("parity", this.PortParity)),
                    new XElement("modbus",
                        new XElement("slave", this.ModbusSlave),
                        new XElement("externalfile", this.ModbusDataFile),
                        new XElement("trimfile", this.TrimLogFile),
                        new XElement("trimrate", this.TrimmingRate),
                        new XElement("interval", this.ModbusInterval)),
                    new XElement("mysql",
                        new XElement("server", this.MySqlServer),
                        new XElement("database", this.MySqlDatabase),
                        new XElement("user", this.MySqlUser),
                        new XElement("password", this.MySqlPassword),
                        new XElement("table", this.MySqlTable),
                        new XElement("datapackage", this.MySqlDataPackage))));

            try
            {
                configDoc.Save(ConfigFile);
            }
            catch (IOException)
            {
            } 
        }
    }
}
