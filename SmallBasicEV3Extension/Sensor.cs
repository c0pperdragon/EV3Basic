using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.SmallBasic.Library;
using EV3Communication;

namespace SmallBasicEV3Extension
{
    /// <summary>
    /// Access sensors that are attached to the brick.
    /// To specify the sensor use the port number which is printed below the socket on the brick (e.g. 1).
    /// To access sensors of further bricks that are connected via daisy-chaining, use the next higher numbers instead (e.g. 5 - 8 will access the sensors on the first daisy-chained brick, 9-13 the sensors on the next one and so on).
    /// </summary>
    [SmallBasicType]
    public static class Sensor
    {
        
        private static int[] rawvalues = new int[8];

        /// <summary>
        /// Get the name and mode of a currently connected sensor. 
        /// This function is mainly intended for diagnostic use because you normally know which sensor is plugged to which port on the model.
        /// </summary>
        /// <param name="port">Number of the sensor port</param>
        /// <returns>Description text (e.g. "TOUCH")</returns>
        public static Primitive GetName(Primitive port)
        {
            int layer;
            int no;
            DecodePort(port, out layer, out no);

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x99);                // INPUT_DEVICE 
            c.CONST(0x15);             // CMD: GET_NAME = 0x15
            c.CONST(layer);
            c.CONST(no);
            c.CONST(32);
            c.GLOBVAR(0);
            byte[] result = EV3Communicator.DirectCommand(c, 32, 0);

            if(result==null)
            {
                return new Primitive("");
            }
            else
            {
                return new Primitive(Encoding.ASCII.GetString(result).Replace((char)0,' ').Trim());
            }
        }

        /// <summary>
        /// Get the numercial type identifier of a currently connected sensor.
        /// </summary>
        /// <param name="port">Number of the sensor port</param>
        /// <returns>Sensor type identifier (e.g. 16 for a touch sensor)</returns>
        public static Primitive GetType(Primitive port)
        {
            int layer;
            int no;
            DecodePort(port, out layer, out no);

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x99);                // INPUT_DEVICE 
            c.CONST(0x05);             // CMD: GET_TYPEMODE = 0x05
            c.CONST(layer);
            c.CONST(no);
            c.GLOBVAR(0);
            c.GLOBVAR(1);
            byte[] result = EV3Communicator.DirectCommand(c, 2, 0);
            
            if (result==null || result.Length<2)
            {
                return new Primitive("0");
            }
            else
            {
                return new Primitive("" + result[0]);
            }
        }

        /// <summary>
        /// Get current operation mode of a sensor. Many sensors can work in substantially different modes.
        /// (e.g. the color sensor can detect either ambient light or reflective light or the color).
        /// When the sensor is plugged in it will start with mode 0, but that can be later changed by the program.
        /// </summary>
        /// <param name="port">Number of the sensor port</param>
        /// <returns>Current operation mode (0 is always the default mode)</returns>
        public static Primitive GetMode(Primitive port)
        {
            int layer;
            int no;
            DecodePort(port, out layer, out no);

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x99);                // INPUT_DEVICE 
            c.CONST(0x05);             // CMD: GET_TYPEMODE = 0x05
            c.CONST(layer);
            c.CONST(no);
            c.GLOBVAR(0);
            c.GLOBVAR(1);
            byte[] result = EV3Communicator.DirectCommand(c, 2, 0);

            if (result == null || result.Length < 2)
            {
                return new Primitive("0");
            }
            else
            {
                return new Primitive("" + result[1]);
            }
        }
       
        /// <summary>
        /// Switches the mode of a sensor. 
        /// Many sensors can work in different modes giving quite different readings.
        /// </summary>
        /// <param name="port">Number of the sensor port</param>
        /// <param name="mode">New mode to switch to. This only succeeds when the mode is indeed supported by the sensor.</param>
        public static void SetMode(Primitive port, Primitive mode)
        {
            int layer;
            int no;
            int mod;
            DecodePort(port, out layer, out no);
            if (int.TryParse(mode==null?"":mode.ToString(), out mod))
            {
                ByteCodeBuffer c = new ByteCodeBuffer();
                c.OP(0x99);                // INPUT_DEVICE 
                c.CONST(0x1C);             // CMD: READY_RAW = 0x1C
                c.CONST(layer);
                c.CONST(no);
                c.CONST(0);                // 0 = don't change
                c.CONST(mod);
                c.CONST(0);                // no return values
                EV3Communicator.DirectCommand(c, 0, 0);
            }          
        }

        /// <summary>
        /// Check if a sensor is currently busy switching mode or in process of initialization.
        /// After mode switching a sensor may take some time to become ready again.
        /// </summary>
        /// <param name="port">Number of the sensor port</param>
        /// <returns>"True" if the sensor is currenty busy</returns>
        public static Primitive IsBusy(Primitive port)
        {
            int layer;
            int no;
            DecodePort(port, out layer, out no);

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x9B);    // Input_Test
            c.CONST(layer);
            c.CONST(no);
            c.GLOBVAR(0);
            byte[] response = EV3Communicator.DirectCommand(c, 1, 0);
            if (response == null || response.Length < 1 || response[0] == 0)
            {
                return new Primitive("False");
            }
            else
            {
                return new Primitive("True");
            }
        }

        /// <summary>
        /// Wait until a sensor has finished its reconfiguration. When no sensor is plugged into the
        /// port, this function returns immediately.
        /// </summary>
        /// <param name="port">Number of the sensor port</param>
        public static void Wait(Primitive port)
        {
            int layer;
            int no;
            DecodePort(port, out layer, out no);

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x9B);    // Input_Test
            c.CONST(layer);
            c.CONST(no);
            c.GLOBVAR(0);
            for (;;)
            {
                byte[] response = EV3Communicator.DirectCommand(c, 1, 0);
                if (response == null || response.Length < 1 || response[0] == 0)
                {
                    return;
                }
                System.Threading.Thread.Sleep(2);
            }
        }


        /// <summary>
        /// Read the current sensor value and apply some sensible percentage scaling.
        /// Most sensors can translate the current reading to a meaningful single percentage value like
        /// light intensity or button press state. 
        /// </summary>
        /// <param name="port">Number of the sensor port</param>
        /// <returns>The percentage value (e.g. Touch sensor gives 100 for pressed and 0 for non pressed)</returns>
        public static Primitive ReadPercent(Primitive port)
        {
            int layer;
            int no;
            DecodePort(port, out layer, out no);

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x9A);                // INPUT_READ 
            c.CONST(layer);
            c.CONST(no);
            c.CONST(0);                // 0 = don't change type
            c.CONST(-1);               // -1 = don't change mode
            c.GLOBVAR(0);
            byte[] result = EV3Communicator.DirectCommand(c, 1, 0);

            if (result == null || result.Length < 1)
            {
                return new Primitive("0");
            }
            else
            {
                int v = result[0];
                return new Primitive(v>127 ? "0" : (""+v));
            }
        }

        /// <summary>
        /// Read current sensor value where the result from ReadPercent() is not specific enough.
        /// Some sensor modes deliver values that can not be translated to percentage (e.g. a color index) or
        /// that contain multiple values at once (e.g. the individual red, green, blue light intensities). 
        /// Use this function to get those values. After retrieving a multi-value reading, get the additional
        /// values with the Raw(index) function.
        /// </summary>
        /// <param name="port">Number of the sensor port</param>
        /// <returns>The first value of a raw reading</returns>
        public static Primitive ReadRaw(Primitive port)
        {
            int layer;
            int no;
            DecodePort(port, out layer, out no);

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x9E);                // Input_ReadExt
            c.CONST(layer);
            c.CONST(no);
            c.CONST(0);                // 0 = don't change type
            c.CONST(-1);               // -1 = don't change mode
            c.CONST(18);               // FORMAT = raw (32bit)
            c.CONST(8);                // return 8 32bit-values
            c.GLOBVAR(0);
            c.GLOBVAR(4);
            c.GLOBVAR(8);
            c.GLOBVAR(12);
            c.GLOBVAR(16);
            c.GLOBVAR(20);
            c.GLOBVAR(24);
            c.GLOBVAR(28);

            byte[] result = EV3Communicator.DirectCommand(c, 32, 0);

            if (result == null || result.Length < 32)
            {
                return new Primitive(0);
            }
            else
            {
                lock (rawvalues)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        rawvalues[i] = DecodeRaw(result, i * 4);
                    }
                };
                int v = DecodeRaw(result, 0);
                return new Primitive(v < 0 ? 0 : v);
            }
        }

        /// <summary>
        /// For sensor readings with multiple values, use this function to retrieve these values.
        /// At each ReadRaw, all values are memorized until to the next ReadRaw call.
        /// </summary>
        /// <param name="index">Which value to take. 1..first value, 2..second value,...</param>
        /// <returns>One of the components of the previous raw reading</returns>
        public static Primitive Raw(Primitive index)
        {
            int idx;
            if (int.TryParse(index==null ? "":index.ToString(), out idx))
            {
                if (idx >= 1 && idx <= 8)
                {
                    lock (rawvalues)
                    {
                        int v = rawvalues[idx-1];
                        return new Primitive(v<0 ? 0:v);
                    }
                }
            }
            return new Primitive(0);
        }




        private static void DecodePort(Primitive port, out int layer, out int no)
        {
            layer = 0;
            no = 0;
            if (port!=null)
            {
                int portnumber;
                if (int.TryParse(port.ToString(), out portnumber))
                {
                    if (portnumber>=1 && portnumber<=16)
                    {
                        layer = (portnumber - 1) / 4;
                        no = (portnumber - 1) % 4;
                    }
                }
            }
        }

        private static int DecodeRaw(byte[] result, int start)
        {
            int b0 = ((int) result[start]) & 0xff;
            int b1 = ((int) result[start+1]) & 0xff;
            int b2 = ((int) result[start+2]) & 0xff;
            int b3 = ((int) result[start+3]) & 0xff;
            return b0 | (b1<<8) | (b2<<16) | (b3<<24);
        }

    }
}
