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
        private int size;
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
                    return "SmallBasic";
                }
                else if (name.EndsWith(".lms", StringComparison.InvariantCultureIgnoreCase))
                {
                    return "Assembler";
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
                       || name.EndsWith(".lms", StringComparison.InvariantCultureIgnoreCase)
                    ;
                }
                return false;
            }
        }

        public DirectoryEntry(string name, int size, bool directory)
        {
            this.name = name;
            this.size = size;
            this.directory = directory;
        }
    }

    public class PCFile : DirectoryEntry
    { 
        public readonly FileInfo fileinfo;

        public PCFile(FileInfo fileinfo) : base (fileinfo.Name, (int)fileinfo.Length, false)
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
