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

    public abstract class LMSObject
    {
        public readonly String name;
        public readonly int id;

        protected MemoryStream program;

        public readonly DataArea locals;

        private Dictionary<String, int> labels;
        private Dictionary<int, String> references;

        protected int offsetToInstructions;

        public LMSObject(String name, int id)
        {
            this.name = name;
            this.id = id;
            this.program = null;
            this.locals = new DataArea();
            this.labels = new Dictionary<String, int>();
            this.references = new Dictionary<int,String>();
            this.offsetToInstructions = 0;
        }

        public virtual void StartCode()
        {
            if(program!=null)
            {
                throw new AssemblerException("Duplicate definition of " + name);
            }
            program = new MemoryStream();
        }

        public void AddOpCode(byte[] opcode)
        {
            program.WriteByte(opcode[0]);
            if (opcode.Length>1)
            {   
                AddConstant(opcode[1]);
            }
        }

        public void AddConstant(int value)
        {
            AddConstant(value, 0);
        }

        public void AddConstant(int value, int minimumencodingbytes)
        {
            if (value>=-32 && value<=31 && minimumencodingbytes<=1)
            {
                program.WriteByte( (byte) (value & 0x3f) );
            }
            else if (value >= -128 && value <= 127 && minimumencodingbytes <= 2)
            {
                program.WriteByte((byte)0x81);
                program.WriteByte((byte)value);
            }
            else if (value >= -32768 && value <= 32767 && minimumencodingbytes <= 3)
            {
                program.WriteByte((byte)0x82);
                DataWriter.Write16(program,value);
            }
            else 
            {
                program.WriteByte((byte)0x83);
                DataWriter.Write32(program,value);
            }
        }
        public void AddVariableReference(int index, bool local)
        {
            if (index < 0)
            {
                throw new AssemblerException("Negative variable index!");
            }
            if (index<=31)
            {
                program.WriteByte( (byte) ( (local?0x40:0x60) | index ) );
            }
            else if (index >= -127 && index <= 127)
            {
                program.WriteByte((byte) (local?0xc1:0xe1));
                program.WriteByte((byte)index);
            }
            else if (index >= -32768 && index <= 32767)
            {
                program.WriteByte((byte) (local?0xc2:0xe2));
                DataWriter.Write16(program, index);
            }
            else
            {
                program.WriteByte((byte) (local?0xc3:0xe3));
                DataWriter.Write32(program, index);
            }
        }
        public void AddStringLiteral(String s)
        {
            program.WriteByte(0x80);
            for (int i = 0; i < s.Length; i++)
            {
                int c = s[i];
                if (c <= 0 || c > 255)
                {
                    throw new AssemblerException("String literal contains non-ascii character");
                }
                program.WriteByte((byte)c);
            }
            program.WriteByte(0);
        }
        public void AddFloatConstant(double fvalue)
        {
            // float values can be embedded in code as a 4-byte value
            program.WriteByte((byte)0x83);
            byte[] b = BitConverter.GetBytes((Single)fvalue);
//            Console.WriteLine("Embedding float: " + fvalue);
            for (int i = 0; i < b.Length; i++)
            {
                program.WriteByte(b[i]);
//                Console.WriteLine("b" + i + ": " + b[i].ToString("X"));
            }
        }

        public void AddLabelReference(String label)
        {
            // when the label is already defined, write the reference right now
            if (labels.ContainsKey(label))
            {
                int target = labels[label];
                // which number to write depends on the length of the parameter encoding:
                // for smaller values a compacter encoding is possible that will lead to a different
                // position of the parameter end (which is the reference for the value itself)
                int distancefromparameterstart = target - (int) program.Length;
                if (distancefromparameterstart>0)
                {
                    throw new Exception("Internal error: Forward label already known");
                }
                if (distancefromparameterstart-1 >= -32)   
                {
                    AddConstant(distancefromparameterstart - 1, 1);
                }
                else if (distancefromparameterstart-2 >= -128)
                {
                    AddConstant(distancefromparameterstart - 2, 2);
                }
                else if (distancefromparameterstart-3 >= -32768)  
                {
                    AddConstant(distancefromparameterstart - 3, 3);
                }
                else
                {
                    AddConstant(distancefromparameterstart - 5, 5);
                }
            }
            // put in empty placeholder and memorize location for back-patching
            else
            {
                // memorize that the label was referenced from here
                references[((int)program.Length)+1] = label;

                // only placeholder that will be replaced at back-patching.
                AddConstant(0, 4);    // 32bit long addressing
            }
        }

        public void AddLabelDifference(String descriptor)
        {
            // memorize that the label difference was referenced from here
            references[((int)program.Length) + 1] = descriptor;
            AddConstant(0, 4);   // placeholder for 32bit constant - will be patched later
        }

        public void MemorizeLabel(String label)
        {
            labels[label] = (int)program.Length;
        }


        public virtual void MemorizeIOParameter(DataType dt, AccessType pt)
        {
            throw new AssemblerException("Can not add IN or OUT data to this object");
        }
        public virtual void MemorizeStringIOParameter(int size, AccessType pt)
        {
            throw new AssemblerException("Can not add IN or OUT data to this object");
        }


        public virtual void WriteByteCodes(Stream stream, int offsetToInstructions)
        {
            this.offsetToInstructions = offsetToInstructions;

            if (program==null)
            {
                throw new AssemblerException("Unresolved subcall: " + name);
            }

            byte[] b = program.GetBuffer();
            int l = (int) program.Length;

            for (int i = 0; i < l; )
            {
                // check if encountering a reference
                if (references.ContainsKey(i))
                {
                    String label = references[i];
                    int colonidx = label.IndexOf(':');
                    // this is a label difference calculation
                    if (colonidx > 0)
                    {
                        String firstlabel = label.Substring(0, colonidx);
                        String secondlabel = label.Substring(colonidx + 1);
                        if ((!labels.ContainsKey(firstlabel)) || (!labels.ContainsKey(secondlabel)))
                        {
                            throw new AssemblerException("Unresolved label distance: " + label);
                        }
                        DataWriter.Write32(stream, labels[secondlabel] - labels[firstlabel]);
                        i += 4;
                    }
                    // normal jump label reference
                    else
                    {
                        if (!labels.ContainsKey(label))
                        {
                            throw new AssemblerException("Unresolved jump target: " + label);
                        }
                        DataWriter.Write32(stream, labels[label] - (i + 4));  // relative jump distance
                        i += 4;
                    }
                }
                else
                {
                    stream.WriteByte(b[i]);
                    i++;
                }
            }
        }

        public abstract void WriteHeader(Stream stream);
        public abstract void WriteBody(Stream stream, int offsetToInstructions);



        public virtual void print()
        {
            locals.print();

            byte[] a = program.ToArray();
            for (int i=0; i<a.Length; i++)
            {
                Console.Write(a[i].ToString("X2"));
                if (i % 16 == 15 || i == a.Length - 1)
                {
                    Console.WriteLine();
                }
            }
        }
    }

    class LMSThread : LMSObject
    {
        public LMSThread(String n, int id) : base(n, id)
        {
        }

        public override void WriteHeader(Stream stream)
        {
            DataWriter.Write32(stream, offsetToInstructions);
            DataWriter.Write16(stream,0);
            DataWriter.Write16(stream,0);
            DataWriter.Write32(stream, locals.TotalBytes());
        }
        public override void WriteBody(Stream stream, int offsetToInstructions)
        {
            WriteByteCodes(stream, offsetToInstructions);
            stream.WriteByte(0x0A);  // OBJECT_END
        }

    }

    class LMSSubCall : LMSObject
    {
        List<DataType> ioDataTypes;
        List<AccessType> ioAccessTypes;
        List<int> ioStringSizes;            // if non-0 this is a IN_S OUT_S or IO_S definition 

        List<List<Object>> callerMemorization;

        LMSSubCall implementation;    // holds the code for this LMSSubCall which does not have own data

        public LMSSubCall(String n, int id) : base(n, id)
        {
            ioDataTypes = new List<DataType>();
            ioAccessTypes = new List<AccessType>();
            ioStringSizes = new List<int>();

            callerMemorization = new List<List<Object>>();
            implementation = null;
        }

        public override void StartCode()
        {
            if (program != null || implementation!=null)
            {
                throw new AssemblerException("Duplicate definition of " + name);
            }
            program = new MemoryStream();
        }

        public void SetImplementation(LMSSubCall impl)
        {
            if (program != null || implementation != null)
            {
                throw new AssemblerException("Duplicate definition of " + name);
            }
            implementation = impl;
        }


        override public void MemorizeIOParameter(DataType dt, AccessType at)
        {
            ioDataTypes.Add(dt);
            ioAccessTypes.Add(at);
            ioStringSizes.Add(0);
        }
        override public void MemorizeStringIOParameter(int size, AccessType pt)
        {
            if (size<1 || size>255)
            {
                throw new AssemblerException("Length of IO parameter must not exceed 255 bytes");
            }
            ioDataTypes.Add(DataType.I8);
            ioAccessTypes.Add(pt);
            ioStringSizes.Add(size);
        }


        public void StartCallerMemorization()
        {
            callerMemorization.Add(new List<Object>());
        }

        public void MemorizeCallerParameter(Object p)
        {
            callerMemorization[callerMemorization.Count-1].Add(p);
        }

        public override void WriteHeader(Stream stream)
        {
            DataWriter.Write32(stream, implementation!=null ? implementation.offsetToInstructions : offsetToInstructions);
            DataWriter.Write16(stream, 0);
            DataWriter.Write16(stream, 1);
            DataWriter.Write32(stream, implementation!=null ? implementation.locals.TotalBytes() : locals.TotalBytes());
        }

        public override void WriteBody(Stream stream, int offsetToInstructions)
        {
            if (implementation!=null)
            {
                return;
            }

            int numpar = ioDataTypes.Count;

            // before actual byte codes there come the IN,OUT,IO descriptors
            stream.WriteByte((byte)numpar);
            for (int i = 0; i < numpar; i++)
            {
                int ioflags = 0;
                switch (ioAccessTypes[i])
                {   
                    case AccessType.Read:
                    case AccessType.ReadMany:
                        ioflags = 0x80;
                        break;
                    case AccessType.Write:
                        ioflags = 0x40;
                        break;
                    case AccessType.ReadWrite:
                        ioflags = 0xC0;
                        break;
                }
                switch (ioDataTypes[i])
                {
                    case DataType.I8:
                        if (ioStringSizes[i] == 0)      // a single I8 will be transfered
                        {
                            stream.WriteByte((byte)(ioflags | 0x00));
                        }
                        else                            // transfer a string of I8s
                        {
                            stream.WriteByte((byte)(ioflags | 0x04));
                            stream.WriteByte((byte)ioStringSizes[i]);
                        }
                        break;
                    case DataType.I16:
                        stream.WriteByte((byte)(ioflags | 0x01));
                        break;
                    case DataType.I32:
                        stream.WriteByte((byte)(ioflags | 0x02));
                        break;
                    case DataType.F:
                        stream.WriteByte((byte)(ioflags | 0x03));
                        break;
                }
            }

            // write actual code parts (with correct termination)
            WriteByteCodes(stream, offsetToInstructions);
            stream.WriteByte(0x08);  // RETURN
            stream.WriteByte(0x0A);  // OBJECT_END

            // insert a check to test all occuring calls for compatibility with paramaters
            foreach (List<Object> l in callerMemorization)
            {
                if (l.Count != numpar)
                {
                    throw new AssemblerException("Detected use of CALL " + name + " with " + l.Count + " parameters instead of " + numpar);
                }
                for (int i = 0; i < numpar; i++)
                {
                    DataTypeChecker.check(l[i], ioDataTypes[i], ioAccessTypes[i]);
                }
            }        
        }


        public override void print()
        {
            base.print();

            for (int i = 0; i < ioDataTypes.Count; i++)
            {
                Console.WriteLine(ioDataTypes[i] + " " + ioAccessTypes[i] + "(" + ioStringSizes[i] + ")");
            }
        }

    }

}
