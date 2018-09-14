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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;

using Microsoft.SmallBasic.Library;
using EV3Communication;

namespace SmallBasicEV3Extension
{
    /// <summary>
    /// Small utility functions that concern the EV3 brick as a whole.
    /// </summary>
    [SmallBasicType]
    public static class EV3
    {
        /// <summary>
        /// The time in milliseconds since the program was started.
        /// </summary>
        public static Primitive Time
        {
            get
            {
                System.Int64 ticks = EV3RemoteControler.TicksSinceStart();
                return new Primitive(System.Math.Ceiling(ticks / 10000.0));
            }
        }

        /// <summary>
        /// Set the color of the brick LED light and the effect to use for it.
        /// </summary>
        /// <param name="color">Can be "OFF", "GREEN", "RED", "ORANGE"</param>
        /// <param name="effect">Can be "NORMAL", "FLASH", "PULSE"</param>
        public static void SetLEDColor(Primitive color, Primitive effect)
        {
            int col = 0;
            String colorstring = color==null ? "":color.ToString();
            if (colorstring.Equals("GREEN", StringComparison.OrdinalIgnoreCase))
            {
                col = 1;
            }
            else if (colorstring.Equals("RED", StringComparison.OrdinalIgnoreCase))
            {
                col = 2;
            }
            else if (colorstring.Equals("ORANGE", StringComparison.OrdinalIgnoreCase))
            {
                col = 3;
            }
            if (col != 0)
            {
                String effectstring = effect==null ? "":effect.ToString();
                if (effectstring.Equals("FLASH", StringComparison.OrdinalIgnoreCase))
                {
                    col += 3;
                }
                else if (effectstring.Equals("PULSE", StringComparison.OrdinalIgnoreCase))
                {
                    col += 6;
                }
            }

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x82);           // UI_WRITE
            c.CONST(0x1B);        // CMD: LED
            c.CONST(col);
            EV3RemoteControler.DirectCommand(c, 0, 0);
        }

        /// <summary>
        /// The current charge level of the battery (range 0 to 100).
        /// </summary>
        public static Primitive BatteryLevel
        {
            get
            {
                ByteCodeBuffer c = new ByteCodeBuffer();
                c.OP(0x81);           // UI_READ
                c.CONST(0x12);        // CMD: GET_LBATT = 0x12
                c.GLOBVAR(0);
                byte[] result = EV3RemoteControler.DirectCommand(c, 1, 0);
                
                if (result==null || result.Length<1 || result[0]<0)
                {
                    return new Primitive(0.0);
                }
                else
                {
                    return new Primitive( (double) result[0]);
                }
            }
        }
        /// <summary>
        /// The current charge level of the battery in volts.
        /// </summary>
        public static Primitive BatteryVoltage
        {
            get
            {
                ByteCodeBuffer c = new ByteCodeBuffer();
                c.OP(0x81);           // UI_READ
                c.CONST(0x01);        // CMD: GET_VBATT = 0x01
                c.GLOBVAR(0);
                byte[] result = EV3RemoteControler.DirectCommand(c, 4, 0);

                if (result == null || result.Length < 4)
                {
                    return new Primitive(0.0);
                }
                else
                {
                    return new Primitive((double)BitConverter.ToSingle(result, 0));
                }
            }
        }
        /// <summary>
        /// The electric current being consumed from the battery in amperes.
        /// </summary>
        public static Primitive BatteryCurrent
        {
            get
            {
                ByteCodeBuffer c = new ByteCodeBuffer();
                c.OP(0x81);           // UI_READ
                c.CONST(0x02);        // CMD: GET_IBATT = 0x02
                c.GLOBVAR(0);
                byte[] result = EV3RemoteControler.DirectCommand(c, 4, 0);

                if (result == null || result.Length < 4)
                {
                    return new Primitive(0.0);
                }
                else
                {
                    return new Primitive((double)BitConverter.ToSingle(result, 0));
                }
            }
        }

        /// <summary>
        /// The individual name of the EV3 brick.
        /// </summary>
        public static Primitive BrickName
        {
            get
            {
                ByteCodeBuffer c = new ByteCodeBuffer();
                c.OP(0xD3);            // opCOM_GET
                c.CONST(0x0D);         // GET_BRICKNAME
                c.CONST(18);           // maximum length
                c.GLOBVAR(0);        
                byte[] response = EV3RemoteControler.DirectCommand(c, 20, 0);

                // check response
                if (response != null && response.Length >= 20)
                {
                    // find the null-termination
                    for (int len = 0; len < 19; len++)
                    {
                        if (response[len] == 0)
                        {
                            // extract the message text
                            char[] msg = new char[len];
                            for (int i = 0; i < len; i++)
                            {
                                msg[i] = (char)response[i];
                            }
                            return new Primitive(new String(msg));
                        }
                    }
                }
                // no proper string found 
                return new Primitive("");
            }
        }

        /// <summary>
        /// Execute one system command by the command shell of the EV3 linux system. All threads of the virtual machine are halted until the system command is finished.
        /// </summary>
        /// <param name="commandline">The system command</param>
        /// <returns>Exit status of the command</returns>
        public static Primitive SystemCall (Primitive commandline)
        {
            String cmd = (commandline == null ? "" : commandline.ToString());

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x60);           // SYSTEM
            c.STRING(cmd);        
            c.LOCVAR(0);
            c.OP(0x30);           // MOVE8_8
            c.LOCVAR(1);           // for some reason, the SYSTEM command returns the result value as 8 bit in byte 1 !
            c.GLOBVAR(0);
            byte[] result = EV3RemoteControler.DirectCommand(c, 1, 4);
            if (result==null || result.Length<1)
            {
                return new Primitive(-1);
            }
            return new Primitive((double)result[0]);
        }

        /// <summary>
        /// Increase performance when program runs in "PC mode": Do not send next command to the brick immediately, but wait until one more command is issued which will then be sent together with the first one. You can bunch more commands together by using an additional call to QueueNextCommand before any command that should be placed into the queue.
        /// In "brick mode", this command has no effect.
        /// </summary>
        public static void QueueNextCommand()
        {
            EV3RemoteControler.QueueNextCommand();
        }


        static bool[] hasDownloaded = new bool[1];

        internal static Primitive NativeCode(Primitive command)
        {
            String cmd = (command == null) ? "" : command.ToString().Trim();
            if (cmd.Length==0)
            {
                return new Primitive(-1);
            }

            // if have not downloaded the native code, do it now
            lock (hasDownloaded)
            {
                if (!hasDownloaded[0])
                {
                    EV3RemoteControler.InstallNativeCode();
                    hasDownloaded[0] = true;
                }
            }

            // start native code process (and wait for termination)
            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x60);            // opSYSTEM
            c.STRING("/tmp/nativecode "+command);
            c.GLOBVAR(0);          // result code

            byte[] result = EV3RemoteControler.DirectCommand(c, 4, 0);
            if (result == null || result.Length < 4)
            {
                return new Primitive(-1);
            }

            return new Primitive(result[1]);  // for some reasons, the program result is delivered in byte 1 
        }



    }
}
