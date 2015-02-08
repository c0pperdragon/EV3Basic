using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

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

            byte[] packet = new byte[2 + 1 + 2 + bytecodes.Length];
            packet[0] = (byte)(messagecounter & 0xff);
            packet[1] = (byte)((messagecounter >> 8) & 0xff);
            packet[2] = 0x00;   // DIRECT_COMMAND_REPLY 
            packet[3] = (byte)(globalbytes & 0xff);
            packet[4] = (byte)(((globalbytes >> 8) & 0x3) + (localbytes << 2));
            bytecodes.CopyTo(packet, 5);
            SendPacket(packet);

            for (; ; )
            {
                packet = ReceivePacket();
                //                hexdump(packet);
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
                if (packet.Length - 3 != globalbytes)
                {
                    throw new Exception("Reply data size does not match expected length");
                }

                // extract data
                byte[] replydata = new byte[packet.Length - 3];
                System.Array.Copy(packet, 3, replydata, 0, packet.Length - 3);
                return replydata;
            }
        }


    }


}
