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
    /// A framework to create true functions with parameters and local variables.
    /// </summary>
    [SmallBasicType]
    public static class F
    {
        static SmallBasicCallback sub = null;
        static Dictionary<String, FunctionDefinition> functions = new Dictionary<String, FunctionDefinition>();
        static Dictionary<String, List<StackFrame>> stacks = new Dictionary<String, List<StackFrame>>();

        public static event SmallBasicCallback Start
        {
            add
            {
                lock (functions)
                {
                    sub = value;
                }
            }
            remove
            {
            }
        }

        public static void Function(Primitive name, Primitive parameterdefinitions, Primitive returntype)
        {
            lock (functions)
            {
                if (name != null && parameterdefinitions!=null)
                {
                    String n = name.ToString();
                    String pd = parameterdefinitions.ToString();
                    if (n.Length > 0)
                    {
                        string[] parameternames = pd.Split(new Char[] { ' ', '\t' });
                        for (int i = 0; i < parameternames.Length; i++ )
                        {
                            int colonidx = parameternames[i].IndexOf(':');
                            if (colonidx > 0) parameternames[i] = parameternames[i].Substring(0, colonidx);
                        }
                        functions[n] = new FunctionDefinition(sub, parameternames);
                    }
                }
            }
        }

        public static void Set(Primitive variablename, Primitive value)
        {
            lock (functions)
            {
                List<StackFrame> stack = GetCurrentStack();
                StackFrame sf = stack[stack.Count - 1];
                if (variablename != null)
                {
                    String vn = variablename.ToString();
                    if (vn.Length > 0)
                    {
                        sf.Set(vn, value);
                    }
                }
            }
        }

        public static void Return(Primitive value)
        {
            lock (functions)
            {
                List<StackFrame> stack = GetCurrentStack();
                StackFrame sf = stack[stack.Count - 1];
                sf.Set("", value);
            }
        }

        public static Primitive Get(Primitive variablename)
        {
            lock (functions)
            {
                List<StackFrame> stack = GetCurrentStack();
                StackFrame sf = stack[stack.Count - 1];
                if (variablename != null)
                {
                    String vn = variablename.ToString();
                    if (vn.Length > 0)
                    {
                        return sf.Get(vn);
                    }
                }
            }
            return new Primitive("0");
        }

        public static Primitive Call0(Primitive functionname)
        {   return Call(functionname); 
        }
        public static Primitive Call1(Primitive functionname, Primitive p1)
        {
            return Call(functionname, p1);
        }
        public static Primitive Call2(Primitive functionname, Primitive p1, Primitive p2)
        {
            return Call(functionname, p1, p2);
        }
        public static Primitive Call3(Primitive functionname, Primitive p1, Primitive p2, Primitive p3)
        {
            return Call(functionname, p1, p2, p3);
        }
        public static Primitive Call4(Primitive functionname, Primitive p1, Primitive p2, Primitive p3, Primitive p4)
        {
            return Call(functionname, p1, p2, p3, p4);
        }
        public static Primitive Call5(Primitive functionname, Primitive p1, Primitive p2, Primitive p3, Primitive p4, Primitive p5)
        {
            return Call(functionname, p1, p2, p3, p4, p5);
        }


        private static Primitive Call(Primitive functionname, params Primitive[] args)
        {
            if (functionname == null) return new Primitive("0");
            String fn = functionname.ToString();
            if (fn.Length < 1)
            {
                return new Primitive("0");
            }

            FunctionDefinition fd;
            List<StackFrame> stack;
            Primitive ret;

            lock (functions)
            {
                if (!functions.ContainsKey(fn)) return new Primitive("0");
                fd = functions[fn];
                stack = GetCurrentStack();
                stack.Add(new StackFrame(fd.parameternames, args));
            }

            fd.sub.Invoke();

            lock (functions)
            {
                ret = stack[stack.Count - 1].Get("");
                if (stack.Count > 1)
                {
                    stack.RemoveAt(stack.Count - 1);
                }
            }

            return ret;
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
                s.Add(new StackFrame(null, null));
                stacks[threadname] = s;
                return s;
            }
        }
    }

    class FunctionDefinition
    {
        internal readonly SmallBasicCallback sub;
        internal readonly String[] parameternames;

        public FunctionDefinition(SmallBasicCallback sub, String[] parameternames)
        {
            this.sub = sub;
            this.parameternames = parameternames;
        }
    }

    class StackFrame
    {
        Dictionary<String, Primitive> variables;

        public StackFrame(String[] names, Primitive[] values)
        {
            variables = new Dictionary<String,Primitive>();
            for (int i = 0; names!=null && i < names.Length && values!=null && i < values.Length; i++)
            {
                Set(names[i], values[i]);
            }
        }

        public void Set(String name, Primitive value)
        {
            variables[name] = value;
        }

        public Primitive Get(String name)
        {
            if (variables.ContainsKey(name))
            {
                return variables[name];
            }
            return new Primitive("0");
        }
    }
}
