using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using EV3BasicCompiler;
using LMSAssembler;
using System.Net.Sockets;
using System.Net;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
//            TestWiFiReceiveBroadcast();
            TestWiFi();
//            TestCompile();
//            TestAssemble();
//            TestDisassemble();
        }

        static void TestDisassemble()
        {
            Assembler a = new Assembler();

            String f = "C:/temp/Program.rbf";
            FileStream fs = new FileStream(f, FileMode.Open, FileAccess.Read);

            a.Disassemble(fs, Console.Out);

            Console.ReadKey();
        }

        static void TestCompile()
        {    
            String f = "C:/Users/Reinhard/Documents/GitHub/EV3Basic/Examples/TowersOfHanoi.sb";
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


        static void TestWiFi()
        {
            Console.WriteLine("Connecting...");
            TcpClient c = new TcpClient("10.0.0.140", 5555);
            Console.WriteLine("Connected!");
            NetworkStream s = c.GetStream();
            Console.WriteLine("Sending data...");
            byte[] data = System.Text.UTF8Encoding.UTF8.GetBytes("GET /target?sn=0016533F0C1E VMTP1.0\r\nProtocol: EV3\r\n\r\n");
//            byte[] data = System.Text.UTF8Encoding.UTF8.GetBytes("X");
            s.Write(data, 0, data.Length);

            for (; ; )
            {
                int b = s.ReadByte();
                if (b < 0) break;
                Console.WriteLine(b);
            }
            
            s.Close();
            c.Close();
        }

        static void TestWiFiReceiveBroadcast()
        {
            Console.WriteLine("Opening receiving UDP port...");               
            UdpClient c = new UdpClient(3015);

            IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

            Console.WriteLine("Receiving incomming packets...");
            for (;;)
            {
                byte[] data = c.Receive(ref RemoteIpEndPoint);
                Console.WriteLine("Received: "+data.Length+ "bytes");
                Console.WriteLine(System.Text.UTF8Encoding.UTF8.GetString(data));
            }

            c.Close();
        }

    }
}
