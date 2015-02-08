using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

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
                        throw new AssemblerException("Duplicate object name: "+name);
                    }
                    currentobject = new LMSThread(name, objects.Count + 1);
                    currentobject.StartCode();
                    objects[name] = currentobject;
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
                    locals.Add(FetchID(tokens, 1, true), 1,1, DataType.I8, true);
                    currentobject.MemorizeIOParameter(DataType.I8, AccessType.Read);
                }
                else if (first.Equals("IN_16"))
                {
                    locals.Add(FetchID(tokens, 1, true), 2,1, DataType.I16, true);
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
                        sc = new LMSSubCall(name, objects.Count+1);                        
                        objects[name] = sc;
                    }

                    // generate the opcode and the parameters and memorize the types for later check
                    sc.StartCallerMemorization();

                    int numpar = tokens.Count - 2;
                    currentobject.AddOpCode(new byte[]{0x09});  // CALL
                    currentobject.AddConstant(sc.id);           // ID of called subcall
                    currentobject.AddConstant(numpar);
                    for (int i = 0; i < numpar; i++)
                    {
                        Object p = DecodeAndAddParameter(locals, tokens[2+i]);
                        sc.MemorizeCallerParameter(p);  // keep for later type check
                    }

                }
                // process a label declaration
                else if (first.EndsWith(":"))                    
                {
                    if (first.IndexOf(':') < first.Length-1)
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
                    else if (tokens.Count<2)
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

                    // check number of parameters
                    if (tokens.Count-paramstart != c.parameters.Length)
                    {
                        throw new AssemblerException("Invalid number of parameters for " + c.name);
                    }

                    // create opcode and parameters
                    currentobject.AddOpCode(c.opcode);

                    for (int i=0; i<c.parameters.Length; i++)
                    {
                        if (c.parameters[i] == DataType.Label)   // special handling for jump label parameters
                        {
                            currentobject.AddLabelReference(tokens[paramstart + i]);
                        }
                        else
                        {                          // normal parameters (numbers, strings, variables)
                            Object a = DecodeAndAddParameter(locals, tokens[paramstart + i]);
                            DataTypeChecker.check(a, c.parameters[i], c.access[i]);
                        }
                    }
                }
            }
        }

        // decodes a parameter and adds it to bytecode stream. 
        // return either DataElement, int or String  (for type checking)         
        private Object DecodeAndAddParameter(DataArea locals, String p)
        { 
            // this must be a variable
            if ( (p[0]>='A' && p[0]<='Z') || p[0]=='_')
            {
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

                    currentobject.AddVariableReference(e.position, local);
                    return e;
                
            }
            // this must be a constant numeric value
            else if ( (p[0]>='0' && p[0]<='9') || p[0]=='-')
            {
                Int32 c;
                if (!Int32.TryParse(p, out c))
                {
                    throw new AssemblerException("Can not decode number");
                }
                currentobject.AddConstant(c);
                return c;
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

            int[] objectstarts = new int[numobjects];
            for (int i = 0; i < numobjects; i++)
            {
                objectstarts[i] = (int) allbytecodes.Length;
                oarray[i].WriteBody(allbytecodes);   
            }

            int totalheadersize = 16 + numobjects * 12;
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
                oarray[i].WriteHeader(target, totalheadersize+objectstarts[i]);
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
                         ||  (c=='_') || c=='.' || c==':')
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
            if (Int64.TryParse(tokens[position],out l))
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
    }
}
