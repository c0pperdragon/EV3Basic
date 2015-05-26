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

namespace LMSAssembler
{
    public class Assembler
    {
        // data loaded once that is available for multiple assembler runs
        Dictionary<String, VMCommand> commands;

        // temporary data for one assembler run
        DataArea globals;
        Dictionary<String, LMSObject> objects;

        // data for current object
        LMSObject currentobject;

        public Assembler()
        {
            commands = new Dictionary<String, VMCommand>();
            readByteCodeList();
        }

        private void readByteCodeList()
        {
            StringReader reader = new StringReader(LMSAssembler.Properties.Resources.bytecodelist);

            String line;
            while ((line = reader.ReadLine()) != null)
            {
                int idx = line.IndexOf("//");
                if (idx>=0)
                {  line = line.Substring(0,idx);
                }
                line = line.Trim();
                if (line.Length>0)
                {
                    VMCommand c = new VMCommand(line);
                    commands.Add(c.name, c);
                }
            }
            reader.Close();
            
//            foreach (VMCommand c in commands.Values)
//            {
//                Console.WriteLine(c);
//            }
        }



        public void Assemble(Stream source, Stream target, List<String> errorList)
        {
            globals = new DataArea();
            objects = new Dictionary<String,LMSObject>();
            currentobject = null;

            // read and process the input line by line
            StreamReader reader = new StreamReader(source, System.Text.Encoding.ASCII);
            String l = reader.ReadLine();
            for (int linenumber=1; l!=null; l=reader.ReadLine(),linenumber++)
            {
                try {
                    ProcessLine(l);
                }
                catch (AssemblerException e)
                {
                   errorList.Add("Error at line " + linenumber+": " + e.Message);
                }
            }
            if (currentobject != null)
            {
                errorList.Add("Unexpected end of file");
            }

            reader.Close();

            try
            {
                GenerateOutput(target);
            }
            catch (AssemblerException e)
            {
                errorList.Add("Error at subcall integration: " + e.Message);
            }

//            globals.print();
//            Console.ReadKey();
        }

        private void ProcessLine(String l)
        { 
            List<String> tokens = TokenizeLine(l);
            if (tokens.Count < 1)
            {
                return;
            }

            String first = tokens[0];
//          Console.WriteLine(first);

            // currently on top level
            if (currentobject==null)
            {
                if (first.Equals("VMTHREAD"))
                {
                    String name = FetchID(tokens, 1, true);
                    if (objects.ContainsKey(name))
                    {
                        // could have found object that was created because of forward reference    
                        LMSObject lo = objects[name];
                        lo.StartCode();
                        currentobject = (LMSThread)lo;      // make sure that this object is indeed a thread
                    }                    
                    else
                    {   // this is the first encounter of the name - create a new thread
                        currentobject = new LMSThread(name, objects.Count + 1);
                        currentobject.StartCode();
                        objects[name] = currentobject;
                    }                            
                }
                else if (first.Equals("SUBCALL"))
                {
                    String name = FetchID(tokens, 1, true);
                    if (objects.ContainsKey(name))
                    {
                        // could have found the object that was created because of a forward reference
                        LMSObject lo = objects[name];
                        lo.StartCode();  
                        currentobject = (LMSSubCall) lo;      // make sure that this object is indeed a subcall
                    }
                    else
                    {
                        // this is the first encounter of the name - create a new subcall
                        LMSSubCall sc = new LMSSubCall(name, objects.Count + 1);
                        objects[name] = sc;
                        sc.StartCode();
                        currentobject = sc;
                    }
                }
                else if (first.Equals("DATA8"))
                {
                    globals.Add(FetchID(tokens,1,true), 1,1, DataType.I8,false);
                }
                else if (first.Equals("DATA16"))
                {
                    globals.Add(FetchID(tokens, 1, true), 2,1, DataType.I16, false);
                }
                else if (first.Equals("DATA32"))
               {
                   globals.Add(FetchID(tokens, 1, true), 4,1,DataType.I32, false);
                }
                else if (first.Equals("DATAF"))
                {
                    globals.Add(FetchID(tokens, 1, true), 4,1, DataType.F, false);
                }
                else if (first.Equals("DATAS") || first.Equals("ARRAY8") )
                {
                    globals.Add(FetchID(tokens, 1, false), 1, FetchNumber(tokens, 2, true), DataType.I8, false);
                }
                else if (first.Equals("ARRAY16"))
                {
                    globals.Add(FetchID(tokens, 1, false), 2, FetchNumber(tokens, 2, true), DataType.I16, false);
                }
                else if (first.Equals("ARRAY32"))
                {
                    globals.Add(FetchID(tokens, 1, false), 4, FetchNumber(tokens, 2, true), DataType.I32, false);
                }
                else if (first.Equals("ARRAYF"))
                {
                    globals.Add(FetchID(tokens, 1, false), 4, FetchNumber(tokens, 2, true), DataType.F, false);
                }
                else
                {
                    throw new AssemblerException("Unknown command: " + first);
                }
            }

            // currently inside a structure element
            else
            {
                DataArea locals = currentobject.locals;

                if (first.Equals("}"))
                {
//                    currentobject.print();
                    currentobject = null;
                    return;
                }

                if (first.Equals("SUBCALL"))      // trying to create a subcall with multiple object IDs, but with the same code
                {
                    if (!(currentobject is LMSSubCall))
                    {
                        throw new AssemblerException("Can not add subcall alias to non-subcall object");
                    }
                    String name = FetchID(tokens, 1, true);                    
                    if (objects.ContainsKey(name))
                    {
                        // could have found the object that was created because of a forward reference
                        LMSObject lo = objects[name];
                        ((LMSSubCall)lo).SetImplementation((LMSSubCall)currentobject);
                    }
                    else
                    {
                        // this is the first encounter of the name - create a new subcall
                        LMSSubCall sc = new LMSSubCall(name, objects.Count + 1);
                        objects[name] = sc;
                        sc.SetImplementation((LMSSubCall)currentobject);
                    }

                        
                }
                else if (first.Equals("DATA8"))
                {
                    locals.Add(FetchID(tokens, 1, true), 1, 1, DataType.I8, false);
                }
                else if (first.Equals("DATA16"))
                {
                    locals.Add(FetchID(tokens, 1, true), 2, 1, DataType.I16, false);
                }
                else if (first.Equals("DATA32"))
                {
                    locals.Add(FetchID(tokens, 1, true), 4, 1, DataType.I32, false);
                }
                else if (first.Equals("DATAF"))
                {
                    locals.Add(FetchID(tokens, 1, true), 4, 1, DataType.F, false);
                }
                else if (first.Equals("DATAS") || first.Equals("ARRAY8"))
                {
                    locals.Add(FetchID(tokens, 1, false), 1, FetchNumber(tokens, 2, true), DataType.I8, false);
                }
                else if (first.Equals("ARRAY16"))
                {
                    locals.Add(FetchID(tokens, 1, false), 2, FetchNumber(tokens, 2, true), DataType.I16, false);
                }
                else if (first.Equals("ARRAY32"))
                {
                    locals.Add(FetchID(tokens, 1, false), 4, FetchNumber(tokens, 2, true), DataType.I32, false);
                }
                else if (first.Equals("ARRAYF"))
                {
                    locals.Add(FetchID(tokens, 1, false), 4, FetchNumber(tokens, 2, true), DataType.F, false);
                }
                else if (first.Equals("IN_8"))
                {
                    locals.Add(FetchID(tokens, 1, true), 1, 1, DataType.I8, true);
                    currentobject.MemorizeIOParameter(DataType.I8, AccessType.Read);
                }
                else if (first.Equals("IN_16"))
                {
                    locals.Add(FetchID(tokens, 1, true), 2, 1, DataType.I16, true);
                    currentobject.MemorizeIOParameter(DataType.I16, AccessType.Read);
                }
                else if (first.Equals("IN_32"))
                {
                    locals.Add(FetchID(tokens, 1, true), 4, 1, DataType.I32, true);
                    currentobject.MemorizeIOParameter(DataType.I32, AccessType.Read);
                }
                else if (first.Equals("IN_F"))
                {
                    locals.Add(FetchID(tokens, 1, true), 4, 1, DataType.F, true);
                    currentobject.MemorizeIOParameter(DataType.F, AccessType.Read);
                }
                else if (first.Equals("IN_S"))
                {
                    int n = FetchNumber(tokens, 2, true);
                    locals.Add(FetchID(tokens, 1, false), 1, n, DataType.I8, true);
                    currentobject.MemorizeStringIOParameter(n, AccessType.ReadMany);
                }
                else if (first.Equals("OUT_8"))
                {
                    locals.Add(FetchID(tokens, 1, true), 1, 1, DataType.I8, true);
                    currentobject.MemorizeIOParameter(DataType.I8, AccessType.Write);
                }
                else if (first.Equals("OUT_16"))
                {
                    locals.Add(FetchID(tokens, 1, true), 2, 1, DataType.I16, true);
                    currentobject.MemorizeIOParameter(DataType.I16, AccessType.Write);
                }
                else if (first.Equals("OUT_32"))
                {
                    locals.Add(FetchID(tokens, 1, true), 4, 1, DataType.I32, true);
                    currentobject.MemorizeIOParameter(DataType.I32, AccessType.Write);
                }
                else if (first.Equals("OUT_F"))
                {
                    locals.Add(FetchID(tokens, 1, true), 4, 1, DataType.F, true);
                    currentobject.MemorizeIOParameter(DataType.F, AccessType.Write);
                }
                else if (first.Equals("OUT_S"))
                {
                    int n = FetchNumber(tokens, 2, true);
                    locals.Add(FetchID(tokens, 1, false), 1, n, DataType.I8, true);
                    currentobject.MemorizeStringIOParameter(n, AccessType.Write);
                }
                else if (first.Equals("IO_8"))
                {
                    locals.Add(FetchID(tokens, 1, true), 1, 1, DataType.I8, true);
                    currentobject.MemorizeIOParameter(DataType.I8, AccessType.ReadWrite);
                }
                else if (first.Equals("IO_16"))
                {
                    locals.Add(FetchID(tokens, 1, true), 2, 1, DataType.I16, true);
                    currentobject.MemorizeIOParameter(DataType.I16, AccessType.ReadWrite);
                }
                else if (first.Equals("IO_32"))
                {
                    locals.Add(FetchID(tokens, 1, true), 4, 1, DataType.I32, true);
                    currentobject.MemorizeIOParameter(DataType.I32, AccessType.ReadWrite);
                }
                else if (first.Equals("IO_F"))
                {
                    locals.Add(FetchID(tokens, 1, true), 4, 1, DataType.F, true);
                    currentobject.MemorizeIOParameter(DataType.F, AccessType.ReadWrite);
                }
                else if (first.Equals("IO_S"))
                {
                    int n = FetchNumber(tokens, 2, true);
                    locals.Add(FetchID(tokens, 1, false), 1, n, DataType.I8, true);
                    currentobject.MemorizeStringIOParameter(n, AccessType.ReadWrite);
                }

                // calling a subcall object (maybe without having the declaration yet)
                else if (first.Equals("CALL"))
                {
                    String name = FetchID(tokens, 1, false);
                    LMSSubCall sc = null;

                    // the name is already known
                    if (objects.ContainsKey(name))
                    {
                        LMSObject o = objects[name];
                        if (!(o is LMSSubCall))
                        {
                            throw new AssemblerException("Trying to call an object that is not defined as a SUBCALL");
                        }
                        sc = (LMSSubCall)o;
                    }
                    // name is not known - create an empty subcall object right here
                    else
                    {
                        sc = new LMSSubCall(name, objects.Count + 1);
                        objects[name] = sc;
                    }

                    // generate the opcode and the parameters and memorize the types for later check
                    sc.StartCallerMemorization();

                    int numpar = tokens.Count - 2;
                    currentobject.AddOpCode(new byte[] { 0x09 });  // CALL
                    currentobject.AddConstant(sc.id);           // ID of called subcall
                    currentobject.AddConstant(numpar);
                    for (int i = 0; i < numpar; i++)
                    {
                        Object p = DecodeAndAddParameter(locals, tokens[2 + i]);
                        sc.MemorizeCallerParameter(p);  // keep for later type check
                    }

                }
                // process a label declaration
                else if (first.EndsWith(":"))
                {
                    if (first.IndexOf(':') < first.Length - 1)
                    {
                        throw new AssemblerException("Label must only have one trailing ':'");
                    }
                    currentobject.MemorizeLabel(first.Substring(0, first.Length - 1));
                }
                else
                {
                    // process regular VMCommands 
                    VMCommand c;
                    int paramstart = 0;
                    if (commands.ContainsKey(tokens[0]))
                    {
                        c = commands[tokens[0]];
                        paramstart = 1;
                    }
                    else if (tokens.Count < 2)
                    {
                        throw new AssemblerException("Unknown opcode " + tokens[0]);
                    }
                    else
                    {
                        String compound = tokens[0] + " " + tokens[1];
                        if (commands.ContainsKey(compound))
                        {
                            c = commands[compound];
                            paramstart = 2;
                        }
                        else
                        {
                            throw new AssemblerException("Unknown opcode " + compound);
                        }
                    }

                    int paramcount = c.parameters.Length;  // this is the default parameter number (can be modifed by some special opcodes)

                    // create opcode and parameters
                    currentobject.AddOpCode(c.opcode);

                    for (int i = 0; i < paramcount; i++)
                    {
                        if (paramstart + i >= tokens.Count)
                        {
                            throw new AssemblerException("Too few parameters for " + c.name);
                        }

                        // of more paramerters then specified, repeat the last type (can only happen for opcodes with variable parameter number)
                        int pidx = Math.Min(i, c.parameters.Length - 1);

                        if (c.parameters[pidx] == DataType.Label)   // special handling for jump label parameters
                        {
                            currentobject.AddLabelReference(tokens[paramstart + i]);
                        }
                        else if (c.parameters[pidx] == DataType.VMThread)   // special handling for VM thread identifier
                        {
                            String name = tokens[paramstart + i];
                            LMSThread th = null;
                            // the name is already known
                            if (objects.ContainsKey(name))
                            {
                                LMSObject o = objects[name];
                                if (!(o is LMSThread))
                                {
                                    throw new AssemblerException("Trying to start an object that is not defined as a thread");
                                }
                                th = (LMSThread)o;
                            }
                            // name is not known - create an empty thread object right here
                            else
                            {
                                th = new LMSThread(name, objects.Count + 1);
                                objects[name] = th;
                            }
                            currentobject.AddConstant(th.id);
                        }
                        else if (c.parameters[pidx] == DataType.VMSubcall)   // special handling for VM subcall identifier
                        {
                            String name = tokens[paramstart + i];
                            if (name.Equals("0"))
                            {
                                // no subcall id - want to access something global
                                currentobject.AddConstant(0);
                            }
                            else
                            {
                                LMSSubCall th = null;
                                // the name is already known
                                if (objects.ContainsKey(name))
                                {
                                    LMSObject o = objects[name];
                                    if (!(o is LMSSubCall))
                                    {
                                        throw new AssemblerException("Trying to access an object that is not defined as a subcall");
                                    }
                                    th = (LMSSubCall)o;
                                }
                                // name is not known - create an empty thread object right here
                                else
                                {
                                    th = new LMSSubCall(name, objects.Count + 1);
                                    objects[name] = th;
                                }
                                currentobject.AddConstant(th.id);
                            }
                        }
                        else if (c.parameters[pidx] == DataType.ParameterCount)   // special handling for opcodes with variable parameter number
                        {
                            Int32 p;
                            if (!Int32.TryParse(tokens[paramstart + i], NumberStyles.Integer, CultureInfo.InvariantCulture, out p))
                            {
                                throw new AssemblerException("Can not decode parameter count specifier");
                            }
                            if (p < 0 || p>1000000000)
                            {
                                throw new AssemblerException("Parameter count specifier out of range");
                            }
                            currentobject.AddConstant(p);
                            paramcount = c.parameters.Length - 1 + p;  // calculate number of parameters needed because of given specifier
                        }
                        else
                        {                          // normal parameters (numbers, strings, variables)
                            Object a = DecodeAndAddParameter(locals, tokens[paramstart + i]);
                            DataTypeChecker.check(a, c.parameters[pidx], c.access[pidx]);
                        }
                    }

                    // check final number of parameters that were generated
                    if (paramstart + paramcount != tokens.Count)
                    {
                        throw new AssemblerException("Invalid number of parameters for " + c.name);
                    }
                }
            }
        }

        // decodes a parameter and adds it to bytecode stream. 
        // return either DataElement, int, double, or String  (for type checking)         
        private Object DecodeAndAddParameter(DataArea locals, String p)
        { 
            // this must be a label address difference
            if ((!p.StartsWith("'")) && p.Contains(':'))
            {
                currentobject.AddLabelDifference(p);
                return 999999999;  // only compatible with 32bit numbers
            }
            // this must be a variable
            else if ( (p[0]>='A' && p[0]<='Z') || p[0]=='_')
            {
                    // check if have offset suffix
                    int offset = 0;
                    int plusidx = p.IndexOf('+');
                    if (plusidx>=0)
                    {
                        if (Int32.TryParse(p.Substring(plusidx+1), NumberStyles.Integer, CultureInfo.InvariantCulture, out offset))
                        {
                            p = p.Substring(0, plusidx);
                        }
                    }
                    
                    DataElement e = null;
                    bool local = true;
                    if (locals != null)
                    {
                        e = locals.Get(p);
                    }
                    if (e == null)
                    {
                        e = globals.Get(p);
                        local = false;
                    }
                    if (e == null)
                    {
                        throw new AssemblerException("Unknown identifier " + p);
                    }

                    currentobject.AddVariableReference(e.position+offset, local);
                    return e;
                
            }
            // this must be a constant numeric value
            else if ( (p[0]>='0' && p[0]<='9') || p[0]=='-')
            {
                Int32 c;
                double d;
                if (Int32.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out c))
                {
                    currentobject.AddConstant(c);
                    return c;
                }
                if (double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                {
                    currentobject.AddFloatConstant(d);
                    return d;
                }
                throw new AssemblerException("Can not decode number: "+p);
            }
            // this must be a string literal
            else if (p[0]=='\'')
            {
                String l = p.Substring(1, p.Length - 2);
                currentobject.AddStringLiteral(l);
                return l;
            }
            else
            {
                throw new AssemblerException("Invalid parameter value");
            }        
        }


        private void GenerateOutput(Stream target)
        {
            int numobjects = objects.Count;
            LMSObject[] oarray = new LMSObject[numobjects];
            
            // get all objects in order of their ID
            foreach (LMSObject o in objects.Values)
            {
                oarray[o.id - 1] = o;
            }

            // create the byte codes in a temporary buffer and memorize positions
            MemoryStream allbytecodes = new MemoryStream();
            int totalheadersize = 16 + numobjects * 12;

            for (int i = 0; i < numobjects; i++)
            {
                oarray[i].WriteBody(allbytecodes, totalheadersize + (int)allbytecodes.Length);   
            }

            // write file header
            target.WriteByte((byte)'L');
            target.WriteByte((byte)'E');
            target.WriteByte((byte)'G');
            target.WriteByte((byte)'O');
            DataWriter.Write32(target, totalheadersize + (int)allbytecodes.Length);
            DataWriter.Write16(target,0x0068);
            DataWriter.Write16(target, numobjects);
            DataWriter.Write32(target,globals.TotalBytes());
            // write object headers
            for (int i = 0; i < numobjects; i++)
            {
                oarray[i].WriteHeader(target);
//                oarray[i].print();
//                Console.ReadKey();
            }
            // write all object bodies
            allbytecodes.WriteTo(target);
        }



        private static List<String> TokenizeLine(String l)
        {
            List<String> tokens = new List<String>();

            int pos=0;
            while(pos<l.Length)
            {
                char c = l[pos];

                if (c=='/')           // rest of line is comment
                {
                    break;
                }
                else if (c==' ' || c=='\t' || c==',' || c=='(' || c==')' || c=='{')   // skip whitespaces and other optional structural elements
                {
                    pos++;
                }
                else if (c=='\'')     // begin of a string with ' delimiters
                {   
                    int start = pos;
                    pos++;
                    for (; ;)
                    {
                        if (pos>=l.Length)
                        {
                            throw new AssemblerException("Nonterminated string");
                        }
                        if (l[pos]=='\'')
                        {   pos++;
                            tokens.Add(Unescape(l.Substring(start, pos-start)));
                            break;
                        }
                        pos++;
                    }
                } 
                else if ( (c>='a' && c<='z')        // begin of a normal token (ID or number)
                      ||  (c>='A' && c<='Z')
                      ||  (c>='0' && c<='9')
                      ||  (c=='_') || (c=='-')  )
                {
                    int start = pos;
                    pos++;
                    for (; ;)
                    {
                        if (pos>=l.Length)
                        {   tokens.Add(l.Substring(start).ToUpperInvariant());
                            break;
                        }
                        c = l[pos];
                        if ( (c>='a' && c<='z')          
                         ||  (c>='A' && c<='Z')
                         ||  (c>='0' && c<='9')
                         ||  (c=='_') || c=='.' || c==':' || c=='+')
                        {
                            pos++;    // still inside token
                        }
                        else
                        {                     
                            tokens.Add(l.Substring(start, pos-start).ToUpperInvariant());
                            break;
                        }
                    }
                }
                else if (c=='}')
                {
                    pos++;
                    tokens.Add("}");
                }
                else
                {
                    throw new AssemblerException("Unknown letter '" + c + "' ");
                }
            }
            
            return tokens;
        }

        private static String FetchID(List<String> tokens, int position, bool mustBeLast)
        {
            if (tokens.Count<=position)
            {
                throw new AssemblerException("Identifier expected");
            }
            if (mustBeLast && position+1<tokens.Count)
            {
                throw new AssemblerException("Too many elements for command");
            }
            String token = tokens[position];
            if ((token[0] >= 'A' && token[0] <= 'Z') || token[0] == '_')
            {
                return token;
            }
            throw new AssemblerException("Identifer expected instead of " + token);
        }

        private static int FetchNumber(List<String> tokens, int position, bool mustBeLast)
        {
            if (tokens.Count <= position)
            {
                throw new AssemblerException("Number expected");
            }
            if (mustBeLast && position + 1 < tokens.Count)
            {
                throw new AssemblerException("Too many elements for command");
            }
            long l;
            if (Int64.TryParse(tokens[position], NumberStyles.Integer, CultureInfo.InvariantCulture, out l))
            {
                if (l<1 || l>Int16.MaxValue)
                {
                    throw new AssemblerException("Number ouf of range");
                }

                return (int)l;
            }
            throw new AssemblerException("Number expected");
        }


        private static String Unescape(String s)
        {
            for (int i=0; i<s.Length; i++)
            {
                if (s[i]=='\\')
                {
                    if (s.Length>i+1 && s[i+1]=='n')
                    {
                        s = s.Substring(0, i) + "\n" + s.Substring(i + 2);
                    }
                    else if (s.Length>i+1 && s[i+1]=='t')
                    {
                        s = s.Substring(0, i) + "\t" + s.Substring(i + 2);
                    }
                    else if (s.Length>i+3 && s[i+1]>='0' && s[i+1]<='3')
                    {
                        s = s.Substring(0, i) + ((char)DecodeOctal(s.Substring(i + 1, 3))) + s.Substring(i + 4);
                    }
                }
            }
            return s;
        }
        private static int DecodeOctal(String s)
        {
            int b2 = s[0] - '0';
            int b1 = s[1] - '0';
            int b0 = s[2] - '0';
            return b2 * 8 * 8 + b1 * 8 + b0;
        }


        // ---------------------------------- DISASSEMLER ----------------------------------------------

        public void Disassemble(Stream binary, TextWriter writer)
        {
            Dictionary<int, String> vmobjects = new Dictionary<int,String>();
            int didread = 0;

            // read file header and object definitions
            int magic = DataReader.Read32(binary, ref didread);

            if (magic != 0x4F47454C)
            {
                throw new Exception("Missing LEGO header");
            }
            int imgsize = DataReader.Read32(binary, ref didread);
            int version = DataReader.Read16(binary, ref didread);
            int numobjects = DataReader.Read16(binary, ref didread);
            int globalbytes = DataReader.Read32(binary, ref didread);
            writer.WriteLine("Image size: " + imgsize);
            writer.WriteLine("Version: " + version);
            writer.WriteLine("Objects: " + numobjects);
            writer.WriteLine("Globals bytes: " + globalbytes);

            for (int i=0; i<numobjects; i++)
            {
                int offset = DataReader.Read32(binary, ref didread);
                int owner = DataReader.Read16(binary, ref didread);
                int triggercount = DataReader.Read16(binary, ref didread);
                int localbytes = DataReader.Read32(binary, ref didread);
                if (owner==0)
                {
                    if (triggercount == 0)
                    {
                        vmobjects[offset] = "VMTHREAD THREAD"+(i+1)+" ( "+localbytes+" locals)";                    
                    }
                    else if (triggercount == 1)
                    {
                        vmobjects[offset] = "SUBCALL SUB" + (i + 1) + " ( " + localbytes + " locals)";
                    }
                    else
                    {
                        throw new Exception("Encountered invalid triggercount value");
                    }
                }
                else
                {
                    if (localbytes!=0)
                    {
                        throw new Exception("Can not have local bytes for object with owner");
                    }
                    vmobjects[offset] = "OBJECT "+(i+1)+" (+trigercount="+triggercount+", owner="+owner+")";
                }
            }

            // read all objects
            while (didread < imgsize)
            {
                if (!vmobjects.ContainsKey(didread))
                {
                    throw new Exception("No object starts at position " + didread);
                }
                String o = vmobjects[didread];
                vmobjects.Remove(didread);
                writer.WriteLine(o);

                // decode parameter specifier for SUBCALL
                if (o.StartsWith("SUBCALL"))
                {
                    int localpos = 0;
                    int numpar = DataReader.Read8(binary, ref didread) & 0xff;
                    for (int i=0; i<numpar; i++)
                    {
                        String prefix = "";
                        int desc = DataReader.Read8(binary, ref didread) & 0xff;
                        if ((desc&0x80) !=0)
                        {
                            prefix = "IN";
                        }
                        if ((desc & 0x40) != 0)
                        {
                            prefix = prefix + "OUT";
                        }
                        switch (desc & 0x07)
                        {
                            case 0: 
                                writer.WriteLine(format(localpos,4) + " " +prefix+"_8");
                                localpos += 1;
                                break;
                            case 1: 
                                writer.WriteLine(format(localpos,4) + " " +prefix + "_16");
                                localpos += 2;
                                break;
                            case 2: 
                                writer.WriteLine(format(localpos,4) + " " + prefix + "_32");
                                localpos += 4;
                                break;
                            case 3: 
                                writer.WriteLine(format(localpos,4) + " " + prefix + "_F");
                                localpos += 4;
                                break;
                            case 4: 
                                int len = DataReader.Read8(binary, ref didread) & 0xff;
                                writer.WriteLine(format(localpos,4) + " " + prefix + "_S " + len);
                                localpos += len;
                                break;
                            default:
                                throw new Exception("Can not decode subcall parameter list");
                        }
                    }
                }

                int objectstart = didread;
                for (; ; )
                {
                    writer.Write(format(didread - objectstart,4)+ "  ");

                    // decode bytecodes
                    VMCommand cmd = ReadOpCode(binary, ref didread);
                    if (cmd == null)
                    {   int n;
                        String objid = ReadParameter(binary,ref didread);
                        if (!int.TryParse(ReadParameter(binary,ref didread),NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                        {
                            throw new Exception ("Invalid specifier for number of CALL parameters");
                        }
                        // special handling for CALL                         
                        writer.Write("CALL SUB"+objid);
                        for (int i=0; i<n; i++)
                        {   writer.Write(" " + ReadParameter(binary,ref didread));
                        }
                        writer.WriteLine();
                    }
                    else
                    {   // normal opcodes
                        writer.Write(cmd.name);

                        // decode parameters
                        int numparameters = cmd.parameters.Length;
                        for (int i = 0; i < numparameters; i++)
                        {
                            String p = ReadParameter(binary, ref didread);
                            writer.Write(" " + p);

                            // check if this parameter is a counter to extend the parameter list
                            if (i<cmd.parameters.Length && cmd.parameters[i]==DataType.ParameterCount)
                            {
                                int n;
                                if (int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                                {
                                    numparameters += (n- 1);
                                }
                            }
                        }
                        writer.WriteLine();

                        if (cmd.name.Equals("OBJECT_END"))
                        {
                            break;
                        }
                    }
                }
            }                
		  
        }

        VMCommand ReadOpCode(Stream binary, ref int didread)
        {
            int op = DataReader.Read8(binary, ref didread) & 0xff;   // get unsigned
            if (op==0x09)   // opcode for CALL
            {
                return null;  
            }

            // check if there is  a single-byte opcode
            foreach (var de in commands)
            {
                VMCommand cmd = de.Value;
                if (cmd.opcode.Length==1 && cmd.opcode[0]==op)
                {
                    return cmd;
                }
            }
            // extend search to get two-byte opcodes as well
            int op1 = DataReader.Read8(binary, ref didread) & 0xff;
            foreach (var de in commands)
            {
                VMCommand cmd = de.Value;
                if (cmd.opcode.Length == 2 && cmd.opcode[0] == op && cmd.opcode[1]==op1)
                {
                    return cmd;
                }
            }
            throw new Exception("Unrecognized opcode: " + op + "("+op1+")");
        }

        String ReadParameter(Stream binary, ref int didread)
        {
            int x = DataReader.Read8(binary, ref didread) & 0xff;

            if ((x&0x80) == 0)
            {   // short format
                if ((x&0x40) == 0)
                {   // constant
                    if ((x&0x20)==0)
                    {   // positive constant
                        return "" + (x&0x1f);
                    }
                    else
                    {   // negative constant
                        return "" + (-32 + (x&0x1f));
                    }
                }  
                else
                {   // variable
                    if ((x&0x20)==0)
                    {   // local index
                        return "LOCAL" + (x & 0x1f);
                    }
                    else
                    {   // global index
                        return "GLOBAL" + (x & 0x1f);
                    }
                }
            }
            else
            {   // long format
                if ((x&0x40) == 0)
                {   // constant
                    if ((x&0x20) == 0)
                    {   // value
                        if ((x&0x07)==0 || (x&0x07)==4)
                        {   // zero-terminated string
                            String s = "";
                            for (; ; )
                            {
                                int c = DataReader.Read8(binary, ref didread) & 0xff;
                                if (c == 0)
                                {
                                    return "'" + s + "'";
                                }
                                s = s + (char)c;
                            }
                        }
                        else
                        {   // other constant numeral
                            return "" + ReadExtraBytes(binary, ref didread, x);
                        }
                    }
                    else
                    {   // label
                        return "offset" + ReadExtraBytes(binary, ref didread, x);
                    }
                }
                else
                {   // variable
                    if ((x & 0x20) == 0)
                    {   // local
                        if ((x & 0x10) == 0)
                        {   // value
                            return "LOCAL" + ReadExtraBytes(binary, ref didread, x);
                        }
                        else
                        {   // handle
                            return "@LOCAL" + ReadExtraBytes(binary, ref didread, x);
                        }
                    }
                    else
                    {   // global
                        if ((x & 0x10) == 0)
                        {   // value
                            return "GLOBAL" + ReadExtraBytes(binary, ref didread, x);
                        }
                        else
                        {   // handle
                            return "@GLOBAL" + ReadExtraBytes(binary, ref didread, x);
                        }
                    }
                }
            }
        }

        int ReadExtraBytes(Stream binary, ref int didread, int firstbyte)
        {
            switch (firstbyte & 0x07)
            {
                case 1:
                    return DataReader.Read8(binary,ref didread);
                case 2:
                    return DataReader.Read16(binary,ref didread);
                case 3:
                    return DataReader.Read32(binary,ref didread);
                default:
                    throw new Exception("Can not decode parameter " + firstbyte);            
            }
        }

        static String format(int number, int places)
        {
            String s = "" + number;
            while (s.Length<places)
            {
                s = " " + s;
            }
            return s;
        }
    }
}
