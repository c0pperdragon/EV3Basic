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
        public abstract String Generate(TextWriter target, int reservedfloats, int reservedstrings, ref int maxfloats, ref int maxstrings);

        // generate code for the expression with a chosen output position. 
        public abstract void Generate(TextWriter target, String outputvar, int reservedfloats, int reservedstrings, ref int maxfloats, ref int maxstrings);


        public virtual void GenerateJumpIfNotTrue(TextWriter target, String jumplabel, ref int maxfloats, ref int maxstrings)
        {
            if (!StringType)
            {
                throw new Exception("Internal error: Try to generate jump for FLOAT condition");
            }
             Generate(target, "S0", 0, 1, ref maxfloats, ref maxstrings);
            target.WriteLine("    AND8888_32 S0 -538976289 S0");    // AND 0xdfdfdfdf performs an upcase for 4 letters
            target.WriteLine("    STRINGS COMPARE S0 'TRUE' S0");
            target.WriteLine("    JR_EQ8 S0 0 " + jumplabel);
        }
    }

    class AtomicExpression : Expression
    {
        String var_or_value;

        public AtomicExpression(bool stringType, String v)
            : base(stringType)
        {
            var_or_value = v;
        }

        override public String Generate(TextWriter target, int reservedfloats, int reservedstrings, ref int maxfloats, ref int maxstrings)
        {
            return var_or_value;
        }

        override public void Generate(TextWriter target, String outputvar, int reservedfloats, int reservedstrings, ref int maxfloats, ref int maxstrings)
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

        override public String Generate(TextWriter target, int reservedfloats, int reservedstrings, ref int maxfloats, ref int maxstrings)
        {
            String outputvar = null;
            if (StringType)
            {
                outputvar = "S" + reservedstrings;
                if (reservedstrings >= maxstrings)
                {
                    maxstrings = reservedstrings + 1;
                }
            }
            else
            {
                outputvar = "F" + reservedfloats;
                if (reservedfloats >= maxfloats)
                {
                    maxfloats = reservedfloats + 1;
                }
            }
            Generate(target, outputvar, reservedfloats, reservedstrings, ref maxfloats, ref maxstrings);
            return outputvar;
        }

        override public void Generate(TextWriter target, String outputvar, int reservedfloats, int reservedstrings, ref int maxfloats, ref int maxstrings)
        {
            List<String> outputs = new List<String>();

            int rf = reservedfloats;
            int rs = reservedstrings;
            for (int i = 0; i < parameters.Length; i++)
            {
                String outvar = parameters[i].Generate(target, rf, rs, ref maxfloats, ref maxstrings);
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

            target.Write("    " + function);
            foreach (String p in outputs)
            {
                target.Write(" " + p);
            }
            if (outputvar != null)
            {
                target.Write(" " + outputvar);
            }
            target.WriteLine();
        }
    }

    class ComparisonExpression : CallExpression
    {
        String alternativejumpifnottrue;

        public ComparisonExpression(String function, String alternativejumpifnottrue, Expression par1, Expression par2)
            : base(true, function, par1, par2)
        {
            this.alternativejumpifnottrue = alternativejumpifnottrue;
        }

        public override void GenerateJumpIfNotTrue(TextWriter target, String jumplabel, ref int maxfloats, ref int maxstrings)
        {
            if (parameters[0].StringType || parameters[1].StringType)
            {
                throw new Exception("Internal error: Found subexpressions with STRING type inside ComparisonExpression");
            }

            String outvar1 = parameters[0].Generate(target, 0, 0, ref maxfloats, ref maxstrings);
            int reserved = outvar1.StartsWith("F") ? 1 : 0;
            String outvar2 = parameters[1].Generate(target, reserved, 0, ref maxfloats, ref maxstrings);
            target.WriteLine("    " + alternativejumpifnottrue + " " + outvar1 + " " + outvar2 + " " + jumplabel);
        }
    }

}
