using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EV3BasicCompiler
{
    class CompileException: System.Exception
    {
        public CompileException(String message) : base(message)
        {
        }
    }
}

