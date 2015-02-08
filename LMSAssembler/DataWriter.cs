using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace LMSAssembler
{
    public class DataWriter
    {

        public static void Write32(Stream stream, int value)
        {
            stream.WriteByte((byte)(value & 0xff));
            stream.WriteByte((byte)((value >> 8) & 0xff));
            stream.WriteByte((byte)((value >> 16) & 0xff));
            stream.WriteByte((byte)((value >> 24) & 0xff));
        }

        public static void Write16(Stream stream, int value)
        {
            stream.WriteByte((byte)(value & 0xff));
            stream.WriteByte((byte)((value >> 8) & 0xff));
        }

    }
}
