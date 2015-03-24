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
using System.IO;

namespace LMSAssembler
{
    class DataReader
    {

        public static int Read32(Stream stream, ref int readposition)
        {
            int b0 = stream.ReadByte() & 0xff;
            int b1 = stream.ReadByte() & 0xff;
            int b2 = stream.ReadByte() & 0xff;
            int b3 = stream.ReadByte() & 0xff;
            readposition += 4;
            return b0 | (b1<<8) | (b2<<16) | (b3<<24);
        }

        public static int Read16(Stream stream, ref int readposition)
        {
            int b0 = stream.ReadByte() & 0xff;
            int b1 = stream.ReadByte() & 0xff;
            readposition += 2;
            return (short) (b0 | (b1 << 8));
        }

        public static int Read8(Stream stream, ref int readposition)
        {
            int b0 = stream.ReadByte() & 0xff; 
            readposition += 1;
            return (sbyte) b0;            
        }

    }

   
}
