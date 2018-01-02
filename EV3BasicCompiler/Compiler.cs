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
using System.Collections;
using System.Globalization;


// Identifier types in assembler output
//  Vxxx     variable name (xxxx is from basic source)
//  Lxxx:    custom jump label (xxxx is from basic source)
//  Fnn      nth temporary float storage
//  Snn      nth temporary string storage
//  Ann      nth temporary float array handle
//  Xnn      nth temporary string array handle

namespace EV3BasicCompiler
{
    public class Compiler
    {
        // data loaded once at creation of Compiler (can be used to compile multiple programs)
        Hashtable library;
        String runtimeglobals;
        String runtimeinit;

        // data used during one compilation run
        Scanner s;
        int labelcount;
        Dictionary<String,ExpressionType> variables; 
        Dictionary<ExpressionType, int> reservedtemporaries;
        Dictionary<ExpressionType, int> maxreservedtemporaries;
        HashSet<LibraryEntry> references;
        List<String> threadnames;

        bool noboundscheck;
        bool nodivisioncheck;

        public Compiler()
        {
            library = new Hashtable();
            runtimeglobals = "";
            runtimeinit = "";
            variables = new Dictionary<String, ExpressionType>();
            references = new HashSet<LibraryEntry>();
            reservedtemporaries = new Dictionary<ExpressionType, int>();
            maxreservedtemporaries = new Dictionary<ExpressionType, int>();
            threadnames = new List<String>();

            readLibrary();
        }

        private void readLibrary()
        {
            readLibraryModule(EV3BasicCompiler.Properties.Resources.runtimelibrary);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Assert);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Buttons);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Byte);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.EV3);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.EV3File);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.LCD);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Mailbox);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Math);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Motor);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Program);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Sensor);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Speaker);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Text);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Thread);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Vector);
        }

        private void readLibraryModule(String moduletext)
        {
            StringReader reader = new StringReader(moduletext);
            
            String currentfirstline = null;
            StringBuilder body = new StringBuilder();

            String line;
            while ((line = reader.ReadLine()) != null)
            {
                if (currentfirstline == null)
                {
                    if (line.IndexOf("subcall")==0 || line.IndexOf("inline")==0 || line.IndexOf("init")==0)
                    {
                        currentfirstline = line;
                        body.Length = 0;
                        body.AppendLine(line);
                    }
                    else
                    {
                        int commentidx = line.IndexOf("//");
                        if (commentidx >= 0)
                        {
                            line = line.Substring(0, commentidx);
                        }
                        if (line.Trim().Length > 0)
                        {
                            runtimeglobals = runtimeglobals + line + "\n";
                        }
                    }
                }
                else
                {
                    body.AppendLine(line);
                    if (line.IndexOf("}") == 0)
                    {
                        if (currentfirstline.StartsWith("init"))
                        {
                            runtimeinit = runtimeinit + "    " + 
                                (body.ToString().Substring(currentfirstline.Length).Replace('{',' ').Replace('}',' ').Trim())
                                +"\n";
                        }
                        else
                        {
                            bool inline = currentfirstline.StartsWith("inline");
                            int idx1 = inline ? 6 : 7;
                            int idx2 = currentfirstline.IndexOf("//", idx1);
                            String functionname = currentfirstline.Substring(idx1, idx2 - idx1).Trim();
                            String[] descriptorandreferences = currentfirstline.Substring(idx2 + 2).Trim().Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                            LibraryEntry le = new LibraryEntry(inline, descriptorandreferences, body.ToString());
                            library[functionname.ToUpperInvariant()] = le;
                        }
                        currentfirstline = null;
                    }
                }
            }

            reader.Close();
        }

        public void Compile(Stream source, Stream targetstream, List<String> errorlist)
        {
            // start reading input file in tokenized form
            s = new Scanner(source); 
            s.GetSym();

            // initialize sybmol tables and other dynamic stuff
            this.labelcount = 0;
            this.variables.Clear();
            this.references.Clear();
            this.reservedtemporaries.Clear();
            this.maxreservedtemporaries.Clear();
            this.threadnames.Clear();
            this.noboundscheck = false;
            this.nodivisioncheck = false;

            // prepare buffers to keep parts of the output
            StringWriter mainprogram = new StringWriter();
            StringWriter subroutines = new StringWriter();

            // -- parse until end
            try
            {
                while (s.NextType != SymType.EOF)
                {
                    if (s.NextIsKEYWORD("SUB"))
                    {
                        compile_sub(subroutines);
                    }
                    else
                    {
                        compile_statement(mainprogram);
                    }
                }
            }
            catch (Exception e)
            {
                errorlist.Add(e.Message);
                return;
            }

            // -- when all information is available, put parts together
            StreamWriter target = new StreamWriter(targetstream);
            target.Write(runtimeglobals);

            // storage and initializer for global variables
            StringWriter initlist = new StringWriter();
            foreach (String vname in variables.Keys)
            {
                switch (variables[vname])
                {
                    case ExpressionType.Number:
                        target.WriteLine("DATAF " + vname);
                        initlist.WriteLine("    MOVEF_F 0.0 " + vname);
                        break;
                    case ExpressionType.Text:
                        target.WriteLine("DATAS " + vname + " 252");
                        initlist.WriteLine("    STRINGS DUPLICATE '' " + vname);
                        break;
                    case ExpressionType.NumberArray:
                        target.WriteLine("ARRAY16 " + vname + " 2");  // waste 16 bit, but keep alignment
                        initlist.WriteLine("    CALL ARRAYCREATE_FLOAT " + vname);
                        memorize_reference("ARRAYCREATE_FLOAT");
                        break;
                    case ExpressionType.TextArray:
                        target.WriteLine("ARRAY16 " + vname + " 2");  // waste 16 bit, but keep alignment
                        initlist.WriteLine("    CALL ARRAYCREATE_STRING " + vname);
                        memorize_reference("ARRAYCREATE_STRING");
                        break;
                }
            }
            // storage for run counters
            foreach (String n in threadnames)
            {
                target.WriteLine("DATA32 RUNCOUNTER_" + n);
                initlist.WriteLine("    MOVE32_32 0 RUNCOUNTER_" + n);
            }

            target.WriteLine();

            // -- create the main thread (this corresponds to the basic main program execution thread)
            target.WriteLine("vmthread MAIN");
            target.WriteLine("{");
            // initialize global variables
            target.Write(runtimeinit);
            target.Write(initlist.ToString());
            target.WriteLine("    ARRAY CREATE8 1 LOCKS");
            // copy native code to brick if needed
            if (references.Contains((LibraryEntry)library["EV3.NATIVECODE"]))
            {
                target.Write(CreateNativeCodeDownload());
            }
            // launch main program
            target.WriteLine("    CALL PROGRAM_MAIN -1");
            target.WriteLine("    PROGRAM_STOP -1");
            target.WriteLine("}");

            // -- create non-main threads (are started somewhere with a Thread.Run=... statement)
            for (int i=0; i<threadnames.Count; i++)
            {
                String n = threadnames[i];
                target.WriteLine("vmthread " + "T"+threadnames[i]);
                target.WriteLine("{");
                // launch program with proper subprogram selector and correct local data area
                target.WriteLine("    DATA32 tmp");
                target.WriteLine("  launch:");
                target.WriteLine("    CALL PROGRAM_"+n+" "+i);                
                target.WriteLine("    CALL GETANDINC32 RUNCOUNTER_" + n + " -1 RUNCOUNTER_" + n + " tmp");
                            // after this position in the code, the flag could be 0 and another thread could 
                            // newly trigger this thread. this causes no problems, because this thread was 
                            // in process of terminating anyway and would not have called the worker method
                            // again. if it is now instead re-activated, it will immediately start at the
                            // begining.
                target.WriteLine("    JR_GT32 tmp 1 launch");
                target.WriteLine("}");
                memorize_reference("GETANDINC32");
            }


            // create the code for the basic program (will be called from various threads)
            // multiple VM subcall objects will be created that share the same implementation, but
            // have a seperate local storage
            target.WriteLine("subcall PROGRAM_MAIN");
            foreach (String n in threadnames)
            {
                target.WriteLine("subcall PROGRAM_"+n);
            }
            target.WriteLine("{");
            // the call parameter that decides, which subroutine to start
            target.WriteLine("    IN_32 SUBPROGRAM");
            // storage for variables for compiler use
            target.WriteLine("    DATA32 INDEX");
            target.WriteLine("    ARRAY8 STACKPOINTER 4");  // waste 4 bytes, but keep alignment
            // storage for temporary float variables
            for (int i = 0; maxreservedtemporaries.ContainsKey(ExpressionType.Number) && i < maxreservedtemporaries[ExpressionType.Number]; i++)
            {
                target.WriteLine("    DATAF F" + i);
            }
            // storage for the return stack
            target.WriteLine("    ARRAY32 RETURNSTACK2 128");   // addressing the stack is done with an 8bit int.
            target.WriteLine("    ARRAY32 RETURNSTACK 128");    // when it wrapps over from 127 to -128, the second 
            target.WriteLine();                                 // part of the stack will be used (256 entries total)
            // storage for temporary string variables
            for (int i = 0; maxreservedtemporaries.ContainsKey(ExpressionType.Text) && i < maxreservedtemporaries[ExpressionType.Text]; i++)
            {
                target.WriteLine("    DATAS S" + i + " 252");
            }

            // initialize the stack pointer
            target.WriteLine("    MOVE8_8 0 STACKPOINTER");

            // add dispatch table to call proper sub-program first, if this is requested
            for (int i=0; i<threadnames.Count; i++)
            {
                int l = GetLabelNumber();
                String n = threadnames[i];
                target.WriteLine("    JR_NEQ32 SUBPROGRAM "+i+" dispatch"+l);
                // put initial return address on stack

                target.WriteLine("    WRITE32 ENDSUB_"+n+":ENDTHREAD STACKPOINTER RETURNSTACK");
                target.WriteLine("    ADD8 STACKPOINTER 1 STACKPOINTER");
                target.WriteLine("    JR SUB_" + n);
                target.WriteLine("  dispatch"+l+":");
            }

            // create the code for the main program 
            target.Write(mainprogram.ToString());
            target.WriteLine("ENDTHREAD:");
            target.WriteLine("    RETURN");
            // create code for all sub programs
            target.Write(subroutines);
            target.WriteLine("}");


            // create all library functions needed
            foreach (LibraryEntry le in references)
            {
                if (!le.inline)
                {
                    target.Write(le.programCode);
                }
            }

            target.Flush();
        }

        // create some program code that installs the native code to the /tmp - folder of the brick
        public String CreateNativeCodeDownload()
        {
            StringBuilder b = new StringBuilder();
            byte[] c = ToByteArray(EV3BasicCompiler.Properties.Resources.NativeCode);

            b.AppendLine("    DATA16 nativefd");
            b.AppendLine("    DATA32 padding");
            b.AppendLine("    ARRAY8 errorcode 4");
            b.AppendLine("    ARRAY8 nativecode " + c.Length);
            b.Append("    INIT_BYTES nativecode " + c.Length);
            for (int i = 0; i < c.Length; i++)
            {
                if (c[i] <= 127)
                {
                    b.Append(" " + c[i]);
                }
                else
                {
                    b.Append(" -" + (256-c[i]));
                }
            }
            b.AppendLine("");
            b.AppendLine("    FILE OPEN_WRITE '/tmp/nativecode' nativefd");
            b.AppendLine("    FILE WRITE_BYTES nativefd " + (c.Length) + " nativecode");
            b.AppendLine("    FILE CLOSE nativefd");
            b.AppendLine("    SYSTEM 'chmod a+x /tmp/nativecode' errorcode");
            return b.ToString();
        }

        /// <summary>
        /// Convert a hexadecimal string to a byte array
        /// </summary>
        private static byte[] ToByteArray(String hexString)
        {
            byte[] retval = new byte[hexString.Length / 2];
            for (int i = 0; i < hexString.Length; i += 2)
                retval[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            return retval;
        }


        // ----------- handling of temporary variables ---------------

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
                    return "F" + (n - 1);
                case ExpressionType.Text: 
                    return "S" + (n - 1);
                default:
                    s.ThrowParseError("Return value that is an array must be directly stored in a variable");
                    return "";
//                case ExpressionType.NumberArray: 
//                    return "A" + (n - 1);
//                case ExpressionType.TextArray: 
//                    return "X" + (n - 1);
          }
        }
        public void releaseVariable(ExpressionType type)
        {
            reservedtemporaries[type]--;
        }

        public int GetLabelNumber()
        {
            return labelcount++;
        }

        // --------------------------- TOP-DOWN PARSER -------------------------------

        public void memorize_reference(String name)
        {
            LibraryEntry impl = (LibraryEntry)library[name];
            if (impl == null)
            {
                s.ThrowParseError("Reference to undefined function: " + name);
            }
            else if (!references.Contains(impl))
            {
                references.Add(impl);
                for (int i=0; i<impl.references.Length; i++)
                {
                    memorize_reference(impl.references[i]);
                }
            }
        }

        // --------------- compile a whole basic program ----------

        private void compile_sub(TextWriter target)
        {
            parse_keyword("SUB");

            if (s.NextType != SymType.ID)
            {
                s.ThrowExpectedSymbol(SymType.ID, null);
            }
            String subname = s.NextContent;
            s.GetSym();

            parse_eol();

            target.WriteLine("SUB_" + subname + ":");

            while (!s.NextIsKEYWORD("ENDSUB"))
            {
                compile_statement(target);
            }
            parse_keyword("ENDSUB");
            parse_eol();

            target.WriteLine("    SUB8 STACKPOINTER 1 STACKPOINTER");
            target.WriteLine("    READ32 RETURNSTACK STACKPOINTER INDEX");
            target.WriteLine("    JR_DYNAMIC INDEX");
            target.WriteLine("ENDSUB_" + subname + ":");
        }


        private void compile_statement(TextWriter target)
        {
            if (s.NextType == SymType.PRAGMA)
            {
                if (s.NextContent.Equals("NOBOUNDSCHECK"))
                {
                    noboundscheck = true;
                }
                else if (s.NextContent.Equals("BOUNDSCHECK"))
                {
                    noboundscheck = false;
                }
                else if (s.NextContent.Equals("NODIVISIONCHECK"))
                {
                    nodivisioncheck = true;
                }
                else if (s.NextContent.Equals("DIVISIONCHECK"))
                {
                    nodivisioncheck = false;
                }
                else
                {
                    s.ThrowParseError("Unknown PRAGMA: " + s.NextContent);
                }
                s.GetSym();
                return;
            }
            else if (s.NextType == SymType.EOL)
            {
                s.GetSym();
                return;
            }
            else if (s.NextIsKEYWORD("IF"))
            {
                compile_if(target);
            }
            else if (s.NextIsKEYWORD("WHILE"))
            {
                compile_while(target);
            }
            else if (s.NextIsKEYWORD("FOR"))
            {
                compile_for(target);
            }
            else if (s.NextIsKEYWORD("GOTO"))
            {
                compile_goto(target);
            }
            else
            {
                compile_atomic_statement(target);
                parse_eol();
            }
        }

        private void compile_if(TextWriter target)
        {
            int l = GetLabelNumber();

            parse_keyword("IF");

            Expression e = parse_typed_expression(ExpressionType.Text, "Need a text as a boolean value here");
            e.GenerateJumpIfCondition(this, target, "else" + l + "_1", false);

            parse_keyword("THEN");
            parse_eol();

            int numbranches = 0;
            for (; ; )
            {
                if (s.NextIsKEYWORD("ELSEIF"))
                {
                    parse_keyword("ELSEIF");

                    Expression e2 = parse_typed_expression(ExpressionType.Text, "Need a text as a boolean value here");

                    numbranches++;
                    target.WriteLine("    JR endif" + l);
                    target.WriteLine("  else" + l + "_" + numbranches + ":");
                    e2.GenerateJumpIfCondition(this, target, "else" + l + "_" + (numbranches + 1), false);

                    parse_keyword("THEN");
                    parse_eol();
                }
                else if (s.NextIsKEYWORD("ELSE"))
                {
                    parse_keyword("ELSE");
                    parse_eol();

                    numbranches++;
                    target.WriteLine("    JR endif" + l);
                    target.WriteLine("  else" + l + "_" + numbranches + ":");

                    while (!(s.NextIsKEYWORD("ENDIF")))
                    {
                        compile_statement(target);
                    }
                    break;
                }
                else if (s.NextIsKEYWORD("ENDIF"))
                {
                    break;
                }
                else
                {
                    compile_statement(target);
                }
            }

            parse_keyword("ENDIF");
            parse_eol();

            target.WriteLine("  else" + l + "_" + (numbranches + 1) + ":");
             target.WriteLine("  endif" + l + ":");
        }

        private void compile_while(TextWriter target)
        {
            int l = GetLabelNumber();

            parse_keyword("WHILE");

            Expression e = parse_typed_expression(ExpressionType.Text, "Need a text as a boolean value here");

            target.WriteLine("  while" + l + ":");
            e.GenerateJumpIfCondition(this, target, "endwhile" + l, false);
            target.WriteLine("  whilebody" + l + ":");
            parse_eol();

            while (!s.NextIsKEYWORD("ENDWHILE"))
            {
                compile_statement(target);
            }
            parse_keyword("ENDWHILE");
            parse_eol();

            e.GenerateJumpIfCondition(this, target, "whilebody" + l, true);
            target.WriteLine("  endwhile" + l + ":");
        }


        private void compile_for(TextWriter target)
        {
            int l = GetLabelNumber();

            parse_keyword("FOR");

            String basicvarname = parse_id();
            String varname = "V" + basicvarname;

            if (!variables.ContainsKey(varname))
            {   
                variables[varname] = ExpressionType.Number;
            }
            else if (variables[varname] != ExpressionType.Number)
            {
                s.ThrowParseError("Can not use " + basicvarname + " as loop counter. Is already defined to contain non-number");
            }

            parse_special("=");

            Expression startexpression = parse_float_expression("Can only use a number as loop start value");

            parse_keyword("TO");

            Expression stopexpression = parse_float_expression("Can only use a number as loop stop value");

            Expression testexpression;
            Expression incexpression;
            if (s.NextIsKEYWORD("STEP"))
            {
                parse_keyword("STEP");
                Expression stepexpression = parse_float_expression("Can only use a number as loop step value");

                if (stepexpression.IsPositive())
                {   // step is guaranteed to be positive - loop ends if counter gets too large
                    testexpression = new ComparisonExpression("CALL LE", "JR_LTEQF", "JR_GTF", 
                        new AtomicExpression(ExpressionType.Number, varname), stopexpression);
                }
                else if (stepexpression.IsNegative())
                {   // step is guaranteed to be negative - loop ends if counter gets too small
                    testexpression = new ComparisonExpression("CALL GE", "JR_GTEQF", "JR_LTF", 
                        new AtomicExpression(ExpressionType.Number, varname), stopexpression);
                }
                else
                {   // unknown step direction - must use a special subroutine to determine loop end
                    testexpression = new CallExpression(ExpressionType.Text, "CALL LE_STEP", 
                        new AtomicExpression(ExpressionType.Number, varname), stopexpression, stepexpression);
                }
                incexpression = new CallExpression(ExpressionType.Number, "ADDF", 
                    new AtomicExpression(ExpressionType.Number, varname), stepexpression);
            }
            else
            {
                testexpression = new ComparisonExpression("CALL LE", "JR_LTEQF", "JR_GTF", 
                    new AtomicExpression(ExpressionType.Number, varname), stopexpression);
                incexpression = new CallExpression(ExpressionType.Number, "ADDF", 
                    new AtomicExpression(ExpressionType.Number, varname), new NumberExpression(1.0));
            }
            parse_eol();

            startexpression.Generate(this, target, varname);

            target.WriteLine("  for" + l + ":");
            testexpression.GenerateJumpIfCondition(this, target, "endfor" + l, false);
            target.WriteLine("  forbody" + l + ":");

            while (!s.NextIsKEYWORD("ENDFOR"))
            {
                compile_statement(target);
            }
            parse_keyword("ENDFOR");
            parse_eol();

            incexpression.Generate(this, target, varname);
            testexpression.GenerateJumpIfCondition(this, target, "forbody" + l, true);
            target.WriteLine("  endfor" + l + ":");
        }

        private void compile_goto(TextWriter target)
        {
            parse_keyword("GOTO");

            if (s.NextType != SymType.ID)
            {
                s.ThrowExpectedSymbol(SymType.ID, null);
            }

            String label = s.NextContent;
            s.GetSym();

            target.WriteLine("    JR L" + label);

            parse_eol();
        }

        private void compile_atomic_statement(TextWriter target)
        {
            // get symbol for look-ahead
            if (s.NextType != SymType.ID)
            {
                s.ThrowExpectedSymbol(SymType.ID, null);
            }
            String id = s.NextContent;
            s.GetSym();

            if (s.NextIsSPECIAL("="))
            {
                s.PushBack(SymType.ID, id);
                compile_variable_assignment(target);
            }
            else if (s.NextIsSPECIAL("["))
            {
                s.PushBack(SymType.ID, id);
                compile_array_assignment(target);
            }
            else if (s.NextIsSPECIAL("."))
            {
                s.PushBack(SymType.ID, id);
                compile_procedure_call_or_property_set(target);
            }
            else if (s.NextIsSPECIAL("("))
            {   // subroutine call             
                parse_special("(");
                parse_special(")");

                String returnlabel = "CALLSUB"+(GetLabelNumber());
                target.WriteLine("    WRITE32 ENDSUB_"+id+":"+returnlabel + " STACKPOINTER RETURNSTACK");
                target.WriteLine("    ADD8 STACKPOINTER 1 STACKPOINTER");
                target.WriteLine("    JR SUB_" + id);
                target.WriteLine(returnlabel+":");
            }
            else if (s.NextIsSPECIAL(":"))
            {   // jump label
                parse_special(":");
                target.WriteLine("  L" + id + ":");
            }
            else
            {
                s.ThrowUnexpectedSymbol();
            }
        }

        private void compile_variable_assignment(TextWriter target)
        {
            String basicvarname = parse_id();
            parse_special("=");
            Expression e = parse_expression();

            String varname = "V" + basicvarname;            
            if (!variables.ContainsKey(varname))
            {
                variables[varname] = e.type;
            }
            else if (variables[varname] != e.type)
            {
                s.ThrowParseError("Can not assign different types to " + basicvarname);
            }

            e.Generate(this, target, varname);
        }

        private void compile_array_assignment(TextWriter target)
        {
            String basicvarname = parse_id();
            parse_special("[");
            Expression eidx = parse_float_expression("Can only have number as array index");
            parse_special("]");
            parse_special("=");
            Expression e = parse_expression();

            if (e.type != ExpressionType.Number && e.type != ExpressionType.Text)
            {
                s.ThrowParseError("Can only store numbers or strings into arrays");
            }
            ExpressionType atype = (e.type == ExpressionType.Number) ? ExpressionType.NumberArray : ExpressionType.TextArray;

            String varname = "V" + basicvarname;
            if (!variables.ContainsKey(varname))
            {
                variables[varname] = atype;
            }
            else if (variables[varname] != atype)
            {
                s.ThrowParseError("Can not use "+ basicvarname+" as array to store this type");
            }

            if (e.type==ExpressionType.Text)
            {
                Expression aex = new CallExpression(ExpressionType.Void, "CALL ARRAYSTORE_STRING :0 :1 "+varname, eidx, e);
                aex.Generate(this, target, null);
            }
            else
            {
                if (noboundscheck)
                {
                    if (eidx is NumberExpression)
                    {   // index is known at compile-time
                        int indexval = (int)(((NumberExpression)eidx).value);
                        if (indexval >= 0)        // when negative do not store anything
                        {
                            Expression aex = new CallExpression(ExpressionType.Void, "ARRAY_WRITE " + varname + " " + indexval, e);
                            aex.Generate(this, target, null);
                        }
                    }
                    else
                    {   // compute index at run-time
                        Expression aex = new CallExpression(ExpressionType.Void, "MOVEF_32 :0 INDEX\n    ARRAY_WRITE " + varname + " INDEX :1", eidx, e);
                        aex.Generate(this, target, null);
                    }
                }
                else
                {
                    Expression aex = new CallExpression(ExpressionType.Void, "CALL ARRAYSTORE_FLOAT :0 :1 " + varname, eidx, e);
                    aex.Generate(this, target, null);
                }
            }
        }

        private void compile_procedure_call_or_property_set(TextWriter target)
        {
            String objectname = parse_id();
            parse_special(".");
            String elementname = parse_id();

            // this is in fact a property set attempt
            if (s.NextIsSPECIAL("="))
            {
                parse_special("=");

                // have hardcoded property set for thread start
                if (objectname.Equals("THREAD") && elementname.Equals("RUN"))
                {
                    if (s.NextType!=SymType.ID)
                    {
                        s.ThrowParseError("Need subprogram name here to start a thread");
                    }
                    String id = parse_id();
                    int l = GetLabelNumber();
                    target.WriteLine("    DATA32 tmp" + l);
                    target.WriteLine("    CALL GETANDINC32 RUNCOUNTER_" + id + " 1  RUNCOUNTER_" + id + " tmp"+l);
                    target.WriteLine("    JR_NEQ32 0 tmp" + l + " alreadylaunched" + l);
                    target.WriteLine("    OBJECT_START T" + id);
                    target.WriteLine("  alreadylaunched" + l + ":");
                    if (!threadnames.Contains(id)) 
                    {   threadnames.Add(id);
                    }
                }
                else
                {
                    s.ThrowParseError("Unknown property to set:  " + objectname + "." + elementname);
                }

                return;
            }
            // procedure call
            else
            {
                parse_special("(");

                String functionname = objectname + "." + elementname;

                LibraryEntry libentry = (LibraryEntry)library[functionname];
                if (libentry == null)
                {
                    s.ThrowParseError("Undefined function: " + functionname);
                }


                List<Expression> list = new List<Expression>();
                while (list.Count < libentry.paramTypes.Length)
                {
                    Expression e = parse_typed_expression_with_parameterconversion(libentry.paramTypes[list.Count]);
                    list.Add(e);
                    if (s.NextIsSPECIAL(","))     // skip optional ',' after each parameter
                    {
                        parse_special(",");
                    }
                }
                parse_special(")");

                Expression ex = new CallExpression(ExpressionType.Void, libentry.inline ? libentry.programCode : ("CALL " + functionname), list);
                if (libentry.returnType == ExpressionType.Void)
                {
                    ex.Generate(this, target, null);
                }
                else
                {
                    String retvar = reserveVariable(libentry.returnType);
                    ex.Generate(this, target, retvar);
                    releaseVariable(libentry.returnType);
                }
            }
        }



        private Expression parse_typed_expression_with_parameterconversion(ExpressionType type)
        {
            Expression e = parse_expression();

            if (e.type == ExpressionType.Number && type == ExpressionType.Text)
            {   // can do a automatic conversion from number to text
                return new CallExpression(ExpressionType.Text, "STRINGS VALUE_FORMATTED :0 '%g' 99", e);
            }
            else if (e.type != type)
            {
                s.ThrowParseError("Can not use this type as a call parameter");
            }
            return e;
        }

        private Expression parse_float_expression(String reasonmessage)
        {
            return parse_typed_expression(ExpressionType.Number, reasonmessage);
        }

        private Expression parse_typed_expression(ExpressionType type, String reasonmessage)
        {
            Expression e = parse_expression();
            if (e.type!=type)
            {
                s.ThrowParseError(reasonmessage);
            }
            return e;
        }

        private Expression parse_expression()
        {
            return parse_or_expression();
        }

        private Expression parse_or_expression()
        {
            Expression total = parse_and_expression();
            for (; ; )
            {
                if (s.NextIsKEYWORD("OR"))
                {
                    s.GetSym();

                    if (total.type!=ExpressionType.Text) s.ThrowParseError("need text on left side of OR");
                    Expression right = parse_and_expression();
                    if (right.type!=ExpressionType.Text) s.ThrowParseError("need text on right side of OR");
                    total = new OrExpression(total, right);
                }
                else
                {
                    break;
                }
            }
            return total;
        }

        private Expression parse_and_expression()
        {
            Expression total = parse_comparative_expression();
            for (; ; )
            {
                if (s.NextIsKEYWORD("AND"))
                {
                    s.GetSym();

                    if (total.type!=ExpressionType.Text) s.ThrowParseError("need text on left side of AND");
                    Expression right = parse_comparative_expression();
                    if (right.type!=ExpressionType.Text) s.ThrowParseError("need text on right side of AND");
                    total = new AndExpression(total, right);
                }
                else
                {
                    break;
                }
            }
            return total;
        }


        private Expression parse_comparative_expression()
        {
            Expression total = parse_additive_expression();

            for (; ; )
            {
                if (s.NextIsSPECIAL("="))
                {
                    s.GetSym();

                    Expression right = parse_additive_expression();
                    if (total.type != right.type)
                    {
                        s.ThrowParseError("Need identical types on both sides of '='");
                    }
                    switch (total.type)
                    {
                        case ExpressionType.Number:
                            total = new ComparisonExpression("CALL EQ_FLOAT", "JR_EQF", "JR_NEQF", total, right);
                            break;
                        case ExpressionType.Text:
                            total = new CallExpression(ExpressionType.Text, "CALL EQ_STRING", total, right);
                            break;
                        default:
                            s.ThrowParseError("Can not compare arrays");
                            break;
                    }
                }
                else if (s.NextIsSPECIAL("<>"))
                {
                    s.GetSym();

                    Expression right = parse_additive_expression();
                    if (total.type != right.type)
                    {
                        s.ThrowParseError("Need identical types on both sides of '<>'");
                    }
                    switch (total.type)
                    {
                        case ExpressionType.Number:
                            total = new ComparisonExpression("CALL NEQ_FLOAT", "JR_NEQF", "JR_EQF", total, right);
                            break;
                        case ExpressionType.Text:
                            total = new CallExpression(ExpressionType.Text, "CALL NE_STRING", total, right);
                            break;
                        default:
                            s.ThrowParseError("Can not compare arrays");
                            break;
                    }
                }
                else if (s.NextIsSPECIAL("<"))
                {
                    s.GetSym();
                    if (total.type!=ExpressionType.Number) s.ThrowParseError("need number on left side of '<'");
                    Expression right = parse_additive_expression();
                    if (right.type!=ExpressionType.Number) s.ThrowParseError("need number on right side of '<'");
                    total = new ComparisonExpression("CALL LT", "JR_LTF", "JR_GTEQF", total, right);
                }
                else if (s.NextIsSPECIAL(">"))
                {
                    s.GetSym();
                    if (total.type!=ExpressionType.Number) s.ThrowParseError("need number on left side of '>'");
                    Expression right = parse_additive_expression();
                    if (right.type != ExpressionType.Number) s.ThrowParseError("need number on right side of '>'");
                    total = new ComparisonExpression("CALL GT", "JR_GTF", "JR_LTEQF", total, right);
                }
                else if (s.NextIsSPECIAL("<="))
                {
                    s.GetSym();
                    if (total.type!=ExpressionType.Number) s.ThrowParseError("need number on left side of '<='");
                    Expression right = parse_additive_expression();
                    if (right.type != ExpressionType.Number) s.ThrowParseError("need number on right side of '<='");
                    total = new ComparisonExpression("CALL LE", "JR_LTEQF", "JR_GTF", total, right);
                }
                else if (s.NextIsSPECIAL(">="))
                {
                    s.GetSym();
                    if (total.type != ExpressionType.Number) s.ThrowParseError("need number on left side of '>='");
                    Expression right = parse_additive_expression();
                    if (right.type != ExpressionType.Number) s.ThrowParseError("need number on right side of '>='");
                    total = new ComparisonExpression("CALL GE", "JR_GTEQF", "JR_LTF", total, right);
                }
                else
                {
                    break;
                }
            }

            return total;
        }


        private Expression parse_additive_expression()
        {
            Expression total = parse_multiplicative_expression();

            for (; ; )
            {
                if (s.NextIsSPECIAL("+"))
                {
                    s.GetSym();

                    Expression right = parse_multiplicative_expression();

                    if (total.type==ExpressionType.Text)
                    {
                        if (right.type==ExpressionType.Number)
                        {
                            right = new CallExpression(ExpressionType.Text, "STRINGS VALUE_FORMATTED :0 '%g' 99", right);
                        }
                        if (right.type!=ExpressionType.Text)
                        {
                            s.ThrowParseError("Can not concat arrays");
                        }
                        total = new CallExpression(ExpressionType.Text, "CALL TEXT.APPEND", total, right);
                    }
                    else if (total.type==ExpressionType.Number)
                    {
                        if (right.type == ExpressionType.Text)
                        {
                            total = new CallExpression(ExpressionType.Text, "STRINGS VALUE_FORMATTED :0 '%g' 99", total);
                            total = new CallExpression(ExpressionType.Text, "CALL TEXT.APPEND", total, right);
                        }
                        else if (right.type == ExpressionType.Number)
                        {                            
                            if (total is NumberExpression && right is NumberExpression)
                            {   // can do compile-time calculation
                                total = new NumberExpression(((NumberExpression)total).value + ((NumberExpression)right).value);
                            }
                            else
                            {   // must calculate at run-time
                                total = new CallExpression(ExpressionType.Number, "ADDF", total, right);
                            }
                        }
                        else
                        {
                            s.ThrowParseError("Can not concat arrays");
                        }
                    }
                    else
                    {
                        s.ThrowParseError("Can not concat arrays");
                    }
                }

                else if (s.NextIsSPECIAL("-"))
                {
                    s.GetSym();
                    if (total.type != ExpressionType.Number) s.ThrowParseError("need number on left side of '-'");
                    Expression right = parse_multiplicative_expression();
                    if (right.type != ExpressionType.Number) s.ThrowParseError("need number on right side of '-'");

                    if (total is NumberExpression && right is NumberExpression)
                    {   // can do compile-time calculation
                        total = new NumberExpression(((NumberExpression)total).value - ((NumberExpression)right).value);
                    }
                    else
                    {   // must calculate at run-time
                        total = new CallExpression(ExpressionType.Number, "SUBF", total, right);
                    }
                }
                else
                {
                    break;
                }
            }

            return total;
        }

        private Expression parse_multiplicative_expression()
        {
            Expression total = parse_unary_minus_expression();

            for (; ; )
            {
                if (s.NextIsSPECIAL("*"))
                {
                    s.GetSym();
                    if (total.type!=ExpressionType.Number) s.ThrowParseError("need number on left side of '*'");
                    Expression right = parse_unary_minus_expression();
                    if (right.type != ExpressionType.Number) s.ThrowParseError("need number on right side of '*'");

                    if (total is NumberExpression && right is NumberExpression)
                    {   // can do compile-time calculation
                        total = new NumberExpression(((NumberExpression)total).value * ((NumberExpression)right).value);
                    }
                    else
                    {   // must calculate at run-time
                        total = new CallExpression(ExpressionType.Number, "MULF", total, right);
                    }
                }
                else if (s.NextIsSPECIAL("/"))
                {
                    s.GetSym();
                    if (total.type != ExpressionType.Number) s.ThrowParseError("need number on left side of '/'");
                    Expression right = parse_unary_minus_expression();
                    if (right.type!=ExpressionType.Number) s.ThrowParseError("need number on right side of '/'");

                    if (total is NumberExpression && right is NumberExpression)
                    {   // can do compile-time calculation
                        double a = ((NumberExpression)total).value;
                        double b = ((NumberExpression)right).value;
                        total = new NumberExpression(b==0.0 ? 0.0 : (a/b));
                    }
                    else
                    {   // must calculate at run-time
                        if (nodivisioncheck)
                        {
                            total = new CallExpression(ExpressionType.Number, "DIVF", total, right);
                        }
                        else
                        {
                            total = new CallExpression(ExpressionType.Number, 
                                   "DATAF tmpf:#\n"
	                        +  "    DATA8 flag:#\n"
	                        +  "    DIVF :0 :1 tmpf:#\n"
	                        +  "    CP_EQF 0.0 :1 flag:#\n"
	                        +  "    SELECTF flag:# 0.0 tmpf:# :2\n" ,
                            total, right);
                        }
                    }
                }
                else
                {
                    break;
                }
            }

            return total;
        }

        private Expression parse_unary_minus_expression()
        {
            if (s.NextIsSPECIAL("-"))
            {
                s.GetSym();

                Expression e = parse_unary_minus_expression();
                if (e.type!=ExpressionType.Number) s.ThrowParseError("need number after '-'");

                if (e is NumberExpression)
                {   // can do compile-time calculation
                    return new NumberExpression(-((NumberExpression)e).value);
                }
                else
                {   // must calculate at run-time 
                    return new CallExpression(ExpressionType.Number, "MATH NEGATE", e);
                }
            }
            else
            {
                return parse_atomic_expression();
            }
        }


        private Expression parse_atomic_expression()
        {
            if (s.NextIsSPECIAL("("))
            {
                s.GetSym();
                Expression e = parse_expression();
                parse_special(")");
                return e;
            }
            else if (s.NextType == SymType.STRING)
            {
                String val = s.NextContent;
                if (val.Length>251)
                {
                    s.ThrowParseError("Text is longer than 251 letters");
                }
                s.GetSym();
                return new AtomicExpression(ExpressionType.Text, "'" + EscapeString(val) + "'");
            }
            else if (s.NextType == SymType.NUMBER)
            {
                double val;
                if (!double.TryParse(s.NextContent, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                {
                    s.ThrowParseError("Can not decode number: "+s.NextContent);
                }
                s.GetSym();
                return new NumberExpression(val);
            }
            else if (s.NextType == SymType.ID)
            {
                String var_or_object = s.NextContent;  // must be a variable name or an object (depends on the precence of a '.' afterwards)
                s.GetSym();

                if (s.NextIsSPECIAL("."))
                {
                    s.PushBack(SymType.ID, var_or_object);
                    return parse_function_call_or_property();
                }
                else if (s.NextIsSPECIAL("["))
                {   // is array reference
                    String varname = "V" + var_or_object;
                    if (!variables.ContainsKey(varname))
                    {
                        s.ThrowParseError("can not use array " + var_or_object + " before first assignment");
                    }
                    ExpressionType atype = ExpressionType.Void;
                    switch (variables[varname])
                    {   case ExpressionType.NumberArray:
                            atype = ExpressionType.Number;
                            break;
                        case ExpressionType.TextArray:
                            atype = ExpressionType.Text;
                            break;
                        default:
                            s.ThrowParseError("Need array to use with '[]'");
                            break;
                    }

                    parse_special("[");
                    Expression e = parse_float_expression("only numbers are allowed as array index");
                    parse_special("]");

                    if (atype==ExpressionType.Text)
                    {
                        return new CallExpression(ExpressionType.Text, "CALL ARRAYGET_STRING :0 :1 "+varname, e);
                    }
                    else
                    {
                        if (noboundscheck)
                        {
                            return new UnsafeArrayGetExpression(varname, e);
                        }
                        else
                        {
                            return new CallExpression(ExpressionType.Number, "CALL ARRAYGET_FLOAT :0 :1 " + varname, e);
                        }
                    }
                }
                else
                {
                    // is variable use
                    String varname = "V" + var_or_object;
                    if (!variables.ContainsKey(varname))
                    {
                        s.ThrowParseError("can not use variable " + var_or_object + " before first assignment");
                    }
                    return new AtomicExpression(variables[varname], varname);
                }
            }
            else
            {
                s.ThrowUnexpectedSymbol();
                return null;
            }
        }

        private Expression parse_function_call_or_property()
        {
            String objectname = parse_id();
            parse_special(".");
            String elementname = parse_id();

            String functionname = objectname + "." + elementname;

            LibraryEntry libentry = (LibraryEntry)library[functionname];
            if (libentry == null)
            {
                s.ThrowParseError("Undefined function or property: " + functionname);
            }
            if (libentry.returnType==ExpressionType.Void)
            {
                s.ThrowParseError("Can not use function that returns nothing in an expression");
            }

            List<Expression> list = new List<Expression>();     // parameter list
            if (s.NextIsSPECIAL("("))
            {   // a function call
                parse_special("(");

                while (list.Count < libentry.paramTypes.Length)
                {
                    Expression e = parse_typed_expression_with_parameterconversion(libentry.paramTypes[list.Count]);
                    list.Add(e);

                    if (s.NextIsSPECIAL(","))     // skip optional ',' after each parameter
                    {
                        s.GetSym();
                    }
                }
                parse_special(")");

                if (list.Count < libentry.paramTypes.Length)
                {
                    s.ThrowParseError("Too few arguments to " + functionname);
                }
            }
            else
            {   // a property reference
                if (libentry.paramTypes.Length != 0)
                {
                    s.ThrowParseError("Can not reference " + functionname+" as a property");
                }
            }

            return new CallExpression(libentry.returnType, libentry.inline ? libentry.programCode : ("CALL " + functionname), list);
        }

        private String parse_id()
        {
            if (s.NextType!=SymType.ID)
            {
                s.ThrowExpectedSymbol(SymType.ID,null);
            }
            String id = s.NextContent;
            s.GetSym();
            return id;
        }

        private void parse_keyword(String k)
        {
            if (!s.NextIsKEYWORD(k))
            {
                s.ThrowExpectedSymbol(SymType.KEYWORD, k);
            }
            s.GetSym();
        }

        private void parse_special(String k)
        {
            if (!s.NextIsSPECIAL(k))
            {
                s.ThrowExpectedSymbol(SymType.SPECIAL, k);
            }
            s.GetSym();
        }

        private void parse_eol()
        {
            if (s.NextType != SymType.EOL)
            {
                s.ThrowExpectedSymbol(SymType.EOL, null);
            }
            s.GetSym();
        }


        private static String EscapeString(String v)
        {
            String s = v;
            for (int i=0; i<s.Length; i++)
            {
                int c = s[i];
                if (c<=0 || c>255)
                {   c = 1;
                }

                if (c<32 || c>127 || c=='\'' || c=='\\')
                {
                    int d0 = c % 8;
                    int d1 = (c/8) % 8;
                    int d2 = (c/64);
                    String esc = "\\"+d2+""+d1+""+d0;
                    s = s.Substring(0, i) + esc + s.Substring(i + 1);
                    i += esc.Length - 1;
                }
            }
            return s;
        }
    }


}
