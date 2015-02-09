using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.SmallBasic.Library;
using EV3Communication;

namespace SmallBasicEV3Extension
{
    /// <summary>
    /// Reads the states and clicks of the buttons on the brick.
    /// The buttons are specified with the following letters:
    ///  U   up
    ///  D   down
    ///  L   left
    ///  R   right
    ///  E   enter
    /// </summary>
    [SmallBasicType]
    public static class Buttons
    {

        /// <summary>
        /// The buttons that are currently pressed. 
        /// This property contains a text with the key letters of all keys being pressed at the moment. 
        /// </summary>
        public static Primitive Current
        {
            get
            {
                ByteCodeBuffer c = new ByteCodeBuffer();
                c.OP(0x83);           // UI_BUTTON
                c.CONST(0x09);        // CMD: PRESSED
                c.CONST(0x01);        // up
                c.GLOBVAR(1);
                c.OP(0x83);           // UI_BUTTON
                c.CONST(0x09);        // CMD: PRESSED
                c.CONST(0x02);        // enter
                c.GLOBVAR(2);
                c.OP(0x83);           // UI_BUTTON
                c.CONST(0x09);        // CMD: PRESSED
                c.CONST(0x03);        // down
                c.GLOBVAR(3);
                c.OP(0x83);           // UI_BUTTON
                c.CONST(0x09);        // CMD: PRESSED
                c.CONST(0x04);        // right
                c.GLOBVAR(4);
                c.OP(0x83);           // UI_BUTTON
                c.CONST(0x09);        // CMD: PRESSED
                c.CONST(0x05);        // left
                c.GLOBVAR(5);

                return CreateFlags(EV3Communicator.DirectCommand(c, 6, 0));
            }
        }

        /// <summary>
        /// Checks which buttons were clicked since the last call to GetClicks and returns a text containing their letters. 
        /// The 'clicked' state of the buttons is then removed. Also a sound is emitted from the brick when a click was detected.
        /// </summary>
        /// <returns>A text containing the letters of the clicked buttons (can be empty)</returns>
        public static Primitive GetClicks()
        {
            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x83);           // UI_BUTTON
            c.CONST(0x01);        // CMD: SHORTPRESS = 0x01
            c.CONST(0x01);        // up
            c.GLOBVAR(1);
            c.OP(0x83);           // UI_BUTTON
            c.CONST(0x01);        // CMD: SHORTPRESS = 0x01
            c.CONST(0x02);        // enter
            c.GLOBVAR(2);
            c.OP(0x83);           // UI_BUTTON
            c.CONST(0x01);        // CMD: SHORTPRESS = 0x01
            c.CONST(0x03);        // down
            c.GLOBVAR(3);
            c.OP(0x83);           // UI_BUTTON
            c.CONST(0x01);        // CMD: SHORTPRESS = 0x01
            c.CONST(0x04);        // right
            c.GLOBVAR(4);
            c.OP(0x83);           // UI_BUTTON
            c.CONST(0x01);        // CMD: SHORTPRESS = 0x01
            c.CONST(0x05);        // left
            c.GLOBVAR(5);
            c.OP(0x83);           // UI_BUTTON
            c.CONST(0x01);        // CMD: SHORTPRESS = 0x01
            c.CONST(0x07);        // any other button
            c.GLOBVAR(0);

            return CreateFlags(EV3Communicator.DirectCommand(c, 6, 0));
        }

        /// <summary>
        /// Wait until at least one button is clicked. If a buttons was already clicked before calling this function, it returns immediately.
        /// </summary>
        public static void Wait()
        {
            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x83);           // UI_BUTTON
            c.CONST(0x0c);        // CMD: TESTSHORTPRESS = 0x0C
            c.CONST(0x07);        // any button
            c.GLOBVAR(0);

            for (; ; )
            {
                byte[] response = EV3Communicator.DirectCommand(c, 1, 0);
                if (response[0]!=0)
                {   return;
                }
                System.Threading.Thread.Sleep(2);
            }
        }

        /// <summary>
        /// Remove any clicked-state of all buttons. Subsequent calls to GetClicks will only deliver the buttons that were clicked after the flush.
        /// </summary>
        public static void Flush()
        {
            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x83);           // UI_BUTTON
            c.CONST(0x04);        // CMD: FLUSH = 0x04
            EV3Communicator.DirectCommand(c, 0, 0);
        }



        private static Primitive CreateFlags(byte[] response)
        {
            if (response == null)
            {
                return new Primitive("");
            }

            String r = "";
            if (response[1] != 0)
            {
                 r = r + "U";
            }
            if (response[2] != 0)
            {
                 r = r + "E";
            }
            if (response[3] != 0)
            {
                 r = r + "D";
            }
            if (response[5] != 0)
            {
                r = r + "L";
            }
            if (response[4] != 0)
            {
                r = r + "R";
            }
            return new Primitive(r);
        }

    }
}
