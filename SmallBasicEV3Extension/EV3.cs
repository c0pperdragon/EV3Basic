using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
                Int64 ticks = EV3Communicator.TicksSinceStart();
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
            EV3Communicator.DirectCommand(c, 0, 0);
        }


    }
}
