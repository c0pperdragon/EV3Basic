using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using EV3BasicCompiler;
using LMSAssembler;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            TestCompile();
            TestAssemble();
        }

        static void TestCompile()
        {    
            String f = "C:/Users/Reinhard/Google Drive/csharp/EV3Basic/testsuite/ev3features/SensorReading.sb";
            FileStream fs = new FileStream(f, FileMode.Open, FileAccess.Read);
            FileStream ofs = new FileStream("c:/temp/compiledbasic.lms", FileMode.Create, FileAccess.Write);

            List<String> errors = new List<String>();

            try
            {
                Compiler c = new Compiler();      
                c.Compile(fs, ofs, errors);

                ofs.Close();
                fs.Close();

                if (errors.Count > 0)
                {
                    foreach (String s in errors)
                    {
                        Console.WriteLine(s);
                    }
                    Console.ReadKey();
                }

            }
            catch (Exception e)
            {
                ofs.Close();
                fs.Close();
                Console.WriteLine("Compiler error: "+e.Message);
                Console.WriteLine(e.StackTrace);
                Console.ReadKey();
            }
        }

        static void TestAssemble()
        {
            Assembler a = new Assembler();

            String f = "C:/temp/compiledbasic.lms";
            FileStream fs = new FileStream(f, FileMode.Open, FileAccess.Read);

            FileStream ofs = new FileStream("c:/temp/compiledbasic.rbf", FileMode.Create, FileAccess.Write);

            List<String> errors = new List<String>();

            a.Assemble(fs, ofs, errors);

            fs.Close();
            ofs.Close();

            if (errors.Count>0)
            {
                foreach (String s in errors)
                {   Console.WriteLine(s);
                }
                Console.ReadKey();
            }
        }

    }
}
