/*  EV3-Basic: A basic compiler to target the Lego EV3 brick
    Copyright (C) 2017 Reinhard Grafl

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

namespace SmallBasicEV3Extension
{
    /// <summary>
    /// Manipulate individual bis of an 8-bit quantity.
    /// </summary>
    [SmallBasicType]
    public static class Byte
    {
        /// <summary>
        /// Get the name and mode of a currently connected sensor. 
        /// This function is mainly intended for diagnostic use because you normally know which sensor is plugged to which port on the model.
        /// </summary>
        /// <param name="port">Number of the sensor port</param>
        /// <returns>Description text (for example, "TOUCH")</returns>
        public static Primitive NOT(Primitive value)
        {
            int v = value;
            return new Primitive((~v) & 0xff);
        }

        public static Primitive AND_(Primitive a, Primitive b)
        {
            int va = a;
            int vb = b;
            return new Primitive((va & vb) & 0xff);
        }

        public static Primitive OR_(Primitive a, Primitive b)
        {
            int va = a;
            int vb = b;
            return new Primitive((va | vb) & 0xff);
        }

        public static Primitive XOR(Primitive a, Primitive b)
        {
            int va = a;
            int vb = b;
            return new Primitive((va ^ vb) & 0xff);
        }

        public static Primitive BIT(Primitive value, Primitive index)
        {
            int v = value;
            int i = index;
            if (i < 0 || i > 7) return new Primitive(0);
            return new Primitive((v>>i) & 1);
        }

        public static Primitive SHL(Primitive value, Primitive distance)
        {
            int v = value;
            int i = distance;
            if (i <= 0) return new Primitive(v & 0xff);
            if (i > 7) return new Primitive(0);
            return new Primitive((v << i) & 0xff);
        }

        public static Primitive SHR(Primitive value, Primitive distance)
        {
            int v = value;
            int i = distance;
            if (i <= 0) return new Primitive(v & 0xff);
            if (i > 7) return new Primitive(0);
            return new Primitive((v >> i) & 0xff);
        }

        public static Primitive ToHex(Primitive value)
        {
            int v = value;
            v = v & 0xff;
            return new Primitive(v.ToString("X2"));
        }

        public static Primitive ToBinary(Primitive value)
        {
            int v = value;
            v = v & 0xff;
            String s = Convert.ToString(v, 2);
            while (s.Length<8) s="0"+s;
            return new Primitive(s);
        }

        public static Primitive FromHex(Primitive value)
        {
            String s= (value==null)?"0":value.ToString();
            int i = 0;
            foreach (char c in s)
            {
                if (c >= 'a' && c<='f') i = i * 16 + (c-'a'+10);
                else if (c >= 'A' && c <= 'F') i = i * 16 + (c - 'A'+10);
                else if (c >= '0' && c <= '9') i = i * 16 + (c - '0');
            }
            return new Primitive(i & 0xff); 
        }

        public static Primitive FromBinary(Primitive value)
        {
            String s = (value == null) ? "0" : value.ToString();
            int i = 0;
            foreach (char c in s)
            {
                if (c >= '0' && c <= '1') i = i * 2 + (c-'0');
            }
            return new Primitive(i & 0xff);
        }

    }
}
