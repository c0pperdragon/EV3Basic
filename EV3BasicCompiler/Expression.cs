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
                if ((type==ExpressionType.NumberArray || type==ExpressionType.TextArray) && arguments.Contains(outputvar))
                {   // in the case of array operations prevent to have one parameter as output target also.
                    // this could lead to data being overwritten while it is in use. introduce a termporary variable
                    // for such cases
                    tmpoutput = compiler.reserveVariable(type);
                    releases.Add(type);
                    arguments.Add(tmpoutput);
                }
                else
                {
                    arguments.Add(outputvar);
                }
            }

            // build a function call with properly injected arguments (where this is needed)
            target.Write("    " + InjectPlaceholders(function, arguments));
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
        }

        private static String InjectPlaceholders(String format, List<String>par)
        {
            int cursor = 0;
            while (cursor < format.Length)
            {
                // look for the next occurence of ":" that denotes a replacement 
                int idx = format.IndexOf(':', cursor);
                if (idx < 0)
                {
                    return format;
                }
                // get the character after the ':' to know which parameter to take
                int pnum = format[idx + 1] - '0';
                // insert the value of the parameter. whenever something is amiss, throw exceptions
                format = format.Substring(0, idx) + par[pnum] + format.Substring(idx + 2);
                // skip the parameter that was just put in place
                cursor = cursor + par[pnum].Length;
                // remove the parameter so it will not be used twice
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
                parameters[0].GenerateJumpIfCondition(compiler, target, jumplabel + "_AND", false);
                parameters[1].GenerateJumpIfCondition(compiler, target, jumplabel, true);
                target.WriteLine("  " + jumplabel + "_AND:");
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
                parameters[0].GenerateJumpIfCondition(compiler, target, jumplabel + "_OR", true);
                parameters[1].GenerateJumpIfCondition(compiler, target, jumplabel, false);
                target.WriteLine("  " + jumplabel + "_OR:");
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


}
