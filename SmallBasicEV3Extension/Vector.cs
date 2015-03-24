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
    ///  This object allows direct manipulation of larger quantities of numbers. 
    ///  These are called vectors and will be stored using arrays with consecutive indizes (starting at 0).
    ///  When arrays with different content are given to the operations, every missing array 
    ///  element with be treated as being 0.
    /// </summary>

    [SmallBasicType]
    public static class Vector
    {
        /// <summary>
        /// Set up a vector of a given size with all elements set to the same value.
        /// </summary>
        /// <param name="size">Size of the vector</param>
        /// <param name="value">The value to use for all elements</param>
        /// <returns>The created vector</returns>
        public static Primitive Init(Primitive size, Primitive value)
        {
            int _size = size;
            double _value;
            if (!double.TryParse(value==null ? "0" : value.ToString(), out _value))
            {
               _value = 0;
            }

            double[] a = new double[_size<0 ? 0 : _size];
            for (int i=0; i<a.Length; i++)
            {
                a[i] = _value;
            }
            return A2P(a);
        }


        /// <summary>
        /// Adds two vectors by adding the individual elements (C[0]=A[0]+B[0], C[1]=A[1]+B[2]...)
        /// </summary>
        /// <param name="size">That many numbers are taken for computation</param>
        /// <param name="A">First vector</param>
        /// <param name="B">Second vector</param>
        /// <returns>A vector of the given size what contains sum values.</returns>
        public static Primitive Add(Primitive size, Primitive A, Primitive B)
        {
            double[] a = P2A(A,size);
            double[] b = P2A(B,size);
            double[] c = new double[a.Length];
            for (int i=0; i<c.Length; i++)
            {   c[i] = a[i]+b[i];
            }
            return A2P(c);
        }

        /// <summary>
        /// Sort the elements of a vector in increasing order.
        /// </summary>
        /// <param name="size">Number of elements to sort</param>
        /// <param name="A">The array containing the elements</param>
        /// <returns>A new vector with the elements in correct order</returns>
        public static Primitive Sort(Primitive size, Primitive A)
        {
            double[] a = P2A(A, size);
            System.Array.Sort(a);
            return A2P(a);
        }
/*
        public static Primitive SortTable(Primitive rows, Primitive columns, Primitive sortcolumn, Primitive A)
        {
            int _rows = rows;
            int _cols = columns;
            int _sortcolumn = sortcolumn;
            if (_rows < 0)
            {
                _rows = 0;
            }
            if (_cols < 0)
            {
                _cols = 0;
            }

            // extract elements from Primitive
            double[] a = P2A(A, _rows*_cols);

            // split into individual rows
            double[][] x = new double[_rows][];
            for (int i = 0; i < rows; i++)
            {
                x[i] = new double[_cols];
                System.Array.Copy(a, i * _cols, x[i], 0, _cols);
            }

            if (sortcolumn>=0 && sortcolumn<columns)
            {   
                System.Array.Sort(x, new DoubleArrayComparer(sortcolumn));
            }

            // merge the rows
            for (int i = 0; i < rows; i++)
            {
                System.Array.Copy(x[i], 0, a, i * _cols, _cols);
            }

            // convert to Primitive again
            return A2P(a);
        }
*/

        /// <summary>
        /// Matrix multipliation operation. 
        /// The input vectors are treated as two-dimensional matrizes of given width and height. The individual rows of the matrix are stored inside the vectors directly one after the other.
        /// To learn more about this mathematical operation see http://en.wikipedia.org/wiki/Matrix_multiplication .
        /// </summary>
        /// <param name="rows">Number of rows in the resulting output matrix</param>
        /// <param name="columns">Number of columns in the resulting output matrix</param>
        /// <param name="k">Number of columns in input matrix A and number of rows in input matrix B</param>
        /// <param name="A">A matrix of size rows * k</param>
        /// <param name="B">A matrix of size k * columns</param>
        /// <returns>A matrix holding the multiplication result</returns>
        public static Primitive Multiply(Primitive rows, Primitive columns, Primitive k, Primitive A, Primitive B)
        {
            int _rows = rows;
            int _cols = columns;
            int _k = k;
            if (_rows<0)
            {  _rows = 0;
            }
            if (_cols<0)
            {  _cols = 0;
            }
            if (_k<0)
            {  _k = 0;
            }

            double[] a = P2A(A,rows*k);
            double[] b = P2A(B,k*columns);
            double[] c = new double[rows*columns];
            for (int i=0; i<rows; i++)
            {
                for (int j=0; j<columns; j++)
                {
                    double sum = 0;
                    for (int x=0; x<k; x++)
                    {
                        sum = sum + a[k*i+x] * b[columns*x+j];
                    }
                    c[i*columns+j] = sum;
                }
            }

            return A2P(c);
        }


        // ---------------------------- simple conversion methods ----------------------------
        private static double[] P2A(Primitive p, int size)
        {
            double[] a = new double[size<0 ? 0:size];
            for (int i = 0; i < a.Length; i++)
            {
                Primitive v = p==0 ? null : Primitive.GetArrayValue(p, new Primitive((double)i));
                double.TryParse(v==null ? "0" : v.ToString(), out a[i]);
            }
            return a;
        }

        private static Primitive A2P(double[] array)
        {
            Dictionary<Primitive, Primitive> map = new Dictionary<Primitive, Primitive>();
            for (int i = 0; i < array.Length; i++)
            {
                map[new Primitive((double)i)] = new Primitive(array[i]);
            }
            return Primitive.ConvertFromMap(map);
        }


        private class DoubleArrayComparer : IComparer<double[]>
        {
            int sortcolumn;

            public DoubleArrayComparer(int sortcolumn)
            {
                this.sortcolumn = sortcolumn;
            }

            public int Compare(double[] x, double[] y)
            {
                double a = x[sortcolumn];
                double b = y[sortcolumn];
                return a < b ? -1 : (a > b ? 1 : 0);
            }
        }    
    }



}
