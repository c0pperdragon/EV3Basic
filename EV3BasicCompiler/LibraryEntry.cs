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
