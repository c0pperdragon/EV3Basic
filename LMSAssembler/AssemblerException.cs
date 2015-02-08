using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LMSAssembler
{
    class AssemblerException : Exception
    {
        public AssemblerException (String message) : base(message)
        { }
    }
}
