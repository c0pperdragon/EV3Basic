using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EV3Communication
{
    public class ByteCodeBuffer : BinaryBuffer
    {
        public ByteCodeBuffer() : base()
        { }

        public void OP(byte opcode)
        {
            Append8(opcode);
        }

        public void CONST(int par)
        {
            if (par >= -32 && par <= 31)
            {
                Append8(par & 0x3f);
            }
            else if (par >= -128 && par <= 127)
            {
                Append8(0x81);
                Append8(par);
            }
            else if (par >= -32768 && par <= 32767)
            {
                Append8(0x82);
                Append16(par);
            }
            else
            {
                Append8(0x83);
                Append32(par);
            }
        }

        public void GLOBVAR(int var)
        {
            if (var <= 31)
            {
                Append8((var & 0x1f) | 0x60);
            }
            else if (var <= 255)
            {
                Append8(0xe1);
                Append8(var);
            }
            else if (var <= 65535)
            {
                Append8(0xe2);
                Append16(var);
            }
            else
            {
                Append8(0xe3);
                Append32(var);
            }
        }

        public void LOCVAR(int var)
        {
            if (var <= 31)
            {
                Append8((var & 0x1f) | 0x40);
            }
            else if (var <= 255)
            {
                Append8(0xc1);
                Append8(var);
            }
            else if (var <= 65535)
            {
                Append8(0xc2);
                Append16(var);
            }
            else
            {
                Append8(0xc3);
                Append32(var);
            }
        }

        public void STRING(String text)
        {
            Append8(0x84);
            AppendZeroTerminated(text);
        }

    }
}
