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
    /// Use the brick's built-in speaker to play tones or sound files.
    /// </summary>
    [SmallBasicType]
    public static class Speaker
    {
        /// <summary>
        /// Stop any currently playing sound or tone.
        /// </summary>
        public static void Stop()
        {
            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x94);       // opSOUND
            c.CONST(0x00);    // CMD: BREAK = 0x00
            EV3RemoteControler.DirectCommand(c, 0, 0);
        }

        /// <summary>
        /// Start playing a simple tone of defined frequency.
        /// </summary>
        /// <param name="volume">Volume can be 0 - 100</param>
        /// <param name="frequency">Frequency in Hz can be 250 - 10000</param>
        /// <param name="duration">Duration of the tone in milliseconds</param>
        public static void Tone(Primitive volume, Primitive frequency, Primitive duration)
        {
            int vol = volume;
            int frq = frequency;
            int dur = duration;

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x94);       // opSOUND
            c.CONST(0x01);    // CMD: TONE = 0x01
            c.CONST(vol);
            c.CONST(frq);
            c.CONST(dur);
            EV3RemoteControler.DirectCommand(c, 0, 0);
        }

        /// <summary>
        /// Start playing a simple tone defined by its text representation.
        /// </summary>
        /// <param name="volume">Volume can be 0 - 100</param>
        /// <param name="note">Text defining a note "C4" to "B7" or halftones like "C#5"</param>
        /// <param name="duration">Duration of the tone in milliseconds</param>
        public static void Note(Primitive volume, Primitive note, Primitive duration)
        {
            int vol = volume;
            int dur = duration;

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x63);       // opNote_To_Freq
            c.STRING(note==null ? "":note.ToString());
            c.LOCVAR(0);
            c.OP(0x94);       // opSOUND
            c.CONST(0x01);    // CMD: TONE = 0x01
            c.CONST(vol);
            c.LOCVAR(0);
            c.CONST(dur);
            EV3RemoteControler.DirectCommand(c, 0, 2);
        }




        /// <summary>
        /// Start playing a sound from a sound file stored on the brick. Only files in .rsf format are supported. 
        /// </summary>
        /// <param name="volume">Volume can be 0 - 100</param>
        /// <param name="filename">Name of the sound file without the .rsf extension. This filename can be relative to the 'prjs' folder or an absolute path (when starting with '/').</param>
        public static void Play(Primitive volume, Primitive filename)
        {
            int vol = volume;

            String fname = filename == null ? "" : filename.ToString();
            if (!fname.StartsWith("/"))      // relative path
            {   
                fname = "/home/root/lms2012/prjs/" + fname;
            }
            if (fname.Length > 500)
            {
                fname = fname.Substring(0, 500);
            }

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x94);       // opSOUND
            c.CONST(0x02);    //CMD: PLAY = 0x02
            c.CONST(vol);
            c.STRING(fname);
            EV3RemoteControler.DirectCommand(c, 0, 0);
        }

        /// <summary>
        /// Check whether the speaker is still busy playing a previous sound.
        /// </summary>
        /// <returns>"True", if there is a sound still playing, "False" otherwise</returns>
        public static Primitive IsBusy()
        {
            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0x95);       // opSound_Test (BUSY)
            c.GLOBVAR(0);
            byte[] reply = EV3RemoteControler.DirectCommand(c, 1, 0);
            return new Primitive((reply == null || reply[0] == 0) ? "False" : "True");
        }

        /// <summary>
        /// Wait until the current sound has finished playing.
        /// When no sound is playing, this function returns immediately.
        /// </summary>
        public static void Wait()
        {
            ByteCodeBuffer c = new ByteCodeBuffer();
            c.Clear();
            c.OP(0x95);       // opSound_Test (BUSY)
            c.GLOBVAR(0);

            for (; ; )
            {
                byte[] reply = EV3RemoteControler.DirectCommand(c, 1, 0);
                if (reply == null || reply[0] == 0)
                {
                    break;
                }
                System.Threading.Thread.Sleep(2);
            }
        }

    }
}
