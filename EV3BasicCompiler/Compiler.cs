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

        // data used during one compilation run
        Scanner s;
        int labelcount;
        Dictionary<String,ExpressionType> variables; 
        Dictionary<ExpressionType, int> reservedtemporaries;
        Dictionary<ExpressionType, int> maxreservedtemporaries;
        HashSet<LibraryEntry> references;

        bool noboundscheck;
        bool nodivisioncheck;

        public Compiler()
        {
            library = new Hashtable();

            variables = new Dictionary<String, ExpressionType>();
            references = new HashSet<LibraryEntry>();
            reservedtemporaries = new Dictionary<ExpressionType, int>();
            maxreservedtemporaries = new Dictionary<ExpressionType, int>();

            readLibrary();
        }

        private void readLibrary()
        {
            runtimeglobals = "";
            readLibraryModule(EV3BasicCompiler.Properties.Resources.runtimelibrary);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Assert);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Buttons);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.EV3);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.LCD);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Math);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Motor);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Program);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Sensor);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Text);
            readLibraryModule(EV3BasicCompiler.Properties.Resources.Vector);
        }

        private void readLibraryModule(String moduletext)
        {
            StringReader reader = new StringReader(moduletext);
            
            String currentfirstline = null;
            StringBuilder body = new StringBuilder();
            StringBuilder globals = new StringBuilder();

            String line;
            while ((line = reader.ReadLine()) != null)
            {
                if (currentfirstline == null)
                {
                    int idx = line.IndexOf("subcall");
                    if (idx== 0)
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
                    if (line.IndexOf("}") == 0)
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

            runtimeglobals = runtimeglobals + globals.ToString();
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
            target.WriteLine("DATA32 INDEX");

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

            for (int i = 0; maxreservedtemporaries.ContainsKey(ExpressionType.Number) && i<maxreservedtemporaries[ExpressionType.Number]; i++)
            {
                target.WriteLine("DATAF F" + i);
            }
            for (int i = 0; maxreservedtemporaries.ContainsKey(ExpressionType.Text) && i < maxreservedtemporaries[ExpressionType.Text]; i++)
            {
                target.WriteLine("DATAS S" + i + " 252");
            }
            for (int i = 0; maxreservedtemporaries.ContainsKey(ExpressionType.NumberArray) && i < maxreservedtemporaries[ExpressionType.NumberArray]; i++)
            {
                target.WriteLine("ARRAY16 A" + i + " 2");
                initlist.WriteLine("    CALL ARRAYCREATE_FLOAT A" + i);
            }
            for (int i = 0; maxreservedtemporaries.ContainsKey(ExpressionType.TextArray) && i < maxreservedtemporaries[ExpressionType.TextArray]; i++)
            {
                target.WriteLine("ARRAY16 X" + i + " 2");
                initlist.WriteLine("    CALL ARRAYCREATE_STRING X" + i);
            }
            target.WriteLine();

            target.WriteLine("vmthread MAIN");
            target.WriteLine("{");
            target.Write(initlist.ToString());
            
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
                case ExpressionType.NumberArray: 
                    return "A" + (n - 1);
                case ExpressionType.TextArray: 
                    return "X" + (n - 1);
                default:
                    throw new Exception("Can not allocate temporary variable of type " + type);
            }
        }
        public void releaseVariable(ExpressionType type)
        {
            reservedtemporaries[type]--;
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
            int l = labelcount++;

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
            int l = labelcount++;

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
                memorize_reference("LE_STEP");
            }
            else
            {
                testexpression = new ComparisonExpression("CALL LE", "JR_LTEQF", "JR_GTF", 
                    new AtomicExpression(ExpressionType.Number, varname), stopexpression);
                incexpression = new CallExpression(ExpressionType.Number, "ADDF", 
                    new AtomicExpression(ExpressionType.Number, varname), new NumberExpression(1.0));
                memorize_reference("LE");
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
                memorize_reference("ARRAYSTORE_STRING");
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
                    memorize_reference("ARRAYSTORE_FLOAT");
                    Expression aex = new CallExpression(ExpressionType.Void, "CALL ARRAYSTORE_FLOAT :0 :1 " + varname, eidx, e);
                    aex.Generate(this, target, null);
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

            Expression ex = new CallExpression(ExpressionType.Void, "CALL " + functionname, list);
            if (libentry.returnType==ExpressionType.Void)
            {   ex.Generate(this, target, null);
            }
            else
            {
                String retvar = reserveVariable(libentry.returnType);
                ex.Generate(this,target,retvar);
                releaseVariable(libentry.returnType);
            }

            memorize_reference(functionname);
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

                    if (total.type!=ExpressionType.Text) s.ThrowParseError("need text on left side of AND");
                    Expression right = parse_comparative_expression();
                    if (right.type!=ExpressionType.Text) s.ThrowParseError("need text on right side of AND");
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
                    memorize_reference("LT");
                }
                else if (s.NextIsSPECIAL(">"))
                {
                    s.GetSym();
                    if (total.type!=ExpressionType.Number) s.ThrowParseError("need number on left side of '>'");
                    Expression right = parse_additive_expression();
                    if (right.type != ExpressionType.Number) s.ThrowParseError("need number on right side of '>'");
                    total = new ComparisonExpression("CALL GT", "JR_GTF", "JR_LTEQF", total, right);
                    memorize_reference("GT");
                }
                else if (s.NextIsSPECIAL("<="))
                {
                    s.GetSym();
                    if (total.type!=ExpressionType.Number) s.ThrowParseError("need number on left side of '<='");
                    Expression right = parse_additive_expression();
                    if (right.type != ExpressionType.Number) s.ThrowParseError("need number on right side of '<='");
                    total = new ComparisonExpression("CALL LE", "JR_LTEF", "JR_GTF", total, right);
                    memorize_reference("LE");
                }
                else if (s.NextIsSPECIAL(">="))
                {
                    s.GetSym();
                    if (total.type != ExpressionType.Number) s.ThrowParseError("need number on left side of '>='");
                    Expression right = parse_additive_expression();
                    if (right.type != ExpressionType.Number) s.ThrowParseError("need number on right side of '>='");
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
                        memorize_reference("TEXT.APPEND");
                    }
                    else if (total.type==ExpressionType.Number)
                    {
                        if (right.type == ExpressionType.Text)
                        {
                            total = new CallExpression(ExpressionType.Text, "STRINGS VALUE_FORMATTED :0 '%g' 99", total);
                            total = new CallExpression(ExpressionType.Text, "CALL TEXT.APPEND", total, right);
                            memorize_reference("TEXT.APPEND");
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
                    Expression right = parse_additive_expression();
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
                            total = new CallExpression(ExpressionType.Number, "CALL DIV", total, right);
                            memorize_reference("DIV");
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
                    throw new Exception("Text is longer than 251 letters");
                }
                s.GetSym();
                return new AtomicExpression(ExpressionType.Text, "'" + EscapeString(val) + "'");
            }
            else if (s.NextType == SymType.NUMBER)
            {
                double val;
                if (!double.TryParse(s.NextContent, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                {
                    throw new Exception("Can not decode number: "+s.NextContent);
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
                        memorize_reference("ARRAYGET_STRING");
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
                            memorize_reference("ARRAYGET_FLOAT");
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

            memorize_reference(functionname);
            return new CallExpression(libentry.returnType, "CALL " + functionname, list);
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
