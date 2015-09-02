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

namespace EV3Explorer
{
    public class DirectoryEntry
    {
        private String name;
        private long size;
        private bool directory;

        public string FileName { 
            get { return name; }
        }
        public String FileSize {
            get { return directory  ? "" : ""+size; } 
        }
        public string FileType { 
            get {
                if (directory)
                {
                    return "Folder";
                }
                else if (name.EndsWith(".sb", StringComparison.InvariantCultureIgnoreCase))
                {
                    return "Basic";
                }
                else if (name.EndsWith(".smallbasic", StringComparison.InvariantCultureIgnoreCase))
                {
                    return "Basic";
                }
                else if (name.EndsWith(".lms", StringComparison.InvariantCultureIgnoreCase))
                {
                    return "Assembler";
                }
                else if (name.EndsWith(".rbf", StringComparison.InvariantCultureIgnoreCase))
                {
                    return "Executable";
                }
                else if (name.EndsWith(".rgf", StringComparison.InvariantCultureIgnoreCase))
                {
                    return "Bitmap";
                }
                else if (name.EndsWith(".rsf", StringComparison.InvariantCultureIgnoreCase))
                {
                    return "Sound";
                }
                return "";
            }
        }
        public bool IsDirectory
        {
            get { return directory; }
        }

        public bool IsCompileable
        {
            get
            {
                if (!directory)
                {
                    return name.EndsWith(".sb", StringComparison.InvariantCultureIgnoreCase)
                       || name.EndsWith(".smallbasic", StringComparison.InvariantCultureIgnoreCase)
                       || name.EndsWith(".lms", StringComparison.InvariantCultureIgnoreCase)
                    ;
                }
                return false;
            }
        }

        public bool IsRunable
        {
            get
            {
                if (!directory)
                {
                    return name.EndsWith(".rbf", StringComparison.InvariantCultureIgnoreCase);
                }
                return false;
            }
        }

        public DirectoryEntry(string name, long size, bool directory)
        {
            this.name = name;
            this.size = size;
            this.directory = directory;
        }
    }

    public class PCFile : DirectoryEntry
    { 
        public readonly FileInfo fileinfo;

        public PCFile(FileInfo fileinfo) : base (fileinfo.Name, fileinfo.Length, false)
        {
            this.fileinfo = fileinfo;
        }
    }

    public class PCDirectory : DirectoryEntry
    {
        public readonly DirectoryInfo directoryinfo;

        public PCDirectory(DirectoryInfo directoryinfo) : base (directoryinfo.Name, 0, true)
        {
            this.directoryinfo = directoryinfo;
        }
    }
}
