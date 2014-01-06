using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;

namespace Modbus_Communicator
{
    public class ModbusRtu
    {
        private SerialPort comPort;
        public string modbusStatus;

        public ModbusRtu()
        {
            this.comPort = new SerialPort();
            this.modbusStatus = "all ports closed";
        }
 
        public bool Open(string portName, int baudRate, int databits, Parity parity, StopBits stopBits)
        {
            //Ensure port isn't already opened:
            if (!this.comPort.IsOpen)
            {
                //Assign desired settings to the serial port:
                this.comPort.PortName = portName;
                this.comPort.BaudRate = baudRate;
                this.comPort.DataBits = databits;
                this.comPort.Parity = parity;
                this.comPort.StopBits = stopBits;
                //These timeouts are default and cannot be editted through the class at this point:
                this.comPort.ReadTimeout = 1000;
                this.comPort.WriteTimeout = 1000;

                try
                {
                    this.comPort.Open();
                }
                catch (Exception err)
                {
                    this.modbusStatus = "Error opening " + portName + ": " + err.Message;
                    return false;
                }
                this.modbusStatus = portName + " opened successfully";
                return true;
            }

            this.modbusStatus = portName + " already opened";
            return false;
        }

        public bool Close()
        {
            //Ensure port is opened before attempting to close:
            if (this.comPort.IsOpen)
            {
                try
                {
                    this.comPort.Close();
                }
                catch (Exception err)
                {
                    this.modbusStatus = "Error closing " + this.comPort.PortName + ": " + err.Message;
                    return false;
                }
                // this.modbusStatus = this.comPort.PortName + " closed successfully";
                return true;
            }

            this.modbusStatus = this.comPort.PortName + " is not open";
            return false;
        }

        private void GetCRC(byte[] message, ref byte[] CRC)
        {
            //Function expects a modbus message of any length as well as a 2 byte CRC array in which to 
            //return the CRC values:

            ushort CRCFull = 0xFFFF;
            byte CRCHigh = 0xFF, CRCLow = 0xFF;
            char CRCLSB;

            for (int i = 0; i < (message.Length) - 2; i++)
            {
                CRCFull = (ushort)(CRCFull ^ message[i]);

                for (int j = 0; j < 8; j++)
                {
                    CRCLSB = (char)(CRCFull & 0x0001);
                    CRCFull = (ushort)((CRCFull >> 1) & 0x7FFF);

                    if (CRCLSB == 1)
                        CRCFull = (ushort)(CRCFull ^ 0xA001);
                }
            }
            CRC[1] = CRCHigh = (byte)((CRCFull >> 8) & 0xFF);
            CRC[0] = CRCLow = (byte)(CRCFull & 0xFF);
        }

        private void BuildMessage(byte address, byte type, ushort start, ushort registers, ref byte[] message)
        {
            //Array to receive CRC bytes:
            byte[] CRC = new byte[2];

            message[0] = address;
            message[1] = type;
            message[2] = (byte)(start >> 8);
            message[3] = (byte)start;
            message[4] = (byte)(registers >> 8);
            message[5] = (byte)registers;

            GetCRC(message, ref CRC);
            message[message.Length - 2] = CRC[0];
            message[message.Length - 1] = CRC[1];
        }

        private bool CheckResponse(byte[] response)
        {
            //Perform a basic CRC check:
            byte[] CRC = new byte[2];
            GetCRC(response, ref CRC);
            if (CRC[0] == response[response.Length - 2] && CRC[1] == response[response.Length - 1])
                return true;
            else
                return false;
        }

        private void GetResponse(ref byte[] response)
        {
            //There is a bug in .Net 2.0 DataReceived Event that prevents people from using this
            //event as an interrupt to handle data (it doesn't fire all of the time).  Therefore
            //we have to use the ReadByte command for a fixed length as it's been shown to be reliable.
            for (int i = 0; i < response.Length; i++)
            {
                response[i] = (byte)(this.comPort.ReadByte());
            }
        }

        // MODBUS functions by number

        /// <summary>
        /// MODBUS function 02 - Read input status (On/Off of 1xxxx discrete inputs)
        /// </summary>
        /// <param name="address">slave controller address (1-247)</param>
        /// <param name="start">start address of the discrete inputs region 1xxxx</param>
        /// <param name="count">number of dicrete inputs to read (1yyyy - 1xxxx + 1)</param>
        /// <param name="values">result state (one bit per input: 0=Off, 1=On; from 1xxxx=bit0-byte0 to 1yyyy=bitM-byteCount)</param>
        /// <returns>true if successfull</returns>
        public bool SendFc02(byte address, ushort start, ushort count, ref byte[] values)
        {
            //Ensure port is open:
            if (this.comPort.IsOpen)
            {
                var resultLength = (count / 8) + ((count % 8 > 0) ? 1 : 0); // each discrete output holds 1 bit of the result
                //Clear in/out buffers:
                this.comPort.DiscardOutBuffer();
                this.comPort.DiscardInBuffer();
                //Function 2 request is always 8 bytes:
                byte[] message = new byte[8];
                //Function 2 response buffer: 
                byte[] response = new byte[5 + resultLength]; //Message is 1 addr + 1 fcn + 1 count + count * reg vals + 2 CRC
                //Build outgoing modbus message:
                BuildMessage(address, (byte)2, start, count, ref message);
                //Send modbus message to Serial Port:
                try
                {
                    this.comPort.Write(message, 0, message.Length);
                    GetResponse(ref response);
                }
                catch (Exception err)
                {
                    this.modbusStatus = "Error in read event: " + err.Message;
                    return false;
                }

                //Evaluate message:
                if (CheckResponse(response) && response[2] == resultLength)
                {
                    values = new byte[resultLength];
                    Array.Copy(response, 3, values, 0, resultLength);
 
                    this.modbusStatus = "Read successful";
                    return true;
                }

                this.modbusStatus = "CRC error";
                return false;
            }

            this.modbusStatus = "Serial port not open";
            return false;
        }

        /// <summary>
        /// MODBUS function 03 - Read holding registers (4xxxx address range - each register holds 2 bytes)
        /// </summary>
        /// <param name="address">slave controller address (1-247)</param>
        /// <param name="start">start address of the holding registers region 4xxxx</param>
        /// <param name="count">number of holding registers to read (1xxxx + 2 * count)</param>
        /// <param name="values">holding registers values (2 bytes per register)</param>
        /// <returns>true if successfull</returns>
        public bool SendFc03(byte address, ushort start, ushort count, ref byte[] values)
        {
            //Ensure port is open:
            if (this.comPort.IsOpen)
            {
                //Clear in/out buffers:
                this.comPort.DiscardOutBuffer();
                this.comPort.DiscardInBuffer();
                //Function 3 request is always 8 bytes:
                byte[] message = new byte[8];
                //Function 3 response buffer:
                byte[] response = new byte[5 + 2 * count]; //Message is 1 addr + 1 fcn + 1 count (16 bits each) + count * 2 * reg vals + 2 CRC
                //Build outgoing modbus message:
                BuildMessage(address, (byte)3, start, count, ref message);
                //Send modbus message to Serial Port:
                try
                {
                    this.comPort.Write(message, 0, message.Length);
                    GetResponse(ref response);
                }
                catch (Exception err)
                {
                    this.modbusStatus = "Error in read event: " + err.Message;
                    return false;
                }

                //Evaluate message:
                if (CheckResponse(response))
                {
                    //Return requested register values:
                    var length = response.Length - 5;
                    values = new byte[length];
                    Array.Copy(response, 3, values, 0, length);

                    this.modbusStatus = "Read successful";
                    return true;
                }

                this.modbusStatus = "CRC error";
                return false;
            }

            this.modbusStatus = "Serial port not open";
            return false;
        }

        /// <summary>
        /// MODBUS function 04 - Read input registers (3xxxx address range - each register holds 2 bytes)
        /// </summary>
        /// <param name="address">slave controller address (1-247)</param>
        /// <param name="start">start address of the holding registers region 4xxxx</param>
        /// <param name="count">number of input registers to read (1xxxx + 2 * count)</param>
        /// <param name="values">holding registers values (2 bytes per register)</param>
        /// <returns>true if successfull</returns>
        public bool SendFc04(byte address, ushort start, ushort count, ref byte[] values)
        {
            //Ensure port is open:
            if (this.comPort.IsOpen)
            {
                //Clear in/out buffers:
                this.comPort.DiscardOutBuffer();
                this.comPort.DiscardInBuffer();
                //Function 3 request is always 8 bytes:
                byte[] message = new byte[8];
                //Function 3 response buffer:
                byte[] response = new byte[5 + 2 * count]; //Message is 1 addr + 1 fcn + 1 count (16 bits each) + count * 2 * reg vals + 2 CRC
                //Build outgoing modbus message:
                BuildMessage(address, (byte)4, start, count, ref message);
                //Send modbus message to Serial Port:
                try
                {
                    this.comPort.Write(message, 0, message.Length);
                    GetResponse(ref response);
                }
                catch (Exception err)
                {
                    this.modbusStatus = "Error in read event: " + err.Message;
                    return false;
                }

                //Evaluate message:
                if (CheckResponse(response))
                {
                    //Return requested register values:
                    var length = response.Length - 5; //Message is 1 addr + 1 fcn + 1 count (16 bits each) + count * 2 * reg vals + 2 CRC
                    values = new byte[length];
                    Array.Copy(response, 3, values, 0, length);

                    this.modbusStatus = "Read successful";
                    return true;
                }

                this.modbusStatus = "CRC error";
                return false;
            }

            this.modbusStatus = "Serial port not open";
            return false;
        }

        /// <summary>
        /// MODBUS function 08 - Slave Diagnostic Command
        /// </summary>
        /// <param name="address">slave controller address (1-247)</param>
        /// <param name="subCommand">sub command (0-4 and 10-21)</param>
        /// 00 Return Query Data - echo the data sent
        /// 01 Restart Comm Option - force slave controller restart (value = FF00 clears log also; 0000 - leaves log uncleared)
        /// 02 Return Diagnostic Register - value = 0000, value as result - state of diagnostic Register
        /// 03 Change ASCII Input Delimiter - value = xx00, where xx is charcode of "EndOfMessage" delimiter (replaces LF)
        /// 04 Force Listen Only Mode - value = 0000, slave goes to listen mode i.e. no response to commands
        /// <param name="value"></param>
        /// <returns>true if successfull</returns>
        public bool SendFc08(byte address, ushort subCommand, ushort data, ref byte[] value) // Diagnostic commmand
        {
            //Ensure port is open:
            if (this.comPort.IsOpen)
            {
                //Clear in/out buffers:
                this.comPort.DiscardOutBuffer();
                this.comPort.DiscardInBuffer();
                //Message is 1 addr + 1 fcn + 2 subcommand + 2 values + 2 CRC
                byte[] message = new byte[8];
                //Function 08 response is fixed at 8 bytes
                byte[] response = new byte[8];

                //Build outgoing message:
                BuildMessage(address, (byte)8, subCommand, data, ref message);

                //Send Modbus message to Serial Port:
                try
                {
                    this.comPort.Write(message, 0, message.Length);
                    GetResponse(ref response);
                }
                catch (Exception err)
                {
                    this.modbusStatus = "Error in write event: " + err.Message;
                    return false;
                }

                //Evaluate message:
                if (CheckResponse(response))
                {
                    //Return requested value:
                    value = new byte[] { response[4], response[5] };
                    this.modbusStatus = "Read successful";
                    return true;
                }

                this.modbusStatus = "CRC error";
                return false;
            }

            this.modbusStatus = "Serial port not open";
            return false;
        }

        /// <summary>
        /// MODBUS function 16 - Preset holding registers (4xxxx address range - each register holds 2 bytes)
        /// </summary>
        /// <param name="address">slave controller address (1-247)</param>
        /// <param name="start">start address of the holding registers region 4xxxx</param>
        /// <param name="count">number of holding registers to preset (to 4xxxx + 2*count)</param>
        /// <param name="values">array of consecutive register values (2 bytes per register)</param>
        /// <returns>true if successfull</returns>
        public bool SendFc16(byte address, ushort start, ushort count, ushort[] values)
        {
            //Ensure port is open:
            if (this.comPort.IsOpen)
            {
                //Clear in/out buffers:
                this.comPort.DiscardOutBuffer();
                this.comPort.DiscardInBuffer();
                //Message is 1 addr + 1 fcn + 2 start + 2 reg + 1 count + 2 * reg vals + 2 CRC
                byte[] message = new byte[9 + 2 * count];
                //Function 16 response is fixed at 8 bytes
                byte[] response = new byte[8];

                //Add bytecount to message:
                message[6] = (byte)(count * 2);
                //Put write values into message prior to sending:
                for (int i = 0; i < count; i++)
                {
                    message[7 + 2 * i] = (byte)(values[i] >> 8);
                    message[8 + 2 * i] = (byte)(values[i]);
                }
                //Build outgoing message:
                BuildMessage(address, (byte)16, start, count, ref message);
                
                //Send Modbus message to Serial Port:
                try
                {
                    this.comPort.Write(message, 0, message.Length);
                    GetResponse(ref response);
                }
                catch (Exception err)
                {
                    this.modbusStatus = "Error in write event: " + err.Message;
                    return false;
                }
                //Evaluate message:
                if (CheckResponse(response))
                {
                    this.modbusStatus = "Write successful";
                    return true;
                }
 
                this.modbusStatus = "CRC error";
                return false;
            }

            this.modbusStatus = "Serial port not open";
            return false;
        }
    }
}
