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
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace EV3Communication
{

    public abstract class EV3Connection
    {
         // system commands
        public const byte BEGIN_DOWNLOAD = 0x92;   // Begin file download
        public const byte CONTINUE_DOWNLOAD = 0x93; // Continue file download
        public const byte BEGIN_UPLOAD = 0x94; // Begin file upload
        public const byte CONTINUE_UPLOAD = 0x95; // Continue file upload
        public const byte BEGIN_GETFILE = 0x96; // Begin get bytes from a file (while writing to the file)
        public const byte CONTINUE_GETFILE = 0x97; // Continue get byte from a file (while writing to the file)
        public const byte CLOSE_FILEHANDLE = 0x98; // Close file handle
        public const byte LIST_FILES = 0x99; // List files
        public const byte CONTINUE_LIST_FILES = 0x9A; // Continue list files
        public const byte CREATE_DIR = 0x9B; // Create directory
        public const byte DELETE_FILE = 0x9C; // Delete
        public const byte LIST_OPEN_HANDLES = 0x9D; // List handles
        public const byte WRITEMAILBOX = 0x9E; // Write to mailbox
        public const byte BLUETOOTHPIN = 0x9F; // Transfer trusted pin code to brick
        public const byte ENTERFWUPDATE = 0xA0; // Restart the brick in Firmware update mode
        // system command status responses
        public const byte SUCCESS = 0x00;
        public const byte UNKNOWN_HANDLE = 0x01;
        public const byte HANDLE_NOT_READY = 0x02;
        public const byte CORRUPT_FILE = 0x03;
        public const byte NO_HANDLES_AVAILABLE = 0x04;
        public const byte NO_PERMISSION = 0x05;
        public const byte ILLEGAL_PATH = 0x06;
        public const byte FILE_EXITS = 0x07;
        public const byte END_OF_FILE = 0x08;
        public const byte SIZE_ERROR = 0x09;
        public const byte UNKNOWN_ERROR = 0x0A;
        public const byte ILLEGAL_FILENAME = 0x0B;
        public const byte ILLEGAL_CONNECTION = 0x0C;


        // low-level data transmission  (implemented in specialized object)
        public abstract void SendPacket(byte[] data);
        public abstract byte[] ReceivePacket();
        public abstract bool IsOpen();
        public abstract void Close();

        // counter for each message to correctly pair it to its reply
        private static ushort messagecounter = 0;

        public byte[] SystemCommand(byte command, BinaryBuffer arguments)
        {
            int al = arguments == null ? 0 : arguments.Length;
            messagecounter++;

            byte[] packet = new byte[2 + 1 + 1 + al];
            packet[0] = (byte)(messagecounter & 0xff);
            packet[1] = (byte)((messagecounter >> 8) & 0xff);
            packet[2] = 0x01;   // SYSTEM_COMMAND_REPLY 
            packet[3] = command;
            if (al > 0)
            {
                arguments.CopyTo(packet, 4);
            }
            SendPacket(packet);

            for (; ; )
            {
                packet = ReceivePacket();
                //                Console.WriteLine("Packet Length: " + packet.Length);                
                if (packet.Length < 3)
                {
                    throw new Exception("Reply is has no message counter");
                }
                // wait until reply arrives that has a counter that matches the command
                ushort ctr = (ushort)(packet[0] + (packet[1] << 8));
                //                Console.WriteLine("Expected counter: "+messagecounter+ " received counter: " + ctr);
                if (ctr != messagecounter)
                {
                    continue;
                }
                // check if problems with received packet
                if (packet[2] != 0x03 && packet[2] != 0x05)
                {
                    throw new Exception("Reply is not of correct type");
                }
                if (packet.Length < 5)
                {
                    throw new Exception("Unexpected end of reply");
                }

                // extract data
                byte[] replyarguments = new byte[packet.Length - 4];
                System.Array.Copy(packet, 4, replyarguments, 0, packet.Length - 4);
                return replyarguments;
            }
        }

        public byte[] DirectCommand(ByteCodeBuffer bytecodes, int globalbytes, int localbytes)
        {

            // increase message counter (neccesary to check if request and respond match)
            messagecounter++;

            byte[] sendpacket = new byte[2 + 1 + 2 + bytecodes.Length];
            sendpacket[0] = (byte)(messagecounter & 0xff);
            sendpacket[1] = (byte)((messagecounter >> 8) & 0xff);
            sendpacket[2] = 0x00;   // DIRECT_COMMAND_REPLY 
            sendpacket[3] = (byte)(globalbytes & 0xff);
            sendpacket[4] = (byte)(((globalbytes >> 8) & 0x3) + (localbytes << 2));
            bytecodes.CopyTo(sendpacket, 5);
            SendPacket(sendpacket);

            for (; ; )
            {
                byte[] packet = ReceivePacket();

                if (packet.Length < 3)
                {
                    throw new Exception("Reply has no message counter");
                }
                // wait until reply arrives that has a counter that matches the command
                ushort ctr = (ushort)(packet[0] + (packet[1] << 8));
                //         Console.WriteLine("Expected counter: "+messagecounter+ " received counter: " + ctr);
                if (ctr != messagecounter)
                {
                    continue;
                }
                // check if the direct command caused an error
                if (packet[2] == 0x04)
                {
                    return null;
                }

                // check if problems with received packet
                if (packet[2] != 0x02)
                {
                    throw new Exception("Reply is not of correct type");
                }
                if (packet.Length != globalbytes+3)
                {
                    throw new Exception("Reply size unexpected: "+packet.Length+" instead of "+(globalbytes+3));
                }

                // extract data
                byte[] replydata = new byte[packet.Length - 3];
                System.Array.Copy(packet, 3, replydata, 0, packet.Length - 3);
                return replydata;
            }
        }


        public void CreateEV3File(String fullname, byte[] content)
        {
            int chunksize = 900;
            int pos = 0;
            int transfernow = Math.Min(content.Length - pos, chunksize - fullname.Length);

            // start the transfer
            BinaryBuffer b = new BinaryBuffer();
            b.Append32(content.Length);
            b.AppendZeroTerminated(fullname);
            b.AppendBytes(content, pos, transfernow);

            byte[] response = SystemCommand(BEGIN_DOWNLOAD, b);

            if (response == null)
            {
                throw new Exception("No response to BEGIN_DOWNLOAD");
            }
            if (response.Length < 2)
            {
                throw new Exception("Response too short for BEGIN_DOWNLOAD");
            }
            if (response[0] != SUCCESS && response[0] != END_OF_FILE)
            {
                throw new Exception("Unexpected status at BEGIN_DOWNLOAD: " + response[0]);
            }

            pos += transfernow;

            int handle = response[1] & 0xff;

            // transfer bytes in small chunks
            while (pos < content.Length)
            {
                transfernow = Math.Min(content.Length - pos, chunksize);
                b.Clear();
                b.Append8(handle);
                b.AppendBytes(content, pos, transfernow);
                response = SystemCommand(CONTINUE_DOWNLOAD, b);

                if (response == null)
                {
                    throw new Exception("No response to CONTINUE_DOWNLOAD");
                }
                if (response.Length < 2)
                {
                    throw new Exception("Response too short for CONTINUE_DOWNLOAD");
                }
                if (response[0] != SUCCESS && response[0] != END_OF_FILE)
                {
                    throw new Exception("Unexpected status at CONTINUE_DOWNLOAD: " + response[0]);
                }

                pos += transfernow;
            }
        }

        public byte[] ReadEV3File(String fullname, ByteCodeBuffer pingercommand=null)
        {
            long nextping = DateTime.Now.Ticks + 500;
            int chunksize = 900;

            // start the transfer
            BinaryBuffer b = new BinaryBuffer();
            b.Append16(0);                     // transfer no content right now
            b.AppendZeroTerminated(fullname);

            byte[] response = SystemCommand(BEGIN_UPLOAD, b);

            if (response == null)
            {
                throw new Exception("No response to BEGIN_UPLOAD");
            }
            if (response.Length < 6)
            {
                throw new Exception("Response too short for BEGIN_UPLOAD");
            }
            if (response[0] != SUCCESS && response[0] != END_OF_FILE)
            {
                throw new Exception("Unexpected status at BEGIN_UPLOAD: " + response[0]);
            }

            int len = ((int)response[1]) + (((int)response[2]) << 8) + (((int)response[3]) << 16) + (((int)response[4]) << 24);
            int handle = response[5] & 0xff;

//    Console.WriteLine("Start uploading file of size: " + len + ". handle=" + handle);

            byte[] buffer = new byte[len];
            int pos = 0;

            // transfer bytes in small chunks
            while (pos < len)
            {
                int transfernow = Math.Min(len - pos, chunksize);
                b.Clear();
                b.Append8(handle);
                b.Append16(transfernow);

                response = SystemCommand(CONTINUE_UPLOAD, b);

                if (response == null)
                {
                    throw new Exception("No response to CONTINUE_UPLOAD");
                }
                if (response.Length < 2 + transfernow)
                {
                    throw new Exception("Response too short for CONTINUE_UPLOAD");
                }
                if (response[0] != SUCCESS && response[0] != END_OF_FILE)
                {
                    throw new Exception("Unexpected status at CONTINUE_UPLOAD: " + response[0]);
                }

                for (int i = 0; i < transfernow; i++)
                {
                    buffer[pos + i] = response[2 + i];
                }
                pos += transfernow;

                // check if it is necessary to send intermediary pings to the watchdog program
                if (pingercommand!=null && DateTime.Now.Ticks > nextping)
                {
                    DirectCommand(pingercommand, 4,0);
                    nextping = DateTime.Now.Ticks+500;
                }
            }

            return buffer;
        }

    }


}
