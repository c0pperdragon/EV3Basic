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
    /// Manipulate individual bits of an 8-bit numerical quantity.
    /// This library lets you treat Small Basic numbers as if they were organized as 8-bit integer values (a.k.a. "bytes"). To do so, the parameter values are always converted to plain bytes, then the requested operation is performed and then the result is converted back to a Small Basic number.
    /// The usual bit operations are supported: AND, OR, NOT, XOR, various shifts and data conversion operations. Note that the identifiers AND and OR are reserved words of Small Basic and so these operations are named AND_ and OR_ instead. For further information see https://en.wikipedia.org/wiki/Bitwise_operation .
    /// </summary>
    [SmallBasicType]
    public static class Byte
    {
        /// <summary>
        /// Bitwise negation. 
        /// </summary>
        /// <param name="value">Number to negate</param>
        /// <returns>The number you get when every bit of the input byte is individually inverted</returns>
        public static Primitive NOT(Primitive value)
        {
            int v = value;
            return new Primitive((~v) & 0xff);
        }

        /// <summary>
        /// Bitwise AND operation. 
        /// </summary>
        /// <param name="a">First operand</param>
        /// <param name="b">Second operand</param>
        /// <returns>The number you get when merging the two input bytes a and b by doing a binary AND operation on their individual bits</returns>
        public static Primitive AND_(Primitive a, Primitive b)
        {
            int va = a;
            int vb = b;
            return new Primitive((va & vb) & 0xff);
        }

        /// <summary>
        /// Bitwise OR operation. 
        /// </summary>
        /// <param name="a">First operand</param>
        /// <param name="b">Second operand</param>
        /// <returns>The number you get when merging the two input bytes a and b by doing a binary OR operation on their individual bits</returns>
        public static Primitive OR_(Primitive a, Primitive b)
        {
            int va = a;
            int vb = b;
            return new Primitive((va | vb) & 0xff);
        }

        /// <summary>
        /// Bitwise XOR operation. 
        /// </summary>
        /// <param name="a">First operand</param>
        /// <param name="b">Second operand</param>
        /// <returns>The number you get when merging the two input bytes a and b by doing a binary XOR operation on their individual bits</returns>
        public static Primitive XOR(Primitive a, Primitive b)
        {
            int va = a;
            int vb = b;
            return new Primitive((va ^ vb) & 0xff);
        }

        /// <summary>
        /// Extract a single bit from a byte.
        /// </summary>
        /// <param name="value">The byte number from where to extract the bit</param>
        /// <param name="index">Position of the bit inside the byte</param>
        /// <returns>The bit on the specified position which is either 0 or 1</returns>
        public static Primitive BIT(Primitive value, Primitive index)
        {
            int v = value;
            int i = index;
            i = i & 0xff;
            if (i > 7) return new Primitive(0);
            return new Primitive((v>>i) & 1);
        }

        /// <summary>
        /// Perform a bitwise shift operation to the left.
        /// </summary>
        /// <param name="value">The byte whose bits will be shifted</param>
        /// <param name="distance">By how many positions to shift the bits</param>
        /// <returns>The number you get after moving every bit of the input value towards the more significant positions</returns>
        public static Primitive SHL(Primitive value, Primitive distance)
        {
            int v = value;
            int i = distance;
            i = i & 0xff;
            if (i > 7) return new Primitive(0);
            return new Primitive((v << i) & 0xff);
        }

        /// <summary>
        /// Perform a bitwise shift operation to the right.
        /// </summary>
        /// <param name="value">The byte whose bits will be shifted</param>
        /// <param name="distance">By how many positions to shift the bits</param>
        /// <returns>The number you get after moving every bit of the input value towards the less significant positions</returns>
        public static Primitive SHR(Primitive value, Primitive distance)
        {
            int v = value;
            int i = distance;
            i = i & 0xff;
            if (i > 7) return new Primitive(0);
            return new Primitive((v&0xff) >> i);
        }

        /// <summary>
        /// Convert an 8-bit byte to its 2-digit hexadecimal string representation.
        /// </summary>
        /// <param name="value">The byte to convert into a string</param>
        /// <returns>A string holding 2 hexadecimal digits</returns>
        public static Primitive ToHex(Primitive value)
        {
            int v = value;
            v = v & 0xff;
            return new Primitive(v.ToString("X2"));
        }

        /// <summary>
        /// Convert an 8-bit byte to its 8-digit binary string representation.
        /// </summary>
        /// <param name="value">The byte to convert into a string</param>
        /// <returns>A string holding 8 binary digits</returns>
        public static Primitive ToBinary(Primitive value)
        {
            int v = value;
            v = v & 0xff;
            String s = Convert.ToString(v, 2);
            while (s.Length<8) s="0"+s;
            return new Primitive(s);
        }

        /// <summary>
        /// Convert a number (can be a 8-bit byte or any other number) to a logic value of either "True" or "False".
        /// This value can then be used for the condition in If or While or any other purpose.
        /// Note that any input value greater than 0 results in a "True" while an input value of 0 or any negative value results in "False".
        /// This specific behaviour allows some weird and wonderful things to be done with this command. Refer to the appendix for advanced logic operations.
        /// </summary>
        /// <param name="value">The numeric value to be converted into its corresponding logic value</param>
        /// <returns>Either "True" or "False"</returns>
        public static Primitive ToLogic(Primitive value)
        {
            double v = value;
            if (v > 0) return new Primitive("True");
            else       return new Primitive("False");
        }

        /// <summary>
        /// Convert a string that contains a hexadecimal value into a number.
        /// </summary>
        /// <param name="value">The string holding a byte in hexadecimal form (for example: "4F")</param>
        /// <returns>The byte as number</returns>
        public static Primitive H(Primitive value)
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

        /// <summary>
        /// Convert a string that contains a binary value into a number.
        /// </summary>
        /// <param name="value">The string holding a byte in binary form (for example: "01001111")</param>
        /// <returns>The byte as number</returns>
        public static Primitive B(Primitive value)
        {
            String s = (value == null) ? "0" : value.ToString();
            int i = 0;
            foreach (char c in s)
            {
                if (c >= '0' && c <= '1') i = i * 2 + (c-'0');
            }
            return new Primitive(i & 0xff);
        }

        /// <summary>
        /// Convert a string that contains a logic value into a numerical 0 or 1.
        /// </summary>
        /// <param name="value">The string holding a logic value. All case-insensitive variants of "True" ("TRUE","TrUe", "truE", etc.) are considered the same. Everything else is treated as "False".</param>
        /// <returns>0 or 1</returns>
        public static Primitive L(Primitive value)
        {
            int v = (value == null ? "" : value.ToString()).Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            return new Primitive(v);
        }

    }
}
