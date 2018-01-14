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
    // data types for variables and expressions
    public enum ExpressionType
    {
        Number,            // floating pointer number
        Text,              // character string
        NumberArray,       // array of floating point numbers
        TextArray,         // array of strings
        Void,              // no data  (only for return type of functions)
    };


    abstract class Expression
    {
        public readonly ExpressionType type;

        public Expression(ExpressionType type)
        {
            this.type = type;
        }

        // every expression that does not need to compute anything (variable reference, constants), 
        // will return the position where this calue can be taken from for further actions.
        // when null is returned, a computation needs to be generated instead.
        public virtual String PreparedValue()
        {
            return null;
        }

        // generate code for the expression with a chosen output position. 
        // the default-implementation can only take a prepared value and copy it to the
        // output variable
        public virtual void Generate(Compiler compiler, TextWriter target, String outputvar)
        {
            String v = PreparedValue();
            if (v == null)
            {
                throw new Exception("Internal error: no implementation to compute this exception");
            }
            switch (type)
            {
                case ExpressionType.Number:
                    target.WriteLine("    MOVEF_F " + v + " " + outputvar);
                    break;
                case ExpressionType.Text:
                    target.WriteLine("    STRINGS DUPLICATE " + v + " " + outputvar);
                    break;
                case ExpressionType.NumberArray:
                case ExpressionType.TextArray:
                    target.WriteLine("    ARRAY COPY " + v + " " + outputvar);
                    break;
            }
        }

        public virtual void GenerateJumpIfCondition(Compiler compiler, TextWriter target, String jumplabel, bool jumpIfTrue)
        {
            if (type!=ExpressionType.Text)
            {
                throw new Exception("Internal error: Try to generate jump for non text condition type");
            }
            String v = compiler.reserveVariable(ExpressionType.Text);
            Generate(compiler, target, v);
            target.WriteLine("    AND8888_32 "+v+" -538976289 "+v);    // AND 0xdfdfdfdf performs an upcase for 4 letters
            target.WriteLine("    STRINGS COMPARE "+v+" 'TRUE' "+v);
            target.WriteLine("    " + (jumpIfTrue?"JR_NEQ8":"JR_EQ8") +" "+v+" 0 " + jumplabel);
            compiler.releaseVariable(ExpressionType.Text);
        }

        public virtual bool IsPositive()
        {
            return false;
        }
        public virtual bool IsNegative()
        {
            return false;
        }
    }

    class NumberExpression : Expression
    {
        public readonly double value;


        public NumberExpression(double value) 
        : base(ExpressionType.Number)
        {
            this.value = value;
        }

        override public String PreparedValue()
        {
            String s = value.ToString(CultureInfo.InvariantCulture);
            if (s.IndexOf('.') >= 0)
            {
                return s;
            }
            else
            {
                return s + ".0";
            }
        }

        override public bool IsPositive()
        {
            return value > 0;
        }
        override public bool IsNegative()
        {
            return value < 0;
        }
    }

    class AtomicExpression : Expression
    {
        public readonly String var_or_string;

        public AtomicExpression(ExpressionType type, String v) 
        : base(type)
        {
            var_or_string = v;
        }

        override public String PreparedValue()
        {
            return var_or_string;
        }

        override public void GenerateJumpIfCondition(Compiler compiler, TextWriter target, String jumplabel, bool jumpIfTrue)
        {
            if (type==ExpressionType.Text && var_or_string.StartsWith("'"))
            {
                bool isTrue = var_or_string.Equals("'TRUE'", StringComparison.InvariantCultureIgnoreCase);
                if (jumpIfTrue == isTrue)
                {
                    target.WriteLine("    JR " + jumplabel);
                }
                return;
            }
            base.GenerateJumpIfCondition(compiler, target, jumplabel, jumpIfTrue);
        }

    }

    class CallExpression : Expression
    {
        String function;
        protected Expression[] parameters;

        public CallExpression(ExpressionType type, String function, Expression par1) 
        : base(type)
        {
            this.function = function;
            this.parameters = new Expression[] { par1 };
        }

        public CallExpression(ExpressionType type, String function, Expression par1, Expression par2)
        : base(type)
        {
            this.function = function;
            this.parameters = new Expression[] { par1, par2 };
        }

        public CallExpression(ExpressionType type, String function, Expression par1, Expression par2, Expression par3)
        : base(type)
        {
            this.function = function;
            this.parameters = new Expression[] { par1, par2, par3 };
        }

        public CallExpression(ExpressionType type, String function, List<Expression> parlist)
        : base(type)
        {
            this.function = function;
            this.parameters = parlist.ToArray();
        }

        override public void Generate(Compiler compiler, TextWriter target, String outputvar)
        {
            List<String> arguments = new List<String>();
            List<ExpressionType> releases = new List<ExpressionType>();

            for (int i = 0; i < parameters.Length; i++)
            {
                Expression p = parameters[i];
                String arg = p.PreparedValue();
                if (arg==null)
                {
                    arg = compiler.reserveVariable(p.type);
                    releases.Add(p.type);
                    p.Generate(compiler, target, arg);
                }
                arguments.Add(arg);
            }
            String tmpoutput = null;
            if (outputvar != null)
            {
                arguments.Add(outputvar);                
            }

            // build a function call with properly injected arguments (where this is needed)
            target.Write("    " + InjectPlaceholders(function, arguments, compiler.GetLabelNumber()));
            // arguments that were not explicitly consumed, are just attached at the end
            foreach (String p in arguments)
            {
                if (p != null)
                {
                    target.Write(" " + p);
                }
            }
            target.WriteLine();

            // when a temporary output was used, copy the result to true output
            if (tmpoutput!=null)
            {
                target.WriteLine("    ARRAY COPY " + tmpoutput + " " + outputvar);
            }
            // release temporary variables
            foreach (ExpressionType et in releases)
            {
                compiler.releaseVariable(et);
            }

            // check if contains calls of a library method
            int idx = 0;
            for (;;)
            {
                idx = function.IndexOf("CALL ", idx);
                if (idx<0)
                {
                    break;
                }
                if (idx==0 || function[idx-1]==' ' || function[idx-1]=='\t')
                {
                    String n = function.Substring(idx+5).Trim();
                    int space = n.IndexOf(' ');
                    if (space >= 0)
                    {
                        n = n.Substring(0, space).Trim();
                    }
                    compiler.memorize_reference(n);
                }
                idx++;
            }
        }

        private static String InjectPlaceholders(String format, List<String>par, int expansioncounter)
        {
            List<int> uses = new List<int>();
            int cursor = 0;
            while (cursor < format.Length)
            {
                // look for the next occurence of ":" that denotes a replacement 
                int idx = format.IndexOf(':', cursor);
                if (idx < 0)   // no more placeholders to find
                {
                    break;
                }
                if (idx + 1 < format.Length)  // do not reach boyond a trailing ':'
                {
                    if (format[idx + 1] == '#')     // macro expansion needs a unique identifier here
                    {
                        String str = "" + expansioncounter;
                        format = format.Substring(0, idx) + str + format.Substring(idx + 2);
                        cursor = cursor + str.Length;
                        continue;
                    }
                    else if (format[idx + 1] >= '0' && format[idx + 1] <= '9')
                    {
                        // get the character after the ':' to know which parameter to take
                        int pnum = format[idx + 1] - '0';
                        // insert the value of the parameter. whenever something is amiss, throw exceptions
                        format = format.Substring(0, idx) + par[pnum] + format.Substring(idx + 2);
                        // skip the parameter that was just put in place
                        cursor = cursor + par[pnum].Length;
                        // memorize that his parameter was used 
                        uses.Add(pnum);
                        continue;
                    }
                }
                cursor = idx+1;  // continue searching
            }

            // remove the used parameters from the list, so it will not be auto-appended
            foreach (int pnum in uses)
            {
                par[pnum] = null;
            }

            return format;
        }
    }

    class ComparisonExpression : CallExpression
    {
        String alternativejumpiftrue;
        String alternativejumpifnottrue;

        public ComparisonExpression(String function, String alternativejumpiftrue, String alternativejumpifnottrue, Expression par1, Expression par2)
        : base(ExpressionType.Text, function, par1, par2)
        {
            this.alternativejumpiftrue = alternativejumpiftrue;
            this.alternativejumpifnottrue = alternativejumpifnottrue;
        }

        public override void GenerateJumpIfCondition(Compiler compiler, TextWriter target, String jumplabel, bool jumpIfTrue)
        {
            if (parameters[0].type != ExpressionType.Number || parameters[1].type != ExpressionType.Number)
            {
                throw new Exception("Internal error: Found subexpressions with non-number type inside ComparisonExpression");
            }

            int numrelease = 0;
            String v1 = parameters[0].PreparedValue();
            if (v1==null)
            {   v1 = compiler.reserveVariable(ExpressionType.Number);
                numrelease++;
                parameters[0].Generate(compiler,target,v1);
            }
            String v2 = parameters[1].PreparedValue();
            bool mustreleasev2 = (v2 == null);
            if (v2==null)
            {
                v2 = compiler.reserveVariable(ExpressionType.Number);
                numrelease++;
                parameters[1].Generate(compiler, target, v2);
            }

            target.WriteLine("    " + 
                (jumpIfTrue ? alternativejumpiftrue : alternativejumpifnottrue)
                + " " + v1 + " " + v2 + " " + jumplabel);

            for (int i = 0; i < numrelease; i++ )
            {
                compiler.releaseVariable(ExpressionType.Number);
            }
        }
    }

    class AndExpression : CallExpression
    {
        public AndExpression(Expression par1, Expression par2) 
        : base(ExpressionType.Text, "CALL AND", par1, par2)
        {}

        public override void GenerateJumpIfCondition(Compiler compiler, TextWriter target, String jumplabel, bool jumpIfTrue)
        {
            if (jumpIfTrue)
            {
                int l = compiler.GetLabelNumber();
                parameters[0].GenerateJumpIfCondition(compiler, target, "and"+l, false);
                parameters[1].GenerateJumpIfCondition(compiler, target, jumplabel, true);
                target.WriteLine("  and" +l +":");
            }
            else
            {
                parameters[0].GenerateJumpIfCondition(compiler, target, jumplabel, false);
                parameters[1].GenerateJumpIfCondition(compiler, target, jumplabel, false);
            }
        }
    }

    class OrExpression : CallExpression
    {
        public OrExpression(Expression par1, Expression par2)
        : base(ExpressionType.Text, "CALL OR", par1, par2)
        { }

        public override void GenerateJumpIfCondition(Compiler compiler, TextWriter target, String jumplabel, bool jumpIfTrue)
        {
            if (jumpIfTrue)
            {
                parameters[0].GenerateJumpIfCondition(compiler, target, jumplabel, true);
                parameters[1].GenerateJumpIfCondition(compiler, target, jumplabel, true);
            }
            else
            {
                int l = compiler.GetLabelNumber();
                parameters[0].GenerateJumpIfCondition(compiler, target, "or"+l, true);
                parameters[1].GenerateJumpIfCondition(compiler, target, jumplabel, false);
                target.WriteLine("  or" + l + ":");
            }
        }
    }

    class UnsafeArrayGetExpression : Expression
    {
        String variablename;
        Expression index;

        public UnsafeArrayGetExpression(String variablename, Expression index) 
        : base (ExpressionType.Number)
        {
            this.variablename = variablename;
            this.index = index;
        }

        override public void Generate(Compiler compiler, TextWriter target, String outputvar)
        {
            if (index.type != ExpressionType.Number)
            {
                throw new Exception("Internal error: non-number type expression for array index");
            }
            if (index is NumberExpression)
            {   // check if index is known at compile-time
                int idxvalue = (int) (((NumberExpression)index).value);
                if (idxvalue<0)  // impossible index
                {
                    target.WriteLine("    MOVEF_F 0.0 " + outputvar);
                }
                else
                {
                    target.WriteLine("    ARRAY_READ " + variablename + " " + idxvalue + " " + outputvar);
                }
            }
            else
            {   // must evaluate index at run-time 
                String pv = index.PreparedValue();
                if (pv != null)
                {   // index is already available in variable or constant
                    target.WriteLine("    MOVEF_32 " + pv + " INDEX");
                    target.WriteLine("    ARRAY_READ " + variablename + " INDEX " + outputvar);
                }
                else
                {   // must first compute index (may use outputvar as temporary storage)
                    index.Generate(compiler, target, outputvar);
                    target.WriteLine("    MOVEF_32 " + outputvar + " INDEX");
                    target.WriteLine("    ARRAY_READ " + variablename + " INDEX " + outputvar);
                }
            }
        }
    }

    class FunctionExpression : Expression
    {
        FunctionDefinition fd;
        protected Expression[] parameters;

        public FunctionExpression(FunctionDefinition fd, List<Expression> parlist)
        : base(fd.getReturnType())
        {
            this.fd = fd;
            this.parameters = parlist.ToArray();
        }

        override public void Generate(Compiler compiler, TextWriter target, String outputvar)
        {
            // if necessary (possible recursive call), store all locals (including temporaries)
            FunctionDefinition cf = compiler.getCurrentFunction();
            bool dosave = compiler.functionCouldCall(fd,cf);
            if (dosave)
            {
                foreach (String n in cf.getCurrentLocalVariables(ExpressionType.Number))
                {   
                    target.WriteLine("    CALL ARRAYSTORE_FLOAT NUMBERSTACKSIZE "+n+" NUMBERSTACKHANDLE");
                    target.WriteLine("    ADDF NUMBERSTACKSIZE 1.0 NUMBERSTACKSIZE");
                    compiler.memorize_reference("ARRAYSTORE_FLOAT");
                }
                foreach (String n in cf.getCurrentLocalVariables(ExpressionType.Text))
                {   
                    target.WriteLine("    CALL ARRAYSTORE_STRING STRINGSTACKSIZE "+n+" STRINGSTACKHANDLE");
                    target.WriteLine("    ADDF STRINGSTACKSIZE 1.0 STRINGSTACKSIZE");
                    compiler.memorize_reference("ARRAYSTORE_STRING");
                }
            }

            // compute call parameters in temporary variables
            String[] tmpvar = new String[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                tmpvar[i] = compiler.reserveVariable(parameters[i].type);
                parameters[i].Generate(compiler, target, tmpvar[i]);
            }
            // set up target call parameters and the defaults for all non-provided local variables
            for (int i = 0; i < fd.getParameterNumber(); i++)
            {
                String pv = fd.getParameterVariable(i);
                if (fd.getParameterType(i) == ExpressionType.Number)
                {
                    target.WriteLine("    MOVEF_F " + (i<tmpvar.Length ? tmpvar[i] : fd.getParameterDefaultLiteral(i)) + " " +pv);
                }
                else if (fd.getParameterType(i) == ExpressionType.Text)
                {
                    target.WriteLine("    STRINGS DUPLICATE " + (i<tmpvar.Length ? tmpvar[i] : fd.getParameterDefaultLiteral(i)) + " " + pv);
                }
            }
            // release temporary variables
            for (int i = parameters.Length - 1; i >= 0; i--)
            {
                compiler.releaseVariable(parameters[i].type);
            }

            // perform the call
            String subid = fd.startsub;
            String returnlabel = "CALLSUB" + (compiler.GetLabelNumber());
            target.WriteLine("    WRITE32 ENDSUB_" + subid + ":" + returnlabel + " STACKPOINTER RETURNSTACK");
            target.WriteLine("    ADD8 STACKPOINTER 1 STACKPOINTER");
            target.WriteLine("    JR SUB_" + subid);
            target.WriteLine(returnlabel + ":");

            // read back the stored values (when it is a possible recursive call)
            if (dosave)
            {
                List<String> vlist = cf.getCurrentLocalVariables(ExpressionType.Number);
                vlist.Reverse();
                foreach (String n in vlist)
                {   
                    target.WriteLine("    SUBF NUMBERSTACKSIZE 1.0 NUMBERSTACKSIZE");
                    target.WriteLine("    CALL ARRAYGET_FLOAT NUMBERSTACKSIZE "+n+" NUMBERSTACKHANDLE");
                    compiler.memorize_reference("ARRAYGET_FLOAT");
                }
                vlist = cf.getCurrentLocalVariables(ExpressionType.Text);
                vlist.Reverse();
                foreach (String n in vlist)
                {   
                    target.WriteLine("    SUBF STRINGSTACKSIZE 1.0 STRINGSTACKSIZE");
                    target.WriteLine("    CALL ARRAYGET_STRING STRINGSTACKSIZE "+n+" STRINGSTACKHANDLE");
                    compiler.memorize_reference("ARRAYGET_STRING");
                }
            }

            // read out the result value if required
            if (outputvar!=null)
            {
                switch (fd.getReturnType())
                {   
                    case ExpressionType.Number:
                        target.WriteLine("    MOVEF_F " + fd.getReturnVariable() + " " + outputvar);
                        break;
                    case ExpressionType.Text:
                        target.WriteLine("    STRINGS DUPLICATE " + fd.getReturnVariable() + " " + outputvar);
                        break;
                }
            }

        }
    }
}
