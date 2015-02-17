using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EV3BasicCompiler
{
    class LibraryEntry
    {
        public readonly bool inline;

        public readonly ExpressionType returnType;
        public readonly ExpressionType[] paramTypes;

        public readonly String[] references;

        public readonly String programCode;

        public LibraryEntry(bool inline, String[] descriptor_and_references, String code)
        {
            this.inline = inline;
            String descriptor = descriptor_and_references[0];
            returnType = decodeType(descriptor[descriptor.Length-1]);

            paramTypes = new ExpressionType[descriptor.Length - 1];
            for (int i = 0; i < paramTypes.Length; i++)
            {
                paramTypes[i] = decodeType(descriptor[i]);
            }

            references = new String[descriptor_and_references.Length - 1];
            for (int i = 0; i < references.Length; i++)
            {
                references[i] = descriptor_and_references[1 + i];
            }

            // for inlining code, trim away "{" and "}"
            if (inline)
            {
                int startbrace = code.IndexOf('{');
                int endbrace = code.IndexOf('}');
                code = code.Substring(startbrace + 1, endbrace - startbrace - 2).Trim();
            }
            programCode = code;
        }

        private ExpressionType decodeType(char c)
        {
            switch (c)
            {   case 'F': return ExpressionType.Number;
                case 'S': return ExpressionType.Text;
                case 'A': return ExpressionType.NumberArray;
                case 'X': return ExpressionType.TextArray;
                case 'V': return ExpressionType.Void;
                default: throw new Exception("Can not read runtime library");
            }
        }

        public override string ToString()
        {
            StringBuilder s = new StringBuilder();
            for (int i = 0; i < paramTypes.Length; i++)
            {
                s.Append(paramTypes[i]);
                s.Append(" ");
            }
            s.Append("-> ");
            s.Append(returnType);
            return s.ToString();
        }

    }
}
