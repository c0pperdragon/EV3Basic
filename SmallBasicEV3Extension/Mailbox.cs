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

using Microsoft.SmallBasic.Library;
using EV3Communication;

namespace SmallBasicEV3Extension
{
    /// <summary>
    /// Communication facility to send messages via Bluetooth from brick to brick
    /// </summary>
    /// 
    [SmallBasicType]
    public static class Mailbox
    {
        private static Object sync = new Object();
        private static int numboxes = 0;

        /// <summary>
        /// Create a mailbox on the local brick that can receive messages from other bricks. 
        /// Only after creation of the box incoming messages can be stored for retrieval.
        /// There is a total limit of 30 mailboxes that can be created.
        /// </summary>
        /// <param name="boxname">Name of the message box to be created.</param>
        /// <returns>A numerical identifier of the mailbox. This is needed to actually retrieve messages from the box.</returns>
        public static Primitive Create(Primitive boxname)
        {
            String bn = boxname==null ? "" : boxname.ToString();
            int no = -1;

            lock (sync)
            {
                // determine next number to use
                if (numboxes<30)
                {
                    no = numboxes;
                    numboxes++;
                }
            }

            if (no >= 0)
            {
                // send box creation request
                ByteCodeBuffer c = new ByteCodeBuffer();
                c.OP(0xD8);       // opMailbox_Open
                c.CONST(no);
                c.STRING(bn);
                c.CONST(4);       // 0x04 : DataS Zero terminated string
                c.CONST(0);       // FIFOSIZE – Not used at this point
                c.CONST(0);       // VALUES – Not used
                EV3RemoteControler.DirectCommand(c, 0, 0);
            }
            return new Primitive(no);
        }

        /// <summary>
        /// Send a message to a mailbox on another brick.
        /// </summary>
        /// <param name="brickname">The name of the brick to receive the message. A connection to this brick must be already open for this command to work. You can specify empty Text here, in which case the message will be sent to all connected bricks.</param>
        /// <param name="boxname">Name of the message box on the receiving brick.</param>
        /// <param name="message">The message as a text. Currently only text messages are supported.</param>
        public static void Send(Primitive brickname, Primitive boxname, Primitive message)
        {
            String bn = boxname == null ? "" : boxname.ToString();
            String brick = brickname == null ? "" : brickname.ToString();
            String msg = message == null ? "" : message.ToString();

            // send message send request
            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xD9);       // opMailbox_Write
            c.STRING(brick);
            c.CONST(0);       // HARDWARE - not used
            c.STRING(bn);
            c.CONST(4);       // 0x04 : DataS Zero terminated string
            c.CONST(1);       // VALUES
            c.STRING(msg);
            EV3RemoteControler.DirectCommand(c, 0, 0);
        }


        /// <summary>
        /// Checks if there is a message in the specified mailbox.
        /// </summary>
        /// <param name="id">Identifier of the local mailbox</param>
        /// <returns>"True" if there is a message waiting, "False" otherwise</returns>
        public static Primitive IsAvailable(Primitive id)
        {
            int no = id;
            // send message info request
            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xDB);          // opMailbox_Test
            c.CONST(no);
            c.GLOBVAR(0);        // return value
            byte[] response = EV3RemoteControler.DirectCommand(c, 1, 0);
            // check response
            if (response != null && response.Length >= 1 && response[0] == 0)
            {
                return new Primitive("True");
            }
            else
            {
                return new Primitive("False");
            }
        }

        /// <summary>
        /// Receive the latest message from a local mailbox. When no message is present, the command will block until some message arrives.
        /// The message will then be consumed and the next call to Receive will wait for the next message.
        /// To avoid blocking, you can check with IsAvailable() whether there is a message in the box. When no message box with the name exists, the command will return "" immediately.
        /// </summary>
        /// <param name="id">Identifier of the local mailbox</param>
        /// <returns>The message as a Text. Currently only text messages are supported.</returns>
        public static Primitive Receive(Primitive id)
        {
            int no = id;

            ByteCodeBuffer c = new ByteCodeBuffer();
            for (; ; )
            {
                // send request that checks for existence and tries to retrieve the message
                c.Clear();
                c.OP(0xDB);          // opMailbox_Test
                c.CONST(no);
                c.GLOBVAR(0);        // return value for the existence test
                c.OP(0xDA);        // opMailbox_Read
                c.CONST(no);
                c.CONST(252);      // maximum string size
                c.CONST(1);        // want to read 1 value
                c.GLOBVAR(1);      // where to store the data (if any available)

                byte[] response = EV3RemoteControler.DirectCommand(c, 253, 0);
                
                // check response
                if (response != null && response.Length >= 232 && response[0] == 0)
                {
                    // find the null-termination
                    for (int len = 0; len < 252; len++)
                    {
                        if (response[1+len]==0)
                        {
                            // extract the message text
                            char[] msg = new char[len];
                            for (int i=0; i<len; i++)
                            {
                                msg[i] = (char) response[1+i];
                            }
                            return new Primitive(new String(msg));
                        }
                    }
                }

                // when response did not match requirement, retry later
                System.Threading.Thread.Sleep(2);
            }
        }


        /// <summary>
        /// Tries to establish a Bluetooth connection to another brick if it is not already connected.
        /// Only after a connection has been made (either by this command or manually from the on-brick menu), messages can be exchanged in both directions.
        /// </summary>
        /// <param name="brickname">Name of the remote brick.</param>
        public static void Connect(Primitive brickname)
        {
            String brick = brickname == null ? "" : brickname.ToString();

            // send connection request
            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xD4);           // opCom_Set
            c.CONST(0x07);        // CMD: SET_CONNECTION = 0x07
            c.CONST(2);           // HARDWARE:  2: Bluetooth communication interface
            c.STRING(brick);
            c.CONST(1);           // connect
            EV3RemoteControler.DirectCommand(c, 0, 0);
        }


    }
}
