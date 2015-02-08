using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EV3BasicCompiler
{
    class LibraryEntry
    {
        public readonly bool VoidReturnType;
        public readonly bool StringReturnType;

        public readonly bool[] StringParamTypes;

        public readonly String[] references;

        public readonly String programCode;

        public LibraryEntry(String[] descriptor_and_references, String code)
        {
            String descriptor = descriptor_and_references[0];
            VoidReturnType = descriptor.EndsWith("V");
            StringReturnType = descriptor.EndsWith("S");

            StringParamTypes = new bool[descriptor.Length - 1];
            for (int i = 0; i < StringParamTypes.Length; i++)
            {
                StringParamTypes[i] = descriptor[i] == 'S';
            }

            references = new String[descriptor_and_references.Length - 1];
            for (int i = 0; i < references.Length; i++)
            {
                references[i] = descriptor_and_references[1 + i];
            }

            programCode = code;
        }

        public override string ToString()
        {
            StringBuilder s = new StringBuilder();
            for (int i = 0; i < StringParamTypes.Length; i++)
            {
                s.Append(StringParamTypes[i] ? "STRING " : "FLOAT ");
            }
            s.Append("-> ");
            if (VoidReturnType)
            {
                s.Append("VOID");
            }
            else if (StringReturnType)
            {
                s.Append("STRING");
            }
            else
            {
                s.Append("FLOAT");
            }
            return s.ToString();
        }

    }
}
