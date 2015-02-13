using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace EV3BasicCompiler
{
    abstract class Expression
    {
        public readonly bool StringType;
        public Expression(bool stringType)
        {
            this.StringType = stringType;
        }

        // generate code for the expression. the output will be stored in a variable chosen by the expression   
        // (or the constant string itself will be returned)
        public abstract String Generate(Compiler compiler, TextWriter target, int reservedfloats, int reservedstrings);

        // generate code for the expression with a chosen output position. 
        public abstract void Generate(Compiler compiler, TextWriter target, String outputvar, int reservedfloats, int reservedstrings);

        public virtual void GenerateJumpIfTrue(Compiler compiler, TextWriter target, String jumplabel)
        {
            if (!StringType)
            {
                throw new Exception("Internal error: Try to generate jump for FLOAT condition");
            }
            Generate(compiler, target, "S0", 0, 1);
            target.WriteLine("    AND8888_32 S0 -538976289 S0");    // AND 0xdfdfdfdf performs an upcase for 4 letters
            target.WriteLine("    STRINGS COMPARE S0 'TRUE' S0");
            target.WriteLine("    JR_NEQ8 S0 0 " + jumplabel);
        }

        public virtual void GenerateJumpIfNotTrue(Compiler compiler, TextWriter target, String jumplabel)
        {
            if (!StringType)
            {
                throw new Exception("Internal error: Try to generate jump for FLOAT condition");
            }
            Generate(compiler, target, "S0", 0, 1);
            target.WriteLine("    AND8888_32 S0 -538976289 S0");    // AND 0xdfdfdfdf performs an upcase for 4 letters
            target.WriteLine("    STRINGS COMPARE S0 'TRUE' S0");
            target.WriteLine("    JR_EQ8 S0 0 " + jumplabel);
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

    class AtomicExpression : Expression
    {
        public readonly String var_or_value;

        public AtomicExpression(bool stringType, String v)
            : base(stringType)
        {
            var_or_value = v;
        }

        override public String Generate(Compiler compiler, TextWriter target, int reservedfloats, int reservedstrings)
        {
            return var_or_value;
        }

        override public void Generate(Compiler compiler, TextWriter target, String outputvar, int reservedfloats, int reservedstrings)
        {
            if (StringType)
            {
                target.WriteLine("    STRINGS DUPLICATE " + var_or_value + " " + outputvar);
            }
            else
            {
                target.WriteLine("    MOVEF_F " + var_or_value + " " + outputvar);
            }
        }
        override public bool IsPositive()
        {
            return var_or_value.StartsWith("C") && !var_or_value.StartsWith("CM");
        }
        override public bool IsNegative()
        {
            return var_or_value.StartsWith("CM");
        }
    }

    class CallExpression : Expression
    {
        String function;
        protected Expression[] parameters;

        public CallExpression(bool stringType, String function, Expression par1)
            : base(stringType)
        {
            this.function = function;
            this.parameters = new Expression[] { par1 };
        }

        public CallExpression(bool stringType, String function, Expression par1, Expression par2)
            : base(stringType)
        {
            this.function = function;
            this.parameters = new Expression[] { par1, par2 };
        }

        public CallExpression(bool stringType, String function, Expression par1, Expression par2, Expression par3)
            : base(stringType)
        {
            this.function = function;
            this.parameters = new Expression[] { par1, par2, par3 };
        }

        public CallExpression(bool stringType, String function, List<Expression> parlist)
            : base(stringType)
        {
            this.function = function;
            this.parameters = parlist.ToArray();
        }

        override public String Generate(Compiler compiler, TextWriter target, int reservedfloats, int reservedstrings)
        {
            String outputvar = null;
            if (StringType)
            {
                outputvar = "S" + reservedstrings;
                if (reservedstrings >= compiler.maxstrings)
                {
                    compiler.maxstrings = reservedstrings + 1;
                }
            }
            else
            {
                outputvar = "F" + reservedfloats;
                if (reservedfloats >= compiler.maxfloats)
                {
                    compiler.maxfloats = reservedfloats + 1;
                }
            }
            Generate(compiler, target, outputvar, reservedfloats, reservedstrings);
            return outputvar;
        }

        override public void Generate(Compiler compiler, TextWriter target, String outputvar, int reservedfloats, int reservedstrings)
        {
            List<String> outputs = new List<String>();

            int rf = reservedfloats;
            int rs = reservedstrings;
            for (int i = 0; i < parameters.Length; i++)
            {
                String outvar = parameters[i].Generate(compiler, target, rf, rs);
                outputs.Add(outvar);
                if (outvar.StartsWith("S"))
                {
                    rs++;
                }
                else if (outvar.StartsWith("F"))
                {
                    rf++;
                }
            }

            int suffixdelimiter = function.IndexOf(':');
            if (suffixdelimiter>=0)
            {   target.Write("    " + function.Substring(0,suffixdelimiter));
            }
            else
            {
                target.Write("    " + function);
            }
            foreach (String p in outputs)
            {
                target.Write(" " + p);
            }
            if (outputvar != null)
            {
                target.Write(" " + outputvar);
            }
            if (suffixdelimiter>=0)
            {
                target.Write(" "+function.Substring(suffixdelimiter+1));
            }
            target.WriteLine();
        }
    }

    class ComparisonExpression : CallExpression
    {
        String alternativejumpiftrue;
        String alternativejumpifnottrue;

        public ComparisonExpression(String function, String alternativejumpiftrue, String alternativejumpifnottrue, Expression par1, Expression par2)
            : base(true, function, par1, par2)
        {
            this.alternativejumpiftrue = alternativejumpiftrue;
            this.alternativejumpifnottrue = alternativejumpifnottrue;
        }

        public override void GenerateJumpIfTrue(Compiler compiler, TextWriter target, String jumplabel)
        {
            if (parameters[0].StringType || parameters[1].StringType)
            {
                throw new Exception("Internal error: Found subexpressions with STRING type inside ComparisonExpression");
            }

            String outvar1 = parameters[0].Generate(compiler, target, 0, 0);
            int reserved = outvar1.StartsWith("F") ? 1 : 0;
            String outvar2 = parameters[1].Generate(compiler, target, reserved, 0);
            target.WriteLine("    " + alternativejumpiftrue + " " + outvar1 + " " + outvar2 + " " + jumplabel);
        }

        public override void GenerateJumpIfNotTrue(Compiler compiler, TextWriter target, String jumplabel)
        {
            if (parameters[0].StringType || parameters[1].StringType)
            {
                throw new Exception("Internal error: Found subexpressions with STRING type inside ComparisonExpression");
            }

            String outvar1 = parameters[0].Generate(compiler, target, 0, 0);
            int reserved = outvar1.StartsWith("F") ? 1 : 0;
            String outvar2 = parameters[1].Generate(compiler, target, reserved, 0);
            target.WriteLine("    " + alternativejumpifnottrue + " " + outvar1 + " " + outvar2 + " " + jumplabel);
        }
    }

    class AndExpression : CallExpression
    {
        public AndExpression(Expression par1, Expression par2) : base(true, "CALL AND", par1, par2)
        {}

        public override void GenerateJumpIfTrue(Compiler compiler, TextWriter target, String jumplabel)
        {
            parameters[0].GenerateJumpIfNotTrue(compiler, target, jumplabel+"_AND");
            parameters[1].GenerateJumpIfTrue(compiler, target, jumplabel);
            target.WriteLine("  "+jumplabel + "_AND:");
        }
        public override void GenerateJumpIfNotTrue(Compiler compiler, TextWriter target, String jumplabel)
        {
            parameters[0].GenerateJumpIfNotTrue(compiler, target, jumplabel);
            parameters[1].GenerateJumpIfNotTrue(compiler, target, jumplabel);
        }
    }

    class OrExpression : CallExpression
    {
        public OrExpression(Expression par1, Expression par2)
            : base(true, "CALL OR", par1, par2)
        { }

        public override void GenerateJumpIfTrue(Compiler compiler, TextWriter target, String jumplabel)
        {
            parameters[0].GenerateJumpIfTrue(compiler, target, jumplabel);
            parameters[1].GenerateJumpIfTrue(compiler, target, jumplabel);
        }
        public override void GenerateJumpIfNotTrue(Compiler compiler, TextWriter target, String jumplabel)
        {
            parameters[0].GenerateJumpIfTrue(compiler, target, jumplabel+"_OR");
            parameters[1].GenerateJumpIfNotTrue(compiler, target, jumplabel);
            target.WriteLine("  "+jumplabel + "_OR:");
        }
    }

    class UnsafeArrayGetExpression : Expression
    {
        String variablename;
        Expression index;

        public UnsafeArrayGetExpression(String variablename, Expression index) : base (false)
        {
            this.variablename = variablename;
            this.index = index;
            if (index.StringType)
            {
                throw new Exception("Internal error: string type expression for array index");
            }
        }

        override public String Generate(Compiler compiler, TextWriter target, int reservedfloats, int reservedstrings)
        {
            String var = "F"+reservedfloats;           
            Generate(compiler, target, var, reservedfloats, reservedstrings);
            return var;
        }

        override public void Generate(Compiler compiler, TextWriter target, String outputvar, int reservedfloats, int reservedstrings)
        {
            String indexvar = index.Generate(compiler, target, reservedfloats, reservedstrings);
            target.WriteLine("    MOVEF_32 "+indexvar+" INDEX");
            target.WriteLine("    ARRAY_READ "+variablename+" INDEX "+outputvar);
        }
    }


}
