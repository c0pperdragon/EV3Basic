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

namespace LMSAssembler
{

    public class VMCommand
    {
        public readonly String name;
        public readonly byte[] opcode;

        public readonly DataType[] parameters;
        public readonly AccessType[] access;


        public VMCommand(String descriptor)
        {
            char[] delimiters = new char[] { '\t', ' ' };
            String[] tokens = descriptor.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

            int pstart = 0;
            if (tokens[0].Length==2 && tokens.Length>=2)
            {
                opcode = new byte[] { (byte) Convert.ToInt32(tokens[0], 16) };
                name = tokens[1];
                pstart = 2;
            }
            else if (tokens[0].Length==4 && tokens.Length>=3)
            {
                opcode = new byte[] { (byte)Convert.ToInt32(tokens[0].Substring(0,2), 16) 
                                    , (byte)Convert.ToInt32(tokens[0].Substring(2,2), 16) 
                                    };
                name = tokens[1] + " " + tokens[2];
                pstart = 3;
            }
            else
            {
                throw new Exception("Can not decode definition list");
            }

            int nump = tokens.Length - pstart;
            parameters = new DataType[nump];
            access = new AccessType[nump];
            for (int i = 0; i < nump; i++)
            {
                String t = tokens[pstart + i];
                if (t.StartsWith("8"))
                {
                    parameters[i] = DataType.I8;
                }
                else if (t.StartsWith("16"))
                {
                    parameters[i] = DataType.I16;
                }
                else if (t.StartsWith("32"))
                {
                    parameters[i] = DataType.I32;
                }
                else if (t.StartsWith("F"))
                {
                    parameters[i] = DataType.F;
                }
                else if (t.StartsWith("?"))
                {
                    parameters[i] = DataType.Unspecified;
                }
                else if (t.Equals("L"))
                {
                    parameters[i] = DataType.Label;
                }
                else if (t.Equals("T"))
                {
                    parameters[i] = DataType.VMThread;
                }
                else if (t.Equals("S"))
                {
                    parameters[i] = DataType.VMSubcall;
                }
                else if (t.Equals("P"))
                {
                    parameters[i] = DataType.ParameterCount;
                }
                else
                {
                    throw new Exception("Can not read opcode descriptor: "+descriptor);
                }

                if (t.EndsWith("*"))
                {
                    access[i] = AccessType.Write;
                }
                else if (t.EndsWith("+"))
                {
                    access[i] = AccessType.ReadMany;
                }
                else
                {
                    access[i] = AccessType.Read;
                }
            }
        }

        public override String ToString()
        {            
            String s = BitConverter.ToString(opcode) + " "+ name;
            for (int i = 0; i < parameters.Length; i++)
            {
                s = s + " " + parameters[i] + access[i];
            }
            return s;
        }
    }


}
