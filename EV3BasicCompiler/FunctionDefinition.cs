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
using System.Globalization;

namespace EV3BasicCompiler
{

    public class FunctionDefinition
    {
        public readonly String fname;
        public readonly String startsub;
        public readonly String[] paramnames;
        public readonly Object[] defaultvalues;

        private Dictionary<ExpressionType, int> reservedtemporaries;
        private Dictionary<ExpressionType, int> maxreservedtemporaries;
        private ExpressionType returnType;

        public FunctionDefinition(String fname, String startsub, String[] paramnames, Object[] defaultvalues)
        {
            this.fname = fname;
            this.startsub = startsub;
            this.paramnames = paramnames;
            this.defaultvalues = defaultvalues;

            reservedtemporaries = new Dictionary<ExpressionType, int>();
            maxreservedtemporaries = new Dictionary<ExpressionType, int>();
            returnType = ExpressionType.Void;
        }

        public override String ToString()
        {
            String s = "\"" + fname + "\" " + startsub + "(";
            for (int i = 0; i < paramnames.Length; i++)
            {
                s = s + " " + paramnames[i] + ":" + tostring(defaultvalues[i]);
            }
            s = s + "): "+returnType;
            return s;
        }
        private static String tostring(Object v)
        {
            if (v is double[])
            {
                String s = "" + ((double[])v)[0];
                if (s.IndexOf('.') < 0) s = s + ".0";
                return s;
            }
            return "'" + v.ToString() + "'";
        }

        public int findParameter(String name)
        {
            for (int i=0; i<paramnames.Length; i++)
            {
                if (paramnames[i].Equals(name))
                { return i;
                }
            }
            return -1;
        }

        public int getParameterNumber()
        {
            return this.paramnames.Length;
        }

        public String getParameterDefaultLiteral(int index)
        {
            return tostring(defaultvalues[index]);
        }

        public ExpressionType getParameterType(int index)
        {
            if (defaultvalues[index] is String) return ExpressionType.Text;
            else return ExpressionType.Number;
        }

        public String getParameterVariable(int index)
        {
            switch (getParameterType(index))
            {
                case ExpressionType.Number:
                    return "F" + fname+ "." + paramnames[index];
                case ExpressionType.Text:
                    return "S" + fname + "." + paramnames[index];
                default:
                    return null;
            }
        }


        public ExpressionType getReturnType()
        {
            return returnType;
        }

        public String getReturnVariable()
        {
            switch (getReturnType())
            {
                case ExpressionType.Number:
                    return "F" + fname + "." ;
                case ExpressionType.Text:
                    return "S" + fname + ".";
                default:
                    return "";
            }
        }

        public void setReturnType(ExpressionType t)
        {
            returnType = t;
        }

        public String reserveVariable(ExpressionType type)
        {
            if (!reservedtemporaries.ContainsKey(type))
            {
                reservedtemporaries[type] = 0;
                maxreservedtemporaries[type] = 0;
            }
            int n = reservedtemporaries[type] + 1;
            reservedtemporaries[type] = n;
            maxreservedtemporaries[type] = Math.Max(n, maxreservedtemporaries[type]);
            switch (type)
            {
                case ExpressionType.Number:
                    return "F" + fname + "." + (n - 1);
                case ExpressionType.Text:
                    return "S" + fname + "." + (n - 1);
                default:
                    return null;
            }
        }

        public void releaseVariable(ExpressionType type)
        {
            reservedtemporaries[type]--;
        }

        public int getMaxReserved(ExpressionType type)
        {
            return maxreservedtemporaries.ContainsKey(type) ? maxreservedtemporaries[type] : 0;
        }


        public List<String> getAllLocalVariables(ExpressionType type)
        {
            String prefix = (type == ExpressionType.Number ? "F" : "S") + fname + ".";

            List<String> l = new List<String>();
            if (getReturnType()==type)
            {
                l.Add(prefix);
            }
            for (int i = 0; i < paramnames.Length; i++)
            {
                if (getParameterType(i)==type)
                {
                    l.Add(prefix + paramnames[i]);
                }
            }
            for (int i=0; i<getMaxReserved(type); i++)
            {
                l.Add(prefix + i);
            }
            return l;
        }

        public List<String> getCurrentLocalVariables(ExpressionType type)
        {
            String prefix = (type == ExpressionType.Number ? "F" : "S") + fname + ".";

            List<String> l = new List<String>();
            for (int i = 0; i < paramnames.Length; i++)
            {
                if (getParameterType(i) == type)
                {
                    l.Add(prefix + paramnames[i]);
                }
            }
            if (reservedtemporaries.ContainsKey(type))
            {
                for (int i = 0; i < reservedtemporaries[type]; i++)
                {
                    l.Add(prefix + i);
                }
            }
            return l;
        }


        public static FunctionDefinition make(String fname, String startsub, String pardeclarator)
        {
            double val;
            String[] parlist = pardeclarator.Split(new Char[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            Object[] defaultvalues = new Object[parlist.Length];

            for (int i=0; i<parlist.Length; i++)
            {
                int colon = parlist[i].IndexOf(':');
                if (colon>0)
                {
                    String v = parlist[i].Substring(colon+1);
                    parlist[i]= parlist[i].Substring(0,colon).ToUpperInvariant();
                    if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                    {
                        defaultvalues[i] = new double[]{val};
                    }            
                    else
                    {   
                        defaultvalues[i] = v; 
                    }
                }
                else
                {
                    parlist[i] = parlist[i].ToUpperInvariant();
                    defaultvalues[i] = new double[] { 0.0 };
                }
            }
            
            return new FunctionDefinition(fname, startsub, parlist, defaultvalues);
        }
    }

}
