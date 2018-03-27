/*  EV3-Basic: A basic compiler to target the Lego EV3 brick
    Copyright (C) 2017 Reinhard Grafl

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

using Microsoft.SmallBasic.Library;

namespace SmallBasicEV3Extension
{
    /// <summary>
    /// A framework to create functions with parameters and local variables in Small Basic. 
    /// This enables programs to call user-defined library functions in a form similar to what is possible with the built-in commands, including nicer parameter passing and return values. 
    /// Functions can be defined by use of the F.Function command and can be later called with one of the F.Call - commands.
    /// See the provided example "Function.sb" for a better introduction.
    /// </summary>
    [SmallBasicType]
    public static class F
    {
        static SmallBasicCallback start = null;
        static Dictionary<String, FunctionDefinition> functions = new Dictionary<String, FunctionDefinition>();
        static Dictionary<String, List<StackFrame>> stacks = new Dictionary<String, List<StackFrame>>();

        /// <summary>
        /// This property must be set to a subprogram before a subsequent F.Function operation is done which actually defines the function.
        /// </summary>
        public static event SmallBasicCallback Start
        {
            add
            {
                lock (functions)
                {
                    start = value;
                }
            }
            remove
            {
            }
        }

        /// <summary>
        /// Define a named function and its local variables/parameters with default values. Before this command is executed, the Start property needs to be set to a subroutine that will then be the starting point of the function.
        /// The local variables are also used for parameter passing when using a Call command: For any call with n parameters, these parameters will be assigned to the first n local variables. The rest of the local variables will be set to their defined initial value.
        /// </summary>
        /// <param name="name">The name for the function (needs to be a string literal)</param>
        /// <param name="parameterdefinitions">A string that holds a sequence of local variable names and initial values. This looks like for example "A B:5 T:hello". When no default is specified, 0 will be used.</param>
        public static void Function(Primitive name, Primitive parameterdefinitions)
        {
            String n = (name == null) ? "" : name.ToString().ToUpperInvariant();
            String pd = (parameterdefinitions == null) ? "" : parameterdefinitions.ToString();
            if (n.Length <= 0)
            {
                TextWindow.WriteLine("Can not define function with empty name");
                return;
            }

            lock (functions)
            {
                if (start==null)
                {   TextWindow.WriteLine("Need to specify a start subroutine before defining a function");
                    return;
                }

                string[] parameternames = pd.Split(new Char[] { ' ', '\t' });
                Primitive[] defaults = new Primitive[parameternames.Length];
                for (int i = 0; i < parameternames.Length; i++ )
                {
                    int colonidx = parameternames[i].IndexOf(':');
                    if (colonidx > 0)
                    {
                        defaults[i] = new Primitive(parameternames[i].Substring(colonidx + 1));
                        parameternames[i] = parameternames[i].Substring(0, colonidx).ToUpperInvariant();
                    }
                    else
                    {
                        defaults[i] = new Primitive("0");
                        parameternames[i] = parameternames[i].ToUpperInvariant();
                    }
                }
                
                functions[n] = new FunctionDefinition(start, 
                    parameternames, 
                    defaults);

                start = null;  // do not use same start subroutine twice
            }
        }


        /// <summary>
        /// Set a named local variable to a specified value.
        /// </summary>
        /// <param name="variablename">The name of the local variable (case insensitive)</param>
        /// <param name="value">The value to store into the local variable</param>
        public static void Set(Primitive variablename, Primitive value)
        {
            String vn = (variablename == null) ? "" : variablename.ToString().ToUpperInvariant();
            lock (functions)
            {
                List<StackFrame> stack = GetCurrentStack();
                StackFrame sf = stack[stack.Count - 1];
                if (sf.variables.ContainsKey(vn))
                {
                    sf.variables[vn] = value;
                }
                else
                {
                    TextWindow.WriteLine("Can not set undefined local variable: "+vn);
                }
            }
        }

        /// <summary>
        /// Causes the current function call to terminate immediately and delivers the value as number back to the caller.
        /// In "brick mode" it is only allowed to use this command in the topmost sub of a function.
        /// </summary>
        /// <param name="value">The return value (must be a number)</param>
        public static void ReturnNumber(Primitive value)
        {
            double number = value;
            Return(new Primitive(number));
        }

        /// <summary>
        /// Causes the current function call to terminate immediately and delivers the value as text back to the caller.
        /// In "brick mode" it is only allowed to use this command in the topmost sub of a function.
        /// </summary>
        /// <param name="value">The return value (is interpreted as text)</param>
        public static void ReturnText(Primitive value)
        {
            String text = value != null ? value.ToString() : "";
            Return(new Primitive(text));
        }

        /// <summary>
        /// Causes the current function call to terminate immediately.
        /// In "brick mode" it is only allowed to use this command in the topmost sub of a function.
        /// </summary>
        public static void Return()
        {
            Return(new Primitive(""));
        }

        private static void Return(Primitive value)
        {
            lock (functions)
            {
                List<StackFrame> stack = GetCurrentStack();
                if (stack.Count<2)
                {
                    TextWindow.WriteLine("Can not use 'Return' outside of function context");
                    return;
                }
            }
            throw new ReturnValue(value);
        }

        /// <summary>
        /// Retrieve the value of a named local variable.
        /// </summary>
        /// <param name="variablename">The name of the local variable (case insensitive)</param>
        /// <returns>The value stored in the variable</returns>
        public static Primitive Get(Primitive variablename)
        {
            String vn = (variablename == null) ? "" : variablename.ToString().ToUpperInvariant();
            lock (functions)
            {
                List<StackFrame> stack = GetCurrentStack();
                StackFrame sf = stack[stack.Count - 1];
                if (sf.variables.ContainsKey(vn))
                {
                    return sf.variables[vn];
                }
                else
                {
                    TextWindow.WriteLine("Can not get undefined local variable: " + vn);
                    return new Primitive("");
                }
            }
        }

        /// <summary>
        /// Do a function call without passing parameters.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call0(Primitive name)
        {   return Call(name); 
        }
        /// <summary>
        /// Do a function call with 1 parameter.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call1(Primitive name, Primitive p1)
        {
            return Call(name, p1);
        }
        /// <summary>
        /// Do a function call with 2 parameters.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call2(Primitive name, Primitive p1, Primitive p2)
        {
            return Call(name, p1, p2);
        }
        /// <summary>
        /// Do a function call with 3 parameters.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <param name="p3">Parameter 3</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call3(Primitive name, Primitive p1, Primitive p2, Primitive p3)
        {
            return Call(name, p1, p2, p3);
        }
        /// <summary>
        /// Do a function call with 4 parameters.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <param name="p3">Parameter 3</param>
        /// <param name="p4">Parameter 4</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call4(Primitive name, Primitive p1, Primitive p2, Primitive p3, Primitive p4)
        {
            return Call(name, p1, p2, p3, p4);
        }
        /// <summary>
        /// Do a function call with 5 parameters.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <param name="p3">Parameter 3</param>
        /// <param name="p4">Parameter 4</param>
        /// <param name="p5">Parameter 5</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call5(Primitive name, Primitive p1, Primitive p2, Primitive p3, Primitive p4, Primitive p5)
        {
            return Call(name, p1, p2, p3, p4, p5);
        }
        /// <summary>
        /// Do a function call with 6 parameters.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <param name="p3">Parameter 3</param>
        /// <param name="p4">Parameter 4</param>
        /// <param name="p5">Parameter 5</param>
        /// <param name="p6">Parameter 6</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call6(Primitive name, Primitive p1, Primitive p2, Primitive p3, Primitive p4, Primitive p5, Primitive p6)
        {
            return Call(name, p1, p2, p3, p4, p5, p6);
        }
        /// <summary>
        /// Do a function call with 7 parameters.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <param name="p3">Parameter 3</param>
        /// <param name="p4">Parameter 4</param>
        /// <param name="p5">Parameter 5</param>
        /// <param name="p6">Parameter 6</param>
        /// <param name="p7">Parameter 7</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call7(Primitive name, Primitive p1, Primitive p2, Primitive p3, Primitive p4, Primitive p5, Primitive p6, Primitive p7)
        {
            return Call(name, p1, p2, p3, p4, p5, p6, p7);
        }
        /// <summary>
        /// Do a function call with 8 parameters.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <param name="p3">Parameter 3</param>
        /// <param name="p4">Parameter 4</param>
        /// <param name="p5">Parameter 5</param>
        /// <param name="p6">Parameter 6</param>
        /// <param name="p7">Parameter 7</param>
        /// <param name="p8">Parameter 8</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call8(Primitive name, Primitive p1, Primitive p2, Primitive p3, Primitive p4, Primitive p5, Primitive p6, Primitive p7, Primitive p8)
        {
            return Call(name, p1, p2, p3, p4, p5, p6, p7, p8);
        }
        /// <summary>
        /// Do a function call with 9 parameters.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <param name="p3">Parameter 3</param>
        /// <param name="p4">Parameter 4</param>
        /// <param name="p5">Parameter 5</param>
        /// <param name="p6">Parameter 6</param>
        /// <param name="p7">Parameter 7</param>
        /// <param name="p8">Parameter 8</param>
        /// <param name="p9">Parameter 9</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call9(Primitive name, Primitive p1, Primitive p2, Primitive p3, Primitive p4, Primitive p5, Primitive p6, Primitive p7, Primitive p8, Primitive p9)
        {
            return Call(name, p1, p2, p3, p4, p5, p6, p7, p8, p9);
        }
        /// <summary>
        /// Do a function call with 10 parameters.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <param name="p3">Parameter 3</param>
        /// <param name="p4">Parameter 4</param>
        /// <param name="p5">Parameter 5</param>
        /// <param name="p6">Parameter 6</param>
        /// <param name="p7">Parameter 7</param>
        /// <param name="p8">Parameter 8</param>
        /// <param name="p9">Parameter 9</param>
        /// <param name="p10">Parameter 10</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call10(Primitive name, Primitive p1, Primitive p2, Primitive p3, Primitive p4, Primitive p5, Primitive p6, Primitive p7, Primitive p8, Primitive p9, Primitive p10)
        {
            return Call(name, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10);
        }
        /// <summary>
        /// Do a function call with 11 parameters.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <param name="p3">Parameter 3</param>
        /// <param name="p4">Parameter 4</param>
        /// <param name="p5">Parameter 5</param>
        /// <param name="p6">Parameter 6</param>
        /// <param name="p7">Parameter 7</param>
        /// <param name="p8">Parameter 8</param>
        /// <param name="p9">Parameter 9</param>
        /// <param name="p10">Parameter 10</param>
        /// <param name="p11">Parameter 11</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call11(Primitive name, Primitive p1, Primitive p2, Primitive p3, Primitive p4, Primitive p5, Primitive p6, Primitive p7, Primitive p8, Primitive p9, Primitive p10, Primitive p11)
        {
            return Call(name, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11);
        }
        /// <summary>
        /// Do a function call with 12 parameters.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <param name="p3">Parameter 3</param>
        /// <param name="p4">Parameter 4</param>
        /// <param name="p5">Parameter 5</param>
        /// <param name="p6">Parameter 6</param>
        /// <param name="p7">Parameter 7</param>
        /// <param name="p8">Parameter 8</param>
        /// <param name="p9">Parameter 9</param>
        /// <param name="p10">Parameter 10</param>
        /// <param name="p11">Parameter 11</param>
        /// <param name="p12">Parameter 12</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call12(Primitive name, Primitive p1, Primitive p2, Primitive p3, Primitive p4, Primitive p5, Primitive p6, Primitive p7, Primitive p8, Primitive p9, Primitive p10, Primitive p11, Primitive p12)
        {
            return Call(name, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12);
        }
        /// <summary>
        /// Do a function call with 13 parameters.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <param name="p3">Parameter 3</param>
        /// <param name="p4">Parameter 4</param>
        /// <param name="p5">Parameter 5</param>
        /// <param name="p6">Parameter 6</param>
        /// <param name="p7">Parameter 7</param>
        /// <param name="p8">Parameter 8</param>
        /// <param name="p9">Parameter 9</param>
        /// <param name="p10">Parameter 10</param>
        /// <param name="p11">Parameter 11</param>
        /// <param name="p12">Parameter 12</param>
        /// <param name="p13">Parameter 13</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call13(Primitive name, Primitive p1, Primitive p2, Primitive p3, Primitive p4, Primitive p5, Primitive p6, Primitive p7, Primitive p8, Primitive p9, Primitive p10, Primitive p11, Primitive p12, Primitive p13)
        {
            return Call(name, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13);
        }
        /// <summary>
        /// Do a function call with 14 parameters.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <param name="p3">Parameter 3</param>
        /// <param name="p4">Parameter 4</param>
        /// <param name="p5">Parameter 5</param>
        /// <param name="p6">Parameter 6</param>
        /// <param name="p7">Parameter 7</param>
        /// <param name="p8">Parameter 8</param>
        /// <param name="p9">Parameter 9</param>
        /// <param name="p10">Parameter 10</param>
        /// <param name="p11">Parameter 11</param>
        /// <param name="p12">Parameter 12</param>
        /// <param name="p13">Parameter 13</param>
        /// <param name="p14">Parameter 14</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call14(Primitive name, Primitive p1, Primitive p2, Primitive p3, Primitive p4, Primitive p5, Primitive p6, Primitive p7, Primitive p8, Primitive p9, Primitive p10, Primitive p11, Primitive p12, Primitive p13, Primitive p14)
        {
            return Call(name, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14);
        }
        /// <summary>
        /// Do a function call with 15 parameters.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <param name="p3">Parameter 3</param>
        /// <param name="p4">Parameter 4</param>
        /// <param name="p5">Parameter 5</param>
        /// <param name="p6">Parameter 6</param>
        /// <param name="p7">Parameter 7</param>
        /// <param name="p8">Parameter 8</param>
        /// <param name="p9">Parameter 9</param>
        /// <param name="p10">Parameter 10</param>
        /// <param name="p11">Parameter 11</param>
        /// <param name="p12">Parameter 12</param>
        /// <param name="p13">Parameter 13</param>
        /// <param name="p14">Parameter 14</param>
        /// <param name="p15">Parameter 15</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call15(Primitive name, Primitive p1, Primitive p2, Primitive p3, Primitive p4, Primitive p5, Primitive p6, Primitive p7, Primitive p8, Primitive p9, Primitive p10, Primitive p11, Primitive p12, Primitive p13, Primitive p14, Primitive p15)
        {
            return Call(name, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15);
        }
        /// <summary>
        /// Do a function call with 16 parameters.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <param name="p3">Parameter 3</param>
        /// <param name="p4">Parameter 4</param>
        /// <param name="p5">Parameter 5</param>
        /// <param name="p6">Parameter 6</param>
        /// <param name="p7">Parameter 7</param>
        /// <param name="p8">Parameter 8</param>
        /// <param name="p9">Parameter 9</param>
        /// <param name="p10">Parameter 10</param>
        /// <param name="p11">Parameter 11</param>
        /// <param name="p12">Parameter 12</param>
        /// <param name="p13">Parameter 13</param>
        /// <param name="p14">Parameter 14</param>
        /// <param name="p15">Parameter 15</param>
        /// <param name="p16">Parameter 16</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call16(Primitive name, Primitive p1, Primitive p2, Primitive p3, Primitive p4, Primitive p5, Primitive p6, Primitive p7, Primitive p8, Primitive p9, Primitive p10, Primitive p11, Primitive p12, Primitive p13, Primitive p14, Primitive p15, Primitive p16)
        {
            return Call(name, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16);
        }
        /// <summary>
        /// Do a function call with 17 parameters.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <param name="p3">Parameter 3</param>
        /// <param name="p4">Parameter 4</param>
        /// <param name="p5">Parameter 5</param>
        /// <param name="p6">Parameter 6</param>
        /// <param name="p7">Parameter 7</param>
        /// <param name="p8">Parameter 8</param>
        /// <param name="p9">Parameter 9</param>
        /// <param name="p10">Parameter 10</param>
        /// <param name="p11">Parameter 11</param>
        /// <param name="p12">Parameter 12</param>
        /// <param name="p13">Parameter 13</param>
        /// <param name="p14">Parameter 14</param>
        /// <param name="p15">Parameter 15</param>
        /// <param name="p16">Parameter 16</param>
        /// <param name="p17">Parameter 17</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call17(Primitive name, Primitive p1, Primitive p2, Primitive p3, Primitive p4, Primitive p5, Primitive p6, Primitive p7, Primitive p8, Primitive p9, Primitive p10, Primitive p11, Primitive p12, Primitive p13, Primitive p14, Primitive p15, Primitive p16, Primitive p17)
        {
            return Call(name, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16, p17);
        }
        /// <summary>
        /// Do a function call with 18 parameters.
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <param name="p3">Parameter 3</param>
        /// <param name="p4">Parameter 4</param>
        /// <param name="p5">Parameter 5</param>
        /// <param name="p6">Parameter 6</param>
        /// <param name="p7">Parameter 7</param>
        /// <param name="p8">Parameter 8</param>
        /// <param name="p9">Parameter 9</param>
        /// <param name="p10">Parameter 10</param>
        /// <param name="p11">Parameter 11</param>
        /// <param name="p12">Parameter 12</param>
        /// <param name="p13">Parameter 13</param>
        /// <param name="p14">Parameter 14</param>
        /// <param name="p15">Parameter 15</param>
        /// <param name="p16">Parameter 16</param>
        /// <param name="p17">Parameter 17</param>
        /// <param name="p18">Parameter 18</param>
        /// </summary>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call18(Primitive name, Primitive p1, Primitive p2, Primitive p3, Primitive p4, Primitive p5, Primitive p6, Primitive p7, Primitive p8, Primitive p9, Primitive p10, Primitive p11, Primitive p12, Primitive p13, Primitive p14, Primitive p15, Primitive p16, Primitive p17, Primitive p18)
        {
            return Call(name, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16, p17, p18);
        }
        /// <summary>
        /// Do a function call with 19 parameters.
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <param name="p3">Parameter 3</param>
        /// <param name="p4">Parameter 4</param>
        /// <param name="p5">Parameter 5</param>
        /// <param name="p6">Parameter 6</param>
        /// <param name="p7">Parameter 7</param>
        /// <param name="p8">Parameter 8</param>
        /// <param name="p9">Parameter 9</param>
        /// <param name="p10">Parameter 10</param>
        /// <param name="p11">Parameter 11</param>
        /// <param name="p12">Parameter 12</param>
        /// <param name="p13">Parameter 13</param>
        /// <param name="p14">Parameter 14</param>
        /// <param name="p15">Parameter 15</param>
        /// <param name="p16">Parameter 16</param>
        /// <param name="p17">Parameter 17</param>
        /// <param name="p18">Parameter 18</param>
        /// <param name="p19">Parameter 19</param>
        /// </summary>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call19(Primitive name, Primitive p1, Primitive p2, Primitive p3, Primitive p4, Primitive p5, Primitive p6, Primitive p7, Primitive p8, Primitive p9, Primitive p10, Primitive p11, Primitive p12, Primitive p13, Primitive p14, Primitive p15, Primitive p16, Primitive p17, Primitive p18, Primitive p19)
        {
            return Call(name, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16, p17, p18, p19);
        }
        /// <summary>
        /// Do a function call with 20 parameters.
        /// </summary>
        /// <param name="name">The name of the function (case insensitive)</param>
        /// <param name="p1">Parameter 1</param>
        /// <param name="p2">Parameter 2</param>
        /// <param name="p3">Parameter 3</param>
        /// <param name="p4">Parameter 4</param>
        /// <param name="p5">Parameter 5</param>
        /// <param name="p6">Parameter 6</param>
        /// <param name="p7">Parameter 7</param>
        /// <param name="p8">Parameter 8</param>
        /// <param name="p9">Parameter 9</param>
        /// <param name="p10">Parameter 10</param>
        /// <param name="p11">Parameter 11</param>
        /// <param name="p12">Parameter 12</param>
        /// <param name="p13">Parameter 13</param>
        /// <param name="p14">Parameter 14</param>
        /// <param name="p15">Parameter 15</param>
        /// <param name="p16">Parameter 16</param>
        /// <param name="p17">Parameter 17</param>
        /// <param name="p18">Parameter 18</param>
        /// <param name="p19">Parameter 19</param>
        /// <param name="p20">Parameter 20</param>
        /// <returns>The value returned by the function</returns>
        public static Primitive Call20(Primitive name, Primitive p1, Primitive p2, Primitive p3, Primitive p4, Primitive p5, Primitive p6, Primitive p7, Primitive p8, Primitive p9, Primitive p10, Primitive p11, Primitive p12, Primitive p13, Primitive p14, Primitive p15, Primitive p16, Primitive p17, Primitive p18, Primitive p19, Primitive p20)
        {
            return Call(name, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16, p17, p18, p19, p20);
        }


        private static Primitive Call(Primitive functionname, params Primitive[] args)
        {
            String fn = functionname==null ? "" : functionname.ToString().ToUpperInvariant();
            if (fn.Length < 1)
            {
                TextWindow.WriteLine("Can not call function with empty name");
                return new Primitive("");
            }

            FunctionDefinition fd;
            List<StackFrame> stack;
            Primitive ret = null;

            lock (functions)
            {
                if (!functions.ContainsKey(fn))
                {
                    TextWindow.WriteLine("Can not call undefined function: "+fn);
                    return new Primitive("");
                }

                fd = functions[fn];
                stack = GetCurrentStack();
                stack.Add(new StackFrame(fd.parameternames, fd.defaults, args));
            }

            try
            {
                fd.start.Invoke();
            }
            catch (ReturnValue rv)
            {
                ret = rv.value;
            }

            lock (functions)
            {
                if (stack.Count > 1)
                {
                    stack.RemoveAt(stack.Count - 1);
                }
            }

            return ret==null ? new Primitive("") : ret;
        }

        private static List<StackFrame> GetCurrentStack()
        {
            String threadname = System.Threading.Thread.CurrentThread.Name;
            if (threadname==null || !threadname.StartsWith("EV3-")) threadname="EV3-MAIN";
            if (stacks.ContainsKey(threadname))
            {   return stacks[threadname];
            }
            else
            {
                List<StackFrame> s = new List<StackFrame>();
                s.Add(new StackFrame(null, null, null));
                stacks[threadname] = s;
                return s;
            }
        }
    }

    class FunctionDefinition
    {
        internal readonly SmallBasicCallback start;
        internal readonly String[] parameternames;
        internal readonly Primitive[] defaults;

        public FunctionDefinition(SmallBasicCallback start, String[] parameternames, Primitive[] defaults)
        {
            this.start = start;
            this.parameternames = parameternames;
            this.defaults = defaults;
        }
    }

    class StackFrame
    {
        internal readonly Dictionary<String, Primitive> variables;

        public StackFrame(String[] names, Primitive[] defaults, Primitive[] args)
        {
            variables = new Dictionary<String,Primitive>();
            for (int i = 0; names!=null && i < names.Length && defaults!=null && i < defaults.Length; i++)
            {
                variables[names[i]] =  (i < args.Length && args[i]!=null) ? args[i] : defaults[i];
            }
        }
    }

    class ReturnValue: System.Exception
    {
        internal readonly Primitive value;

        internal ReturnValue(Primitive value)
        {
            this.value = value;
        }
    }



}
