using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

using Microsoft.SmallBasic.Library;
using EV3Communication;

namespace SmallBasicEV3Extension
{

    /// <summary>
    /// Static library for communication with the EV3 brick.
    /// Contains methods for System commands and Direct commands.
    /// Only one brick can be attached to the program at any time.
    /// </summary>
    public static class EV3Communicator
    {
        // lock to prevent concurrent access to static methods
        private static Object sync = new Object();

        // the single connection to the EV3 brick
        private static EV3Connection con = null;

        private static Int64 starttime = 0;

        internal static byte[] DirectCommand(ByteCodeBuffer bytecodes, int globalbytes, int localbytes)
        {
            try
            {
                lock (sync)
                {
                    // if not already done, memorize the start time and fire up the communication
                    MemorizeStartTime();
                    EstablishConnection();

                    // finally execute the command
                    return con.DirectCommand(bytecodes, globalbytes, localbytes);
                }
            }
            catch (Exception)
            {   // no connection - must terminate immediately 
                System.Environment.Exit(1);
            }
            return null;
        }

        internal static void CreateEV3File(String fullname, byte[] content)
        {
            try
            {
                lock (sync)
                {
                    // if not already done, memorize the start time and fire up the communication
                    MemorizeStartTime();
                    EstablishConnection();

                    // finally execute the command
                    con.CreateEV3File(fullname, content);
                }
            }
            catch (Exception)
            {   // no connection or other severe error - must terminate immediately 
                System.Environment.Exit(1);
            }
        }

        internal static byte[] ReadEV3File(String fullname)
        {
            try
            {
                lock (sync)
                {
                    // if not already done, memorize the start time and fire up the communication
                    MemorizeStartTime();
                    EstablishConnection();

                    // finally execute the command
                    return con.ReadEV3File(fullname);
                }
            }
            catch (Exception)
            {   // no connection or other severe error - must terminate immediately 
                System.Environment.Exit(1);
            }
            return null;
        }


        internal static Int64 TicksSinceStart()
        {
            lock (sync)
            {
                MemorizeStartTime();
                return DateTime.Now.Ticks - starttime;
            }
        }


        // must be called while holding a lock on sync
        private static void MemorizeStartTime()
        {
            if (starttime==0)
            {
                starttime = DateTime.Now.Ticks;
            }
        }

        // must be called while holding a lock on sync
        private static void EstablishConnection()
        {
            // the first action also involves passing some SystemCommands and DirectCommands
            if (con == null)
            {
                // try to start up connection (when failing, an exception is thrown)
                con = new EV3ConnectionUSB();

                String filename = "/tmp/EV3-Basic Session.rbf";

                // download the watchdog program as a file to the brick
                con.CreateEV3File(filename, HexDumpToBytes(SmallBasicEV3Extension.Properties.Resources.WatchDog));

                // before loading the watchdog program, check if there is no other program running
                ByteCodeBuffer c = new ByteCodeBuffer();
                c.OP(0x0C);            // opProgram_Info
                c.CONST(0x16);         // CMD: GET_STATUS = 0x16
                c.CONST(1);            // program slot 1 = user slot
                c.GLOBVAR(8);

                // load and start it
                c.OP(0xC0);       // opFILE
                c.CONST(0x08);    // CMD: LOAD_IMAGE = 0x08
                c.CONST(1);       // slot 1 = user program slot
                c.STRING(filename);
                c.GLOBVAR(0);
                c.GLOBVAR(4);
                c.OP(0x03);       // opPROGRAM_START
                c.CONST(1);       // slot 1 = user program slot
                c.GLOBVAR(0);
                c.GLOBVAR(4);
                c.CONST(0);

                // after starting, check if indeed running now
                c.OP(0x0C);            // opProgram_Info
                c.CONST(0x16);         // CMD: GET_STATUS = 0x16
                c.CONST(1);            // program slot 1 = user slot
                c.GLOBVAR(9);

                byte[] response = con.DirectCommand(c, 10, 0);
                if (response == null || response[8] != 0x0040 || response[9] == 0x0040)
                {
                    throw new Exception("Could not start EV3 remote client on device");
                }

                // set up local ping thread to periodically send a command to the watchdog
                // program to keep it alive (and check if the brick is still operating)
                (new System.Threading.Thread(runpings)).Start();
            }
        }

        internal static byte[] HexDumpToBytes(String hexcontent)
        {
            byte[] content = new byte[hexcontent.Length / 2];
            for (int i = 0; i < hexcontent.Length; i += 2)
                content[i / 2] = Convert.ToByte(hexcontent.Substring(i, 2), 16);
            return content;
        }


    // Periodically modify a flag in the global variables of program slot 1. 
    // The program in slot 1 is a watchdog program that will notice when this "pings" no longer
    // arrive and will stop itself and shut down motors and sensors gracefully.
    // On the other side, the pinger will monitor if the watchdog is still running (could have
    // been stopped manually by the user) and if so, terminates the Basic program also.
        private static void runpings()
        {
            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x3A);           // Move32_32
            c.CONST(42);          // move this value
            c.GLOBVAR(0);         // to gloval variable 0-3

            c.OP(0x7E);            // Memory_Write
            c.CONST(1);            // program slot 1 = user slot
            c.CONST(0);            // write to global variables
            c.CONST(0);            // to global variable 0
            c.CONST(4);            // write 4 bytes
            c.GLOBVAR(0);          // take the prepared value 42

            c.OP(0x0C);            // opProgram_Info
            c.CONST(0x16);         // CMD: GET_STATUS = 0x16
            c.CONST(1);            // program slot 1 = user slot
            c.GLOBVAR(0);

            for (; ; )
            {
                lock (sync)
                {
                    byte[] packet = null;
                    try
                    {
                        packet = con.DirectCommand(c, 4, 0);
                    }
                    catch (Exception e) 
                    {
                        throw e;
                    }
                    // detected communication error or watchdog progam is no longer running
                    if (packet == null || packet.Length<=0 || packet[0] == 0x40)
                    {
                        System.Environment.Exit(1);
                    }
                }
                System.Threading.Thread.Sleep(500);
            }
        }
    }



}
