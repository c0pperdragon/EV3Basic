using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.SmallBasic.Library;

namespace SmallBasicEV3Extension
{
    /// <summary>
    /// A test facility to help check part of the code for correctness. 
    /// Assertions make implicit assumptions about the current program state explicit. By adding assertion calls you can help finding bugs in your program more easily.
    /// e.g.: When a part of the program depends on the variable A having a positive value, you could call  Assert.Greater(A,0,"A must be > 0!"). 
    /// In the case that the program runs into an assertion that is not satisfied, the error message is displayed stating the problem.
    /// </summary>
    [SmallBasicType]
    public class Assert
    {

        /// <summary>
        /// Write a failure message to the TextWindow. This function should only be called if something has already failed in the program. 
        /// </summary>
        /// <param name="message"></param>
        public static void Failed(Primitive message)
        {
            TextWindow.WriteLine("ASSERT FAILED: " + message);
        }

        /// <summary>
        /// Make sure that two values are equal. For this test, even "True" and "tRue" are not considered equal.
        /// </summary>
        /// <param name="a">First value</param>
        /// <param name="b">Second value</param>
        /// <param name="message">Message that will be displayed if the assertion fails.</param>
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
        /// <param name="message">Message that will be displayed if the assertion fails.</param>
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
        /// <param name="message">Message that will be displayed if the assertion fails.</param>
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
        /// <param name="message">Message that will be displayed if the assertion fails.</param>
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
        /// <param name="message">Message that will be displayed if the assertion fails.</param>
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
        /// <param name="message">Message that will be displayed if the assertion fails.</param>
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
        /// Make sure that the two numbers are nearly identical. This can be used for fractional numbers with many decimal places where 
        /// the computation could give slightly different results because of rounding issues.
        /// </summary>
        /// <param name="a">First value</param>
        /// <param name="b">Second value</param>
        /// <param name="message">Message that will be displayed if the assertion fails.</param>
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
