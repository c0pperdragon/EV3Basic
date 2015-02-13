using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections;

namespace EV3BasicCompiler
{
    public class Compiler
    {
        // data loaded once at creation of Compiler (can be used to compile multiple programs)
        Hashtable library;
        String runtimeglobals;

        // data used during one compilation run
        Scanner s;
        int labelcount;
        public int maxfloats;
        public int maxstrings;
        Hashtable variables;                    // maps varaiable names to   "S","F","[S","[F"   (e.g.  "VTEMP" -> "S")
        Hashtable constants;                    // maps value strints to constant names   (e.g.  "0.1" -> "C5")
        HashSet<LibraryEntry> references;

        bool noboundscheck;
        bool nodivisioncheck;

        public Compiler()
        {
            library = new Hashtable();

            variables = new Hashtable();
            constants = new Hashtable();
            references = new HashSet<LibraryEntry>();

            readLibrary();
        }

        private void readLibrary()
        {
            StringReader reader = new StringReader(EV3BasicCompiler.Properties.Resources.runtimelibrary);
            
            String currentfirstline = null;
            StringBuilder body = new StringBuilder();

            StringBuilder globals = new StringBuilder();

            String line;
            while ((line = reader.ReadLine()) != null)
            {
                if (currentfirstline == null)
                {
                    int idx = line.IndexOf("subcall");
                    if (idx >= 0)
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
                            globals.AppendLine(line);
                        }
                    }
                }
                else
                {
                    body.AppendLine(line);
                    if (line.IndexOf("}") >= 0)
                    {
                        int idx1 = currentfirstline.IndexOf("subcall") + 7;
                        int idx2 = currentfirstline.IndexOf("//", idx1);
                        String functionname = currentfirstline.Substring(idx1, idx2 - idx1).Trim();
                        String[] descriptorandreferences = currentfirstline.Substring(idx2 + 2).Trim().Split(new char[]{' ','\t'}, StringSplitOptions.RemoveEmptyEntries);

                        LibraryEntry le = new LibraryEntry(descriptorandreferences, body.ToString());
                        library[functionname] = le;

                        currentfirstline = null;
                    }
                }
            }

            reader.Close();

            runtimeglobals = globals.ToString();
        }

        public void Compile(Stream source, Stream targetstream, List<String> errorlist)
        {
            // start reading input file in tokenized form
            s = new Scanner(source); 
            s.GetSym();

            // initialize sybmol tables and other dynamic stuff
            this.labelcount = 0;
            this.maxfloats = 1;     // at least FLOAT_0  needs to be defined
            this.maxstrings = 1;    // at least STRING_0 needs to be defined
            this.variables.Clear();
            this.constants.Clear();
            this.references.Clear();
            this.noboundscheck = false;
            this.nodivisioncheck = false;
            constants["0"] = "C0";                                   // always have constant 0
            constants["57.295779513082"] = "CR2D"; // conversion factor from radians to degrees

            // perpare buffers to keep parts of the output
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
            target.WriteLine("DATA32 INDEX");

            foreach (DictionaryEntry de in constants)
            {
                target.WriteLine("DATAF " + de.Value);
            }
            foreach (DictionaryEntry de in variables)
            {
                String t = (String)de.Value;
                if (t.Equals("S"))
                {
                    target.WriteLine("DATAS " + de.Key + " 252");
                }
                else if (t.Equals("F"))
                {
                    target.WriteLine("DATAF " + de.Key);
                }
                else if (t.StartsWith("["))
                {
                    target.WriteLine("ARRAY16 " + de.Key+ " 2");  // waste 16 bit, but keep alignment
                }
            }
            for (int i = 0; i < maxfloats; i++)
            {
                target.WriteLine("DATAF F" + i);
            }
            for (int i = 0; i < maxstrings; i++)
            {
                target.WriteLine("DATAS S" + i + " 252");
            }
            target.WriteLine();

            target.WriteLine("vmthread MAIN");
            target.WriteLine("{");
            foreach (DictionaryEntry de in constants)
            {
                target.WriteLine("    STRINGS STRING_TO_VALUE '" + de.Key + "' " + de.Value);
            }
            foreach (DictionaryEntry de in variables)
            {
                String t = (String)de.Value;
                if (t.Equals("S"))
                {
                    target.WriteLine("    STRINGS DUPLICATE '' " + de.Key);
                }
                else if (t.Equals("F"))
                {
                    target.WriteLine("    MOVEF_F C0 " + de.Key);
                }
                else if (t.Equals("[S"))
                {
                    target.WriteLine("    CALL ARRAYCREATE_STRING " + de.Key);
                    memorize_reference("ARRAYCREATE_STRING");
                }
                else if (t.Equals("[F"))
                {
                    target.WriteLine("    CALL ARRAYCREATE_FLOAT " + de.Key);
                    memorize_reference("ARRAYCREATE_FLOAT");
                }
            }


            target.WriteLine();
            target.Write(mainprogram.ToString());
            target.WriteLine("}");

            target.Write(subroutines);

//            foreach (DictionaryEntry de in library)
//            {   LibraryEntry le = (LibraryEntry) de.Value;
            foreach (LibraryEntry le in references)
            {
                target.Write(le.programCode);
            }
            target.Flush();
        }


        // --------------------------- TOP-DOWN PARSER -------------------------------

        private void memorize_reference(String name)
        {
            LibraryEntry impl = (LibraryEntry)library[name];
            if (impl == null)
            {
                throw new Exception("Reference to undefined function: " + name);
            }
            if (!references.Contains(impl))
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

            target.WriteLine("subcall SUB_" + subname);
            target.WriteLine("{");

            while (!s.NextIsKEYWORD("ENDSUB"))
            {
                compile_statement(target);
            }
            parse_keyword("ENDSUB");
            parse_eol();

            target.WriteLine("}");
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
                    throw new Exception("Unknown PRAGMA: " + s.NextContent);
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
            int l = labelcount++;

            parse_keyword("IF");

            Expression e = parse_string_expression();
            e.GenerateJumpIfNotTrue(this, target, "else" + l + "_1");

            parse_keyword("THEN");
            parse_eol();

            int numbranches = 0;
            for (; ; )
            {
                if (s.NextIsKEYWORD("ELSEIF"))
                {
                    parse_keyword("ELSEIF");

                    Expression e2 = parse_string_expression();

                    numbranches++;
                    target.WriteLine("    JR endif" + l);
                    target.WriteLine("  else" + l + "_" + numbranches + ":");
                    e2.GenerateJumpIfNotTrue(this, target, "else" + l + "_" + (numbranches + 1));

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
            int l = labelcount++;

            parse_keyword("WHILE");

            Expression e = parse_string_expression();

            target.WriteLine("  while" + l + ":");
            e.GenerateJumpIfNotTrue(this, target, "endwhile" + l);
            target.WriteLine("  whilebody" + l + ":");
            parse_eol();

            while (!s.NextIsKEYWORD("ENDWHILE"))
            {
                compile_statement(target);
            }
            parse_keyword("ENDWHILE");
            parse_eol();

            e.GenerateJumpIfTrue(this, target, "whilebody" + l);
            target.WriteLine("  endwhile" + l + ":");
        }


        private void compile_for(TextWriter target)
        {
            int l = labelcount++;

            parse_keyword("FOR");

            String basicvarname = parse_id();

            String varname = "V" + basicvarname;
            String vartype = (String)variables[varname];
            if (vartype == null)
            {
                variables[varname] = "F";
            }
            else if (!vartype.Equals("F"))
            {
                s.ThrowParseError("Can not use " + s.NextContent + " as loop counter. Is already defined to contain text");
            }

            parse_special("=");

            Expression startexpression = parse_float_expression("Can not use a text as loop start value");

            parse_keyword("TO");

            Expression stopexpression = parse_float_expression("Can not use a text as loop stop value");

            Expression testexpression;
            Expression incexpression;
            if (s.NextIsKEYWORD("STEP"))
            {
                parse_keyword("STEP");
                Expression stepexpression = parse_float_expression("Can not use a text as loop step value");

                if (stepexpression.IsPositive())
                {   // step is guaranteed to be positive - loop ends if counter gets too large
                    testexpression = new ComparisonExpression("CALL LE", "JR_LTEQF", "JR_GTF", new AtomicExpression(false, varname), stopexpression);
                }
                else if (stepexpression.IsNegative())
                {   // step is guaranteed to be negative - loop ends if counter gets too small
                    testexpression = new ComparisonExpression("CALL GE", "JR_GTEQF", "JR_LTF", new AtomicExpression(false, varname), stopexpression);
                }
                else
                {   // unknown step direction - must use a special subroutine to determine loop end
                    testexpression = new CallExpression(true, "CALL LE_STEP", new AtomicExpression(false, varname), stopexpression, stepexpression);
                }
                incexpression = new CallExpression(false, "ADDF", new AtomicExpression(false, varname), stepexpression);
                memorize_reference("LE_STEP");
            }
            else
            {
                testexpression = new ComparisonExpression("CALL LE", "JR_LTEQF", "JR_GTF", new AtomicExpression(false, varname), stopexpression);
                incexpression = new CallExpression(false, "ADDF", new AtomicExpression(false, varname), new AtomicExpression(false, "C1"));
                memorize_reference("LE");
            }
            parse_eol();

            startexpression.Generate(this, target, varname, 0, 0);

            target.WriteLine("  for" + l + ":");
            testexpression.GenerateJumpIfNotTrue(this, target, "endfor" + l);
            target.WriteLine("  forbody" + l + ":");

            while (!s.NextIsKEYWORD("ENDFOR"))
            {
                compile_statement(target);
            }
            parse_keyword("ENDFOR");
            parse_eol();

            incexpression.Generate(this, target, varname, 0, 0);
            testexpression.GenerateJumpIfTrue(this, target, "forbody" + l);
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
                compile_procedure_call(target);
            }
            else if (s.NextIsSPECIAL("("))
            {   // subroutine call             
                parse_special("(");
                parse_special(")");
                target.WriteLine("    CALL SUB_" + id);
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
            String varname = "V" + basicvarname;
            String vartype = (String)variables[varname];

            parse_special("=");

            // check if this is a direct variable-to-variable assignment (look ahead to EOL)
            if (s.NextType==SymType.ID)
            {
                String otherid = parse_id();
                if (s.NextType == SymType.EOL)
                {
                    // seems to be indeed a direct transfer
                    String sourcevarname = "V" + otherid;
                    String sourcetype = (String)variables[sourcevarname];
                    if (sourcetype != null && sourcetype.StartsWith("["))
                    {
                        if (vartype == null)
                        {
                            variables[varname] = sourcetype;
                        }
                        else if (!vartype.Equals(sourcetype))
                        {
                            s.ThrowParseError("Can not assign different type to " + basicvarname);
                        }
                        target.WriteLine("    ARRAY COPY " + sourcevarname + " " + varname);
                        return;
                    }
                }
                // is not a direct array transfer - just un-parse the symbol and try to parse an expression
                s.PushBack(SymType.ID, otherid);
            }

            // normal variable assignment from expression value
            Expression e = parse_expression();
            String etype = e.StringType ? "S" : "F";
            if (vartype == null)
            {
                variables[varname] = etype;
            }
            else if (!vartype.Equals(etype))
            {
                s.ThrowParseError("Can not assign different types to " + basicvarname);
            }

            e.Generate(this, target, varname, 0, 0);
        }

        private void compile_array_assignment(TextWriter target)
        {
            String basicvarname = parse_id();
            parse_special("[");
            Expression eidx = parse_float_expression("Can only have number as array index");
            parse_special("]");
            parse_special("=");
            Expression e = parse_expression();
            String varname = "V" + basicvarname;
            String atype = e.StringType ? "[S" : "[F";
            String vartype = (String)variables[varname];
            if (vartype == null)
            {
                variables[varname] = atype;
            }
            else if (!vartype.Equals(atype))
            {
                s.ThrowParseError("Can not assign different types to " + basicvarname);
            }

            if (e.StringType)
            {
                memorize_reference("ARRAYSTORE_STRING");
                Expression aex = new CallExpression(false, "CALL ARRAYSTORE_STRING:"+varname, eidx, e);
                aex.Generate(this, target, null, 0, 0);
            }
            else
            {
                if (noboundscheck)
                {
                    String exprvar = e.Generate(this, target, 0, 0);
                    String indexvar = eidx.Generate(this, target, 1, 0);
                    target.WriteLine("    MOVEF_32 " + indexvar + " INDEX");
                    target.WriteLine("    ARRAY_WRITE " + varname + " INDEX " + exprvar);
                }
                else
                {
                    memorize_reference("ARRAYSTORE_FLOAT");
                    Expression aex = new CallExpression(false, "CALL ARRAYSTORE_FLOAT:" + varname, eidx, e);
                    aex.Generate(this, target, null, 0, 0);
                }
            }
        }

        private void compile_procedure_call(TextWriter target)
        {
            String objectname = parse_id();
            parse_special(".");
            String elementname = parse_id();

            String functionname = objectname + "." + elementname;

            LibraryEntry libentry = (LibraryEntry)library[functionname];
            if (libentry == null)
            {
                s.ThrowParseError("Undefined function: " + functionname);
            }

            parse_special("(");

            List<Expression> list = new List<Expression>();
            while (list.Count < libentry.StringParamTypes.Length)
            {
                Expression e = libentry.StringParamTypes[list.Count] ? parse_string_expression() : parse_float_expression("Need a number as parameter here");

                list.Add(e);

                if (s.NextIsSPECIAL(","))     // skip optional ',' after each parameter
                {
                    parse_special(",");
                }
            }
            parse_special(")");

            Expression ex = new CallExpression(libentry.StringReturnType, "CALL " + functionname, list);
            ex.Generate(this, target,
                libentry.VoidReturnType ? null : libentry.StringReturnType ? "S0" : "F0",
                0, 0);

            memorize_reference(functionname);
        }


        private Expression parse_string_expression()
        {
            Expression e = parse_expression();
            if (e.StringType)
            {
                return e;
            }
            else
            {
                return new CallExpression(true, "STRINGS VALUE_FORMATTED", e, new AtomicExpression(true, "'%g'"), new AtomicExpression(false, "99"));
            }
        }

        private Expression parse_float_expression(String reasonmessage)
        {
            Expression e = parse_expression();
            if (e.StringType)
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

                    if (!total.StringType) s.ThrowParseError("need text on left side of OR");
                    Expression right = parse_and_expression();
                    if (!right.StringType) s.ThrowParseError("need text on right side of OR");
                    total = new OrExpression(total, right);
                    memorize_reference("OR");
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

                    if (!total.StringType) s.ThrowParseError("need text on left side of AND");
                    Expression right = parse_comparative_expression();
                    if (!right.StringType) s.ThrowParseError("need text on right side of AND");
                    total = new AndExpression(total, right);
                    memorize_reference("AND");
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

                    if (total.StringType)
                    {
                        Expression right = parse_additive_expression();
                        if (!right.StringType) s.ThrowParseError("need text on right side of '='");
                        total = new CallExpression(true, "CALL EQ_STRING", total, right);
                        memorize_reference("EQ_STRING");
                    }
                    else
                    {
                        Expression right = parse_additive_expression();
                        if (right.StringType) s.ThrowParseError("need number on right side of '='");
                        total = new ComparisonExpression("CALL EQ_FLOAT", "JR_EQF", "JR_NEQF", total, right);
                        memorize_reference("EQ_FLOAT");
                    }
                }
                else if (s.NextIsSPECIAL("<>"))
                {
                    s.GetSym();

                    if (total.StringType)
                    {
                        Expression right = parse_additive_expression();
                        if (!right.StringType) s.ThrowParseError("need text on right side of '<>'");
                        total = new CallExpression(true, "CALL NE_STRING", total, right);
                        memorize_reference("NE_STRING");
                    }
                    else
                    {
                        Expression right = parse_additive_expression();
                        if (right.StringType) s.ThrowParseError("need number on right side of '<>'");
                        total = new ComparisonExpression("CALL NEQ_FLOAT", "JR_NEQF", "JR_EQF", total, right);
                        memorize_reference("NE_FLOAT");
                    }
                }
                else if (s.NextIsSPECIAL("<"))
                {
                    s.GetSym();
                    if (total.StringType) s.ThrowParseError("need number on left side of '<'");
                    Expression right = parse_additive_expression();
                    if (right.StringType) s.ThrowParseError("need number on right side of '<'");
                    total = new ComparisonExpression("CALL LT", "JR_LTF", "JR_GTEQF", total, right);
                    memorize_reference("LT");
                }
                else if (s.NextIsSPECIAL(">"))
                {
                    s.GetSym();
                    if (total.StringType) s.ThrowParseError("need number on left side of '>'");
                    Expression right = parse_additive_expression();
                    if (right.StringType) s.ThrowParseError("need number on right side of '>'");
                    total = new ComparisonExpression("CALL GT", "JR_GTF", "JR_LTEQF", total, right);
                    memorize_reference("GT");
                }
                else if (s.NextIsSPECIAL("<="))
                {
                    s.GetSym();
                    if (total.StringType) s.ThrowParseError("need number on left side of '<='");
                    Expression right = parse_additive_expression();
                    if (right.StringType) s.ThrowParseError("need number on right side of '<='");
                    total = new ComparisonExpression("CALL LE", "JR_LTEF", "JR_GTF", total, right);
                    memorize_reference("LE");
                }
                else if (s.NextIsSPECIAL(">="))
                {
                    s.GetSym();
                    if (total.StringType) s.ThrowParseError("need number on left side of '>='");
                    Expression right = parse_additive_expression();
                    if (right.StringType) s.ThrowParseError("need number on right side of '>='");
                    total = new ComparisonExpression("CALL GE", "JR_GTEQF", "JR_LTF", total, right);
                    memorize_reference("GE");
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

                    if (total.StringType)
                    {
                        if (!right.StringType)
                        {
                            right = new CallExpression(true, "STRINGS VALUE_FORMATTED", right, new AtomicExpression(true, "'%g'"), new AtomicExpression(false, "99"));
                        }
                        total = new CallExpression(true, "CALL TEXT.APPEND", total, right);
                        memorize_reference("TEXT.APPEND");
                    }
                    else
                    {
                        if (right.StringType)
                        {
                            total = new CallExpression(true, "STRINGS VALUE_FORMATTED", total, new AtomicExpression(true, "'%g'"), new AtomicExpression(false, "99"));
                            total = new CallExpression(true, "CALL TEXT.APPEND", total, right);
                            memorize_reference("TEXT.APPEND");
                        }
                        else
                        {
                            total = new CallExpression(false, "ADDF", total, right);
                        }
                    }
                }

                else if (s.NextIsSPECIAL("-"))
                {
                    s.GetSym();
                    if (total.StringType) s.ThrowParseError("need number on left side of '-'");
                    Expression right = parse_multiplicative_expression();
                    if (right.StringType) s.ThrowParseError("need number on right side of '-'");
                    total = new CallExpression(false, "SUBF", total, right);
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
                    if (total.StringType) s.ThrowParseError("need number on left side of '*'");
                    Expression right = parse_unary_minus_expression();
                    if (right.StringType) s.ThrowParseError("need number on right side of '*'");
                    total = new CallExpression(false, "MULF", total, right);
                }
                else if (s.NextIsSPECIAL("/"))
                {
                    s.GetSym();
                    if (total.StringType) s.ThrowParseError("need number on left side of '/'");
                    Expression right = parse_unary_minus_expression();
                    if (right.StringType) s.ThrowParseError("need number on right side of '/'");
                    if (nodivisioncheck)
                    {
                        total = new CallExpression(false, "DIVF", total, right);
                    }
                    else
                    {
                        total = new CallExpression(false, "CALL DIV", total, right);
                    }
                    memorize_reference("DIV");
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
                if (e.StringType) s.ThrowParseError("need number after '-'");

                // check if can directly evaluate expression
                if (e is AtomicExpression)
                {
                    AtomicExpression ae = (AtomicExpression) e;
                    if (ae.var_or_value.StartsWith("CM"))
                    {
                        return new AtomicExpression(false,"C"+ae.var_or_value.Substring(2));
                    }
                    else if (ae.var_or_value.StartsWith("C"))
                    {
                        String val = "-"+(ae.var_or_value.Substring(1).Replace('_', '.'));
                        String constname = (String)constants[val];
                        if (constname == null)
                        {
                            constname = "C" + val.Replace('.', '_').Replace('-','M');
                            constants[val] = constname;
                        }
                        return new AtomicExpression(false, constname);
                    }
                }

                return new CallExpression(false, "MATH NEGATE", e);
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
                    throw new Exception("Text is longer than 251 letters");
                }
                s.GetSym();
                return new AtomicExpression(true, "'" + EscapeString(val) + "'");
            }
            else if (s.NextType == SymType.NUMBER)
            {
                String val = s.NextContent;

                String constname = (String)constants[val];
                if (constname == null)
                {
                    constname = "C" + val.Replace('.', '_');
                    constants[val] = constname;
                }

                s.GetSym();
                return new AtomicExpression(false, constname);
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
                    String vartype = (String)variables[varname];
                    if (vartype == null)
                    {
                        s.ThrowParseError("can not use array " + var_or_object + " before first assignment");
                    }
                    if (!vartype.StartsWith("["))
                    {
                        s.ThrowParseError("can not use normal variable "+var_or_object+" with an index");
                    }
                    parse_special("[");
                    Expression e = parse_float_expression("only numbers are allowed as array index");
                    parse_special("]");

                    if (vartype.Equals("[S"))
                    {
                        memorize_reference("ARRAYGET_STRING");
                        return new CallExpression(true, "CALL ARRAYGET_STRING:"+varname, e);
                    }
                    else
                    {
                        if (noboundscheck)
                        {
                            return new UnsafeArrayGetExpression(varname, e);
                        }
                        else
                        {
                            memorize_reference("ARRAYGET_FLOAT");
                            return new CallExpression(false, "CALL ARRAYGET_FLOAT:" + varname, e);
                        }
                    }
                }
                else
                {
                    // is variable use
                    String varname = "V" + var_or_object;
                    String vartype = (String)variables[varname];
                    if (vartype == null)
                    {
                        s.ThrowParseError("can not use variable " + var_or_object + " before first assignment");
                    }
                    if (vartype.StartsWith("["))
                    {
                        s.ThrowParseError("can not use array " + var_or_object + " like a normal value");
                    }
                    return new AtomicExpression(vartype.Equals("S"), varname);
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
            if (libentry.VoidReturnType)
            {
                s.ThrowParseError("Can not use function that returns nothing in an expression");
            }

            List<Expression> list = new List<Expression>();     // parameter list
            if (s.NextIsSPECIAL("("))
            {   // a function call
                parse_special("(");

                while (list.Count < libentry.StringParamTypes.Length)
                {
                    Expression e = libentry.StringParamTypes[list.Count] ? parse_string_expression() : parse_float_expression("Need a number as parameter here");
                    list.Add(e);

                    if (s.NextIsSPECIAL(","))     // skip optional ',' after each parameter
                    {
                        s.GetSym();
                    }
                }
                parse_special(")");

                if (list.Count < libentry.StringParamTypes.Length)
                {
                    s.ThrowParseError("Too few arguments to " + functionname);
                }
            }
            else
            {   // a property reference
                if (libentry.StringParamTypes.Length != 0)
                {
                    s.ThrowParseError("Can not reference " + functionname+" as a property");
                }
            }


            memorize_reference(functionname);
            return new CallExpression(libentry.StringReturnType, "CALL " + functionname, list);
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
