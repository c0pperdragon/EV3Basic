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
using System.Net.Sockets;
using System.Net;

namespace EV3Communication
{
    class EV3ConnectionWiFi : EV3Connection
    {
        static String handshakerequest = "GET /target?sn=\r\nProtocol:EV3\r\n\r\n";
        static String handshakeresponse = "Accept:EV340\r\n\r\n";

        private TcpClient tcpClient;
        private NetworkStream stream;
        private BinaryReader reader;
        private BinaryWriter writer;

		public EV3ConnectionWiFi(IPAddress ipaddress)
		{
            tcpClient = null;
            stream = null;

            try
            {
//                Console.WriteLine("Creating connection");
                tcpClient = new TcpClient(ipaddress.ToString(),5555);
                stream = tcpClient.GetStream();

//                Console.WriteLine("Sending handshake");
                   
                // write initial handshake data
                byte[] data = System.Text.UTF8Encoding.UTF8.GetBytes(handshakerequest);
                stream.Write(data, 0, data.Length);

//                Console.WriteLine("Receiving handshake");

                // read and verify handshake response
                byte[] r = System.Text.UTF8Encoding.UTF8.GetBytes(handshakeresponse);
                for (int i=0; i<r.Length; i++)
                {
//                    Console.WriteLine("reading byte " + i + "...");
                    if (stream.ReadByte() != r[i])
                    {
                        throw new IOException("Invalid handshake response");
                    }
                }   
             
                // create the convenient reader and writer objects
                reader = new BinaryReader(stream);
                writer = new BinaryWriter(stream);
            }
            catch (Exception e)
            { 
                if (stream!=null)
                {
                    stream.Close();
                    stream = null;
                }
                if (tcpClient!=null)
                {
                    tcpClient.Close();
                    tcpClient = null;
                }
                throw e;
            }
        }

        public override void SendPacket(byte[] data)
        {
            try
            {
                writer.Write((Int16)data.Length);
                writer.Write(data);
                writer.Flush();
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
                int size = reader.ReadInt16();
                byte[] b = reader.ReadBytes(size);
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
            if (stream != null)
            {
                stream.Close();
                stream = null;
            }
            if (tcpClient != null)
            {
                tcpClient.Close();
                tcpClient = null;
            }
        }

        public override bool IsOpen()
        {
            return stream != null;
        }


    }
}
