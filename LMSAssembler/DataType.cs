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
    // data types for variables and parameters
    public enum DataType { 
        I8,            // 8 bit signed int
        I16,           // 16 bit signed int
        I32,           // 32 bit signed int
        F,             // 4 byte floating point
        Unspecified,   // unspecified reference parameter (can not be used for variables)
        Label,         // opcode needs a jump label (can not be used for variables or parameters)
        VMThread,      // VM execution thread
        VMSubcall,     // VM subcall
        ParameterCount // a constant value specifying the number of parameters to follow
    };

    // specifies what the opcode (or a subcall) will do to the parameters
    public enum AccessType {
        Read,               // only read the value (could also be a constant)
        ReadMany,           // directy read from memory and may also read subsequent elements if it is an array
        Write,              // write output to variable (but does not read)
        ReadWrite           // read and write the variable
    };

    // performs checks if the current datatype of a calling argument fits the defined datatype of the callee
    // if not, throws an AssemblerException

    public class DataTypeChecker
    {
        // the argument value can be:  int, double, String, DataElement

        public static void check(Object argument, DataType parameter_datatype, AccessType parameter_accesstype)
        {
            // check if it is permitted to pass this variable
            if (argument is DataElement)
            {
                DataElement e = (DataElement)argument;
                DataType et = e.datatype;
                if (et != parameter_datatype && parameter_datatype!=DataType.Unspecified)
                {
                    throw new AssemblerException("Using variable of wrong type for call: " + e.name);
                }
            }
            // check in which cases a constant integer value is permitted
            else if (argument is int)
            {
                int c = (int)argument;
                if (parameter_accesstype == AccessType.ReadMany && parameter_datatype == DataType.I8)
                {
                    throw new AssemblerException("Using constant value as parameter where a string value or variable reference is required");
                }
                if (parameter_accesstype != AccessType.Read)
                {
                    throw new AssemblerException("Using constant value as parameter where a variable reference is required");
                }
                switch (parameter_datatype)
                {
                    case DataType.I8:
                        if (c < -128 || c > 127)
                        {
                            throw new AssemblerException("Constant value " + c + "+ out of range of I8");
                        }
                        break;
                    case DataType.I16:
                    case DataType.VMThread:
                        if (c < -32768 || c > 32767)
                        {
                            throw new AssemblerException("Constant value " + c + "+ out of range of I16");
                        }
                        break;
                    case DataType.I32:
                        break;      // must be implicitly in range
                    default:
                        throw new AssemblerException("Constant value " + c + " does not fit the parameter type "+parameter_datatype);
                }
            }
            // check in which cases a constant float value is permitted
            else if (argument is double)
            {
                if (parameter_datatype != DataType.F && parameter_datatype != DataType.Unspecified)
                {
                    throw new AssemblerException("Can not use float literal '" + argument + "' for this parameter type");
                }
                if (parameter_accesstype != AccessType.Read)
                {
                    throw new AssemblerException("Can not use float literal '" + argument + "' for output parameter");
                }
            }
            // check in which cases a string literal is permitted
            else if (argument is String)
            {
                if (parameter_datatype != DataType.I8)
                {
                    throw new AssemblerException("Can not use string literal '" + argument + "' for this parameter type");
                }
                if (parameter_accesstype != AccessType.Read && parameter_accesstype != AccessType.ReadMany)
                {
                    throw new AssemblerException("Can not use string literal '" + argument + "' for output parameter");
                }
            }
        }
    }

}
