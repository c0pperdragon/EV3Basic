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
    public class ByteCodeBuffer : BinaryBuffer
    {
        public ByteCodeBuffer() : base()
        { }

        public void OP(byte opcode)
        {
            Append8(opcode);
        }

        public void CONST(int par)
        {
            if (par >= -32 && par <= 31)
            {
                Append8(par & 0x3f);
            }
            else if (par >= -128 && par <= 127)
            {
                Append8(0x81);
                Append8(par);
            }
            else if (par >= -32768 && par <= 32767)
            {
                Append8(0x82);
                Append16(par);
            }
            else
            {
                Append8(0x83);
                Append32(par);
            }
        }

        public void GLOBVAR(int var)
        {
            if (var <= 31)
            {
                Append8((var & 0x1f) | 0x60);
            }
            else if (var <= 255)
            {
                Append8(0xe1);
                Append8(var);
            }
            else if (var <= 65535)
            {
                Append8(0xe2);
                Append16(var);
            }
            else
            {
                Append8(0xe3);
                Append32(var);
            }
        }

        public void LOCVAR(int var)
        {
            if (var <= 31)
            {
                Append8((var & 0x1f) | 0x40);
            }
            else if (var <= 255)
            {
                Append8(0xc1);
                Append8(var);
            }
            else if (var <= 65535)
            {
                Append8(0xc2);
                Append16(var);
            }
            else
            {
                Append8(0xc3);
                Append32(var);
            }
        }

        public void STRING(String text)
        {
            Append8(0x84);
            AppendZeroTerminated(text);
        }

    }
}
