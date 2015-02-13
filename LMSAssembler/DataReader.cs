
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace LMSAssembler
{
    class DataReader
    {

        public static int Read32(Stream stream, ref int readposition)
        {
            int b0 = stream.ReadByte() & 0xff;
            int b1 = stream.ReadByte() & 0xff;
            int b2 = stream.ReadByte() & 0xff;
            int b3 = stream.ReadByte() & 0xff;
            readposition += 4;
            return b0 | (b1<<8) | (b2<<16) | (b3<<24);
        }

        public static int Read16(Stream stream, ref int readposition)
        {
            int b0 = stream.ReadByte() & 0xff;
            int b1 = stream.ReadByte() & 0xff;
            readposition += 2;
            return (short) (b0 | (b1 << 8));
        }

        public static int Read8(Stream stream, ref int readposition)
        {
            int b0 = stream.ReadByte() & 0xff; 
            readposition += 1;
            return (sbyte) b0;            
        }

    }

   
}
