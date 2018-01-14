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
using System.IO;

namespace EV3BasicCompiler
{
    // ------------------------------------ TOKEN SCANNER ---------------------------------

    public enum SymType : byte { ID, NUMBER, STRING, KEYWORD, SPECIAL, EOL, EOF, PRAGMA };

    public class Scanner
    {
        SymType nexttype;
        String nextcontent;

        private List<String> lines;
        private String line;
        private int linenumber;
        private int columnnumber;

        Stack<SymType> pushbackbuffer_type;
        Stack<String> pushbackbuffer_content;

        public Scanner (Stream source)
        {
            StreamReader reader = new StreamReader(source, System.Text.Encoding.UTF8);
            lines = new List<String>();
            do
            {
                lines.Add(reader.ReadLine());
            } while (lines.Last() != null);

            StartFromBegin();
        }

        public void StartFromBegin()
        {
            nexttype = SymType.EOF;
            nextcontent = "";
            linenumber = 0;
            columnnumber = 0;
            line = lines[0];

            pushbackbuffer_type = new Stack<SymType>();
            pushbackbuffer_content = new Stack<String>();
        }

        public SymType NextType
        {
            get { return nexttype; }
        }

        public String NextContent
        {
            get { return nextcontent; }
        }

        public bool NextIsKEYWORD(String txt)
        {
            return (nexttype == SymType.KEYWORD) && nextcontent.Equals(txt);
        }
        public bool NextIsSPECIAL(String txt)
        {
            return (nexttype == SymType.SPECIAL) && nextcontent.Equals(txt);
        }

        public void ThrowParseError(String message)
        {
            throw new CompileException(message + " at: " + (linenumber + 1) + ":" + (columnnumber + 1));
        }
        public void ThrowUnexpectedSymbol()
        {
            ThrowParseError("Unexpected " + nexttype + " " + nextcontent);
        }
        public void ThrowExpectedSymbol(SymType type, String content)
        {
            if (content != null)
            {
                ThrowParseError("Expected " + content);
            }
            else
            {
                ThrowParseError("Expected " + type);
            }
        }

        public void GetSym()
        {
            if (pushbackbuffer_type.Count>0)
            {
                nexttype = pushbackbuffer_type.Pop();
                nextcontent = pushbackbuffer_content.Pop();
                return;
            }

            for (; ; )
            {
                if (line == null)
                {
                    nexttype = SymType.EOF;
                    nextcontent = "";
                    return;
                }
                if (columnnumber >= line.Length)
                {
                    nexttype = SymType.EOL;
                    nextcontent = "";
                    linenumber++;
                    line = lines[linenumber];
                    columnnumber = 0;
                    return;
                }
                if (columnnumber==0 && line.StartsWith("'PRAGMA "))
                {
                    nexttype = SymType.PRAGMA;
                    nextcontent = line.Substring(8).Trim();
                    linenumber++;
                    line = lines[linenumber];
                    columnnumber = 0;
                    return;
                }
                switch (line[columnnumber])
                {
                    case '\'':
                        // detect begin of comment
                        columnnumber = line.Length;
                        break;
                    case ' ':
                    case '\t':
                        // white spaces will be skipped in normal program context
                        columnnumber++;
                        break;
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        {   // found a number (with optional decimal point, but no '-')
                            int startpos = columnnumber;
                            columnnumber++;
                            while (columnnumber < line.Length && ((line[columnnumber] >= '0' && line[columnnumber] <= '9') || line[columnnumber] == '.'))
                            {
                                columnnumber++;
                            }
                            nexttype = SymType.NUMBER;
                            nextcontent = line.Substring(startpos, columnnumber - startpos);
                            return;
                        }
                    case '"':
                        {   // found a string (maybe with missing trailing ")
                            int startpos = columnnumber;
                            columnnumber++;
                            for (; ; )
                            {
                                if (columnnumber >= line.Length)
                                {
                                    throw new Exception("Nonterminated string at: " + (linenumber + 1) + ":" + (columnnumber + 1));
                                }
                                if (line[columnnumber] == '"')
                                {
                                    columnnumber++;
                                    // an additonal " continues the string
                                    if (columnnumber < line.Length && line[columnnumber] == '"')
                                    {
                                        columnnumber++;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                                columnnumber++;
                            }

                            nexttype = SymType.STRING;
                            nextcontent = line.Substring(startpos + 1, columnnumber - startpos - 2);  // deliver string without "
                            return;
                        }
                    default:
                        {
                            char c = line[columnnumber];
                            if ((c >= 'A' && c <= 'Z')
                              || (c >= 'a' && c <= 'z')
                              || (c == '_'))
                            // found an identifier
                            {
                                int startpos = columnnumber;
                                columnnumber++;
                                while (columnnumber < line.Length)
                                {
                                    c = line[columnnumber];
                                    if ((c >= 'A' && c <= 'Z')
                                        || (c >= 'a' && c <= 'z')
                                        || (c == '_')
                                        || (c >= '0' && c <= '9'))
                                    {
                                        columnnumber++;
                                        continue;
                                    }
                                    break;
                                }
                                String w = line.Substring(startpos, columnnumber - startpos).ToUpperInvariant();
                                nexttype = SymType.ID;
                                nextcontent = w;

                                if (w.Equals("AND") || w.Equals("ELSE") || w.Equals("ELSEIF") || w.Equals("ENDFOR") || w.Equals("ENDIF")
                                || w.Equals("ENDSUB") || w.Equals("ENDWHILE") || w.Equals("FOR") || w.Equals("GOTO") || w.Equals("IF")
                                || w.Equals("OR") || w.Equals("STEP") || w.Equals("SUB") || w.Equals("THEN") || w.Equals("TO") || w.Equals("WHILE")
                                )
                                {
                                    nexttype = SymType.KEYWORD;
                                }
                                return;
                            }
                            else
                            {
                                // other stuff is probably a special character
                                nexttype = SymType.SPECIAL;
                                nextcontent = line.Substring(columnnumber, 1);
                                columnnumber++;

                                // detect two-digit special operators also
                                if (nextcontent.Equals("<") && columnnumber < line.Length && line[columnnumber] == '=')
                                {
                                    nextcontent = "<=";
                                    columnnumber++;
                                }
                                else if (nextcontent.Equals(">") && columnnumber < line.Length && line[columnnumber] == '=')
                                {
                                    nextcontent = ">=";
                                    columnnumber++;
                                }
                                else if (nextcontent.Equals("<") && columnnumber < line.Length && line[columnnumber] == '>')
                                {
                                    nextcontent = "<>";
                                    columnnumber++;
                                }

                                return;
                            }
                        }
                }
            }
        }

        public void PushBack(SymType previoustype, String previouscontent)
        {
            pushbackbuffer_type.Push(nexttype);
            pushbackbuffer_content.Push(nextcontent);
            nexttype = previoustype;
            nextcontent = previouscontent;
        }


    }
}
