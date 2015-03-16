using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Microsoft.SmallBasic.Library;
using EV3Communication;

namespace SmallBasicEV3Extension
{

    /// <summary>
    /// Access the file system on the EV3 brick to read or write data.
    /// File names can be given either absolute (with a leading '/') to reach any file in the system, or relative to the 'prjs' folder.
    /// </summary>
    [SmallBasicType]
    public static class EV3File
    {
        static FileHandle[] openFiles = new FileHandle[100];


        private static Primitive OpenWriteImpl(Primitive filename, bool append)
        {
            String f = (filename == null ? "" : filename.ToString());
            if (!f.StartsWith("/"))
            {
                f = "/home/root/lms2012/prjs/" + f;
            }
            lock (openFiles)
            {
                if (findHandle(f)>=0)       // already have open file with this name
                {
                    return new Primitive(0);
                }
                int i = findUnusedHandleSlot();  
                if (i<=0)                   // can not hold any more files
                {
                    return new Primitive(0);
                }

                // create a new empty file. must close handle immediately because it can not be kept accross direct commands
                ByteCodeBuffer c = new ByteCodeBuffer();
                c.OP(0xC0);       // opFile
                c.CONST(append ? 0x00: 0x02);    // OPEN_APPEND = 0x00   OPEN_WRITE = 0x02
                c.STRING(f);
                c.GLOBVAR(0);     // result: 16-bit handle
                c.OP(0xC0);         // opFile
                c.CONST(0x07);      // CLOSE = 0x07
                c.GLOBVAR(0);
                
                // check if could indeed create/append file on the brick
                byte[] reply = EV3Communicator.DirectCommand(c, 2, 0);

                if (reply==null || reply.Length<2 || (reply[0]==0 && reply[1]==0))
                {
                    return new Primitive(0);
                }

                // memorize the open file
                openFiles[i] = new FileHandle(f);

                return new Primitive((double)i);
            }
        }

        /// <summary>
        /// Open a file for writing. When the file already exists, it will be overwritten.
        /// </summary>
        /// <param name="filename">Name of the file to create/overwrite.</param>
        /// <returns>A number that identifies this open file (a.k.a. file handle)</returns>
        public static Primitive OpenWrite(Primitive filename)
        {
            return OpenWriteImpl(filename, false);
        }

        /// <summary>
        /// Open a file for adding data. When the file does not exist, it will be created.
        /// </summary>
        /// <param name="filename">Name of the file to create/extend.</param>
        /// <returns>A number that identifies this open file (a.k.a. file handle)</returns>
        public static Primitive OpenAppend(Primitive filename)
        {
            return OpenWriteImpl(filename, true);
        }

        /// <summary>
        /// Open a file for reading data. When the file does not exist the program will crash.
        /// </summary>
        /// <param name="filename">Name of the file to read from.</param>
        /// <returns>A number that identifies this open file (a.k.a. file handle)</returns>
        public static Primitive OpenRead(Primitive filename)
        {
            String f = (filename == null ? "" : filename.ToString());
            if (!f.StartsWith("/"))
            {
                f = "/home/root/lms2012/prjs/" + f;
            }
            lock (openFiles)
            {
                if (findHandle(f) >= 0)       // already have open file with this name
                {
                    return new Primitive(0);
                }
                int i = findUnusedHandleSlot();
                if (i <= 0)                   // can not hold any more files
                {
                    return new Primitive(0);
                }

                byte[] content = EV3Communicator.ReadEV3File(f);
                if (content==null)
                {
                    return new Primitive(0);
                }

                openFiles[i] = new FileHandle(f, content);  
                return new Primitive((double)i);
            }
        }

        /// <summary>
        /// Close an open file.
        /// </summary>
        /// <param name="handle">The file handle (previously obtained from an Open... call)</param>
        public static void Close(Primitive handle)
        {
            int hdl = 0;
            Int32.TryParse(handle == null ? "" : handle.ToString(), out hdl);
            lock (openFiles)
            {
                if (hdl >= 0 && hdl < openFiles.Length && openFiles[hdl] != null)
                {
                    openFiles[hdl] = null;
                }
            }
        }

        /// <summary>
        /// Write one line of text to the file. The line will be encoded with ISO-8859-1 encoding and will be terminated with a newline-character (code 10).
        /// </summary>
        /// <param name="handle">The file handle (previously obtained from an Open... call)</param>
        /// <param name="text">The text to write to the file.</param>
        public static void WriteLine(Primitive handle, Primitive text)
        {
            int hdl = 0;
            Int32.TryParse(handle == null ? "" : handle.ToString(), out hdl);
            String txt = (text == null ? "" : text.ToString());
            if (txt.Length > 251)
            {
                txt = txt.Substring(0, 251);   // delimit line size to be written to 251 (which is also string size limit for VM-version)
            }
            lock (openFiles)
            {
                if (hdl >= 0 && hdl < openFiles.Length && openFiles[hdl] != null)
                {
                    ByteCodeBuffer c = new ByteCodeBuffer();
                    c.OP(0xC0);       // opFile
                    c.CONST(0x00);    // OPEN_APPEND = 0x00
                    c.STRING(openFiles[hdl].name);
                    c.GLOBVAR(0);     // result: 16-bit handle
                    c.OP(0xC0);       // opFile
                    c.CONST(0x06);    // WRITE_TEXT = 0x06
                    c.GLOBVAR(0);
                    c.CONST(6);       // 0x06 : Line feed used as delimiter
                    c.STRING(txt);
                    c.OP(0xC0);       // opFile
                    c.CONST(0x07);    // CLOSE = 0x07
                    c.GLOBVAR(0);
                    EV3Communicator.DirectCommand(c, 2, 0);
                }
            }
        }

        /// <summary>
        /// Write a single byte of data on the file.
        /// </summary>
        /// <param name="handle">The file handle (previously obtained from an Open... call)</param>
        /// <param name="data">One byte to write (value of 0 - 255).</param>        
        public static void WriteByte(Primitive handle, Primitive data)
        {
            int hdl = 0;
            int dta = 0;
            Int32.TryParse(handle == null ? "" : handle.ToString(), out hdl);
            Int32.TryParse(data == null ? "" : data.ToString(), out dta);
            lock (openFiles)
            {
                if (hdl >= 0 && hdl < openFiles.Length && openFiles[hdl] != null)
                {
                    ByteCodeBuffer c = new ByteCodeBuffer();
                    c.OP(0x34);       // opMove16_8
                    c.CONST(dta);
                    c.GLOBVAR(2);
                    c.OP(0xC0);       // opFile
                    c.CONST(0x00);    // OPEN_APPEND = 0x00
                    c.STRING(openFiles[hdl].name);
                    c.GLOBVAR(0);     // result: 16-bit handle
                    c.OP(0xC0);       // opFile
                    c.CONST(0x1D);    // WRITE_BYTES = 0x1D
                    c.GLOBVAR(0);
                    c.CONST(1);       // write 1 byte
                    c.GLOBVAR(2);     // where to take the byte from
                    c.OP(0xC0);       // opFile
                    c.CONST(0x07);    // CLOSE = 0x07
                    c.GLOBVAR(0);
                    EV3Communicator.DirectCommand(c, 3, 0);
                }
            }
        }

        /// <summary>
        /// Read one line of text from the file. The line will be decoded using the ISO-8859-1 encoding and must be terminated with a newline-character (code 10).
        /// </summary>
        /// <param name="handle">The file handle (previously obtained from an Open... call)</param>
        /// <returns>The text from the current line in the file.</returns>
        public static Primitive ReadLine(Primitive handle)
        {
            int hdl = 0;
            Int32.TryParse(handle == null ? "" : handle.ToString(), out hdl);
            lock (openFiles)
            {
                if (hdl >= 0 && hdl < openFiles.Length && openFiles[hdl] != null)
                {
                    FileHandle fh = openFiles[hdl];
                    if (fh.content != null && fh.readcursor < fh.content.Length)
                    {
                        StringBuilder b = new StringBuilder();
                        while (fh.readcursor<fh.content.Length && fh.content[fh.readcursor]!='\n')
                        {
                            b.Append((char)fh.content[fh.readcursor]);
                            fh.readcursor++;
                        }
                        fh.readcursor++;
                        return new Primitive(b.ToString());
                    }
                }
            }
            return new Primitive("");
        }

        /// <summary>
        /// Read one byte of data from the file.
        /// </summary>
        /// <param name="handle">The file handle (previously obtained from an Open... call)</param>
        /// <returns>The next byte from the file.</returns>
        public static Primitive ReadByte(Primitive handle)
        {
            int hdl = 0;
            Int32.TryParse(handle == null ? "" : handle.ToString(), out hdl);
            lock (openFiles)
            {
                if (hdl >= 0 && hdl < openFiles.Length && openFiles[hdl] != null)
                {
                    FileHandle fh = openFiles[hdl];
                    if (fh.content!=null && fh.readcursor<fh.content.Length)
                    {
                        byte b = fh.content[fh.readcursor];
                        fh.readcursor++;
                        return new Primitive((int)b);
                    }
                }
            }
            return new Primitive(0);
        }

        /// <summary>
        /// Utility function to convert a text to a number. 
        /// </summary>
        /// <param name="text">A text holding a number in decimal representation (with optional fractional digits)</param>
        /// <returns>The number</returns>
        public static Primitive ConvertToNumber(Primitive text)
        {
            double d = 0;
            double.TryParse(text == null ? "" : text.ToString(), out d);
            return new Primitive(d);
        }

        /// <summary>
        /// Utility function to read bytes from potentially huge data files that are too big to be transfered to memory as a whole.
        /// Because the file could be so big that the numerical precision of the normal numbers is not enough, a row/column addressing is possible.
        /// </summary>
        /// <param name="filename">The name of the file.</param>
        /// <param name="bytes_per_row">When the file has a row/column structure, this is the number of bytes in one row. Use 1 if not applicable.</param>
        /// <param name="row">Which row to access (start with 0).</param>
        /// <param name="column">Which column to access (start with 0).</param>
        /// <returns>The byte on the denoted position</returns>
        public static Primitive TableLookup(Primitive filename, Primitive bytes_per_row, Primitive row, Primitive column)
        {
            int bpr = 0;
            int r = 0;
            int c = 0;

            String fullname = (filename == null) ? "" : filename.ToString();
            Int32.TryParse(bytes_per_row == null ? "" : bytes_per_row.ToString(), out bpr);
            Int32.TryParse(row == null ? "" : row.ToString(), out r);
            Int32.TryParse(column == null ? "" : column.ToString(), out c);
            if (bpr<=0 || row<0 || column<0)
            {
                return new Primitive(0);
            }
            if (!fullname.StartsWith("/"))
            {
                fullname = "/home/root/lms2012/prjs/" + fullname;
            }
            return EV3.NativeCode(new Primitive("tablelookup "+fullname+" "+ bpr + " " + r + " " + c));
        }



        private static int findHandle(String name)
        {
            for (int i=0; i<openFiles.Length; i++)
            {
                if (openFiles[i]!=null && openFiles[i].name.Equals(name))
                {
                    return i;
                }
            }
            return -1;
        }
        private static int findUnusedHandleSlot()
        {
            for (int i=1; i<openFiles.Length; i++)
            {
                if (openFiles[i]==null)
                {
                    return i;
                }
            }
            return -1;
        }
    }

    internal class FileHandle
    {
        internal String name;
        internal byte[] content;
        internal int readcursor;

        internal FileHandle(String name) : this(name, null)
        { }
        internal FileHandle(String name, byte[] content)
        {
            this.name = name;
            this.content = content;
            this.readcursor = 0;
        }

    }
}
