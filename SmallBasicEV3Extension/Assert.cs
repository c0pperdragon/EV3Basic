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

namespace SmallBasicEV3Extension
{
    /// <summary>
    /// A test facility to help check part of the code for correctness. 
    /// Assertions make implicit assumptions about the current program state explicit. By adding assertion calls you can find bugs in your program more easily. For example, when a part of the program depends on the variable A having a positive value, you could call  Assert.Greater(A,0,"A must be > 0!"). 
    /// In the case that the program runs into an assertion that is not satisfied, the error message is displayed stating the problem.
    /// </summary>
    [SmallBasicType]
    public class Assert
    {

        /// <summary>
        /// Write a failure message to the display. This function should only be called if something has already failed in the program. 
        /// </summary>
        /// <param name="message">Message to be displayed</param>
        public static void Failed(Primitive message)
        {
            TextWindow.WriteLine("ASSERT FAILED: " + message);
        }

        /// <summary>
        /// Make sure that two values are equal. For this test, even "True" and "tRue" are not considered equal.
        /// </summary>
        /// <param name="a">First value</param>
        /// <param name="b">Second value</param>
        /// <param name="message">Message that will be displayed if the assertion fails</param>
        public static void Equal(Primitive a, Primitive b, Primitive message)
        {
            String sa = a==null ? "" : a.ToString();
            String sb = b==null ? "" : b.ToString();
            float fa, fb;

            if (float.TryParse(sa, out fa) && float.TryParse(sb, out fb))
            {
                if (fa!=fb)
                {
                    Failed(message);
                }
            }
            else if (!sa.Equals(sb))
            {
                Failed(message);
            }
        }

        /// <summary>
        /// Make sure that two values are not equal. For this test, even "True" and "tRue" are not considered equal.
        /// </summary>
        /// <param name="a">First value</param>
        /// <param name="b">Second value</param>
        /// <param name="message">Message that will be displayed if the assertion fails</param>
        public static void NotEqual(Primitive a, Primitive b, Primitive message)
        {
            String sa = a == null ? "" : a.ToString();
            String sb = b == null ? "" : b.ToString();
            if (sa.Equals(sb))
            {
                Failed(message);
            }
        }

        /// <summary>
        /// Make sure that the first number is less than the second.
        /// </summary>
        /// <param name="a">First value</param>
        /// <param name="b">Second value</param>
        /// <param name="message">Message that will be displayed if the assertion fails</param>
        public static void Less(Primitive a, Primitive b, Primitive message)
        {
            String sa = a == null ? "" : a.ToString();
            String sb = b == null ? "" : b.ToString();
            float _a, _b;
            if (!float.TryParse(sa, out _a))
            {
                Failed(message);
                return;
            }
            if (!float.TryParse(sb, out _b))
            {
                Failed(message);
                return;
            }
            if (! (_a<_b) )
            {
                Failed(message);
            }
        }

        /// <summary>
        /// Make sure that the first number is greater than the second.
        /// </summary>
        /// <param name="a">First value</param>
        /// <param name="b">Second value</param>
        /// <param name="message">Message that will be displayed if the assertion fails</param>
        public static void Greater(Primitive a, Primitive b, Primitive message)
        {
            String sa = a == null ? "" : a.ToString();
            String sb = b == null ? "" : b.ToString();
            float _a, _b;
            if (!float.TryParse(sa, out _a))
            {
                Failed(message);
                return;
            }
            if (!float.TryParse(sb, out _b))
            {
                Failed(message);
                return;
            }
            if (!(_a > _b))
            {
                Failed(message);
            }
        }

        /// <summary>
        /// Make sure that the first number is less than or equal to the second.
        /// </summary>
        /// <param name="a">First value</param>
        /// <param name="b">Second value</param>
        /// <param name="message">Message that will be displayed if the assertion fails</param>
        public static void LessEqual(Primitive a, Primitive b, Primitive message)
        {
            String sa = a == null ? "" : a.ToString();
            String sb = b == null ? "" : b.ToString();
            float _a, _b;
            if (!float.TryParse(sa, out _a))
            {
                Failed(message);
                return;
            }
            if (!float.TryParse(sb, out _b))
            {
                Failed(message);
                return;
            }
            if (!(_a <= _b))
            {
                Failed(message);
            }
        }

        /// <summary>
        /// Make sure that the first number is greater than or equal to the second.
        /// </summary>
        /// <param name="a">First value</param>
        /// <param name="b">Second value</param>
        /// <param name="message">Message that will be displayed if the assertion fails</param>
        public static void GreaterEqual(Primitive a, Primitive b, Primitive message)
        {
            String sa = a == null ? "" : a.ToString();
            String sb = b == null ? "" : b.ToString();
            float _a, _b;
            if (!float.TryParse(sa, out _a))
            {
                Failed(message);
                return;
            }
            if (!float.TryParse(sb, out _b))
            {
                Failed(message);
                return;
            }
            if (!(_a >= _b))
            {
                Failed(message);
            }
        }

        /// <summary>
        /// Make sure that the two numbers are nearly identical. This can be used for fractional numbers with many decimal places where the computation could give slightly different results because of rounding issues.
        /// </summary>
        /// <param name="a">First value</param>
        /// <param name="b">Second value</param>
        /// <param name="message">Message that will be displayed if the assertion fails</param>
        public static void Near(Primitive a, Primitive b, Primitive message)
        {
            String sa = a == null ? "" : a.ToString();
            String sb = b == null ? "" : b.ToString();
            float _a, _b;
            if (!float.TryParse(sa, out _a))
            {
                Failed(message);
                return;
            }
            if (!float.TryParse(sb, out _b))
            {
                Failed(message);
                return;
            }

            if (_a<_b-0.0000001 || _a>_b+0.0000001)
            {
                Failed(message);
            }
        }
    }
}
