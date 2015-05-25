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

namespace EV3Communication
{
    public class BinaryBuffer
    {
        private byte[] buffer;
        private int len;

        public BinaryBuffer()
        {
            buffer = new byte[20];
            len = 0;
        }

        public int Length
        {
            get { return len; }
        }

        public void Clear()
        {
            len = 0;
        }

        public void CopyTo(byte[] target, int position)
        {
            System.Array.Copy(buffer, 0, target, position, len);
        }

        public void CopyTo(BinaryBuffer target)
        {
            target.AppendBytes(buffer, 0, len);             
        }


        public void Append8(int i)
        {
            if (len >= buffer.Length)
            {
                byte[] buffer2 = new byte[buffer.Length * 2];
                buffer.CopyTo(buffer2, 0);
                buffer = buffer2;
            }
            buffer[len] = (byte)i;
            len++;
        }

        public void Append16(int i)
        {
            Append8(i & 0xff);
            Append8((i>>8) & 0xff);
        }

        public void Append32(int i)
        {
            Append8(i & 0xff);
            Append8((i >> 8) & 0xff);
            Append8((i >> 16) & 0xff);
            Append8((i >> 24) & 0xff);
        }

        public void AppendBytes(byte[] b)
        {
            AppendBytes(b, 0, b.Length);
        }

        public void AppendBytes(byte[] b, int start, int length)
        {
            for (int i = 0; i < length; i++)
            {
                Append8(b[start+i]);
            }
        }

        public void AppendZeroTerminated(String s)
        {
            AppendNonZeroTerminated(s);
            Append8(0);
        }

        public void AppendNonZeroTerminated(String s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                int c = s[i];
                if (c<=0 || c>255)
                {
                    Append8(1);
                }
                else
                {
                    Append8(c);
                }
            }
        }


        public static int Extract16(byte[] buffer, int position)
        {
            int b1 = ((int)buffer[position]) & 0xff;
            int b2 = ((int)buffer[position+1]) & 0xff;
            return b1 + (b2 << 8);
        }
        public static int Extract32(byte[] buffer, int position)
        {
            int b1 = ((int)buffer[position]) & 0xff;
            int b2 = ((int)buffer[position + 1]) & 0xff;
            int b3 = ((int)buffer[position + 2]) & 0xff;
            int b4 = ((int)buffer[position + 3]) & 0xff;
            return b1 + (b2 << 8) + (b2 << 16) + (b3 << 24);
        }
    }

}
