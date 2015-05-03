/*  EV3-Basic: A basic compiler to target the Lego EV3 brick
    Copyright (C) 2015 Reinhard Grafl

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Ports;

namespace EV3Communication
{
    class EV3ConnectionBluetooth : EV3Connection
    {

        private SerialPort _serialPort;
		private BinaryReader _reader;
        private BinaryWriter _writer;

		/// <summary>
		/// Initialize a BluetoothCommunication object.
		/// </summary>
		/// <param name="port">The COM port on which to connect.</param>
		public EV3ConnectionBluetooth(string port)
		{
            try
            {
                _serialPort = new SerialPort(port);
                _serialPort.Open();
            }
            catch (Exception e)
            { 
                // do a workaround for previous bug where port name got an extra letter of garbage
                if (port.StartsWith("COM") && port.Length>4)
                {
                    _serialPort = new SerialPort(port.Substring(0,port.Length-1));
                    _serialPort.Open();
                }
                else
                {
                    throw e;
                }
            }
            _serialPort.WriteTimeout = 5000;  // no send must take that long
            _serialPort.ReadTimeout = 5000;  // no reply must take that long
            _reader = new BinaryReader(_serialPort.BaseStream);
            _writer = new BinaryWriter(_serialPort.BaseStream);
        }

        public override void SendPacket(byte[] data)
        {
            try
            {
                _writer.Write((Int16)data.Length);
                _writer.Write(data);
                _writer.Flush();
            } 
            catch (Exception e)
            {
                Close();
                throw e;
            }
        }

        public override byte[] ReceivePacket()
        {
            try
            {
                int size = _reader.ReadInt16();
                byte[] b = _reader.ReadBytes(size);
                return b;
            }
            catch (Exception e)
            {
                Close();
                throw e;
            }
        }

        public override void Close()
        {
            if (_serialPort != null)
            {
                _serialPort.Close();
                _serialPort = null;
            }
        }

        public override bool IsOpen()
        {
            return _serialPort != null;
        }

    }
}
