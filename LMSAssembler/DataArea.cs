using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LMSAssembler
{
    // specifies one local or global data element
    public class DataElement
    {
        public readonly String name;
        public readonly int position;
        public readonly DataType datatype;

        public DataElement(String n, int p, DataType dt)
        {
            name = n;
            position = p;
            datatype = dt;
        }
        public override string ToString()
        {
            return position + " " + name + " " + datatype;
        }
    }

    // one continous space of data elements. 
    // can be either global or a local data area for a thread or a subroutine
    public class DataArea
    {
        private Dictionary<String, DataElement> elements;
        private int endofarea;        // total bytes already in use
        bool haveNonParameters;

        public DataArea()
        {
            elements = new Dictionary<String, DataElement>();
            endofarea = 0;
            haveNonParameters = false;
        }

        public void Add(String name, int length, int number, DataType datatype, bool isParameter)
        {
            if (elements.ContainsKey(name))
            {
                throw new AssemblerException("Identifier " + name + " already in use");
            }

            if (!isParameter)
            {
                haveNonParameters = true;
            }
            else if (haveNonParameters)
            {
                throw new AssemblerException("Can not place IN,OUT,IO elements after DATA elements");
            }

            while (endofarea%length != 0)    // check if alignment fits
            {
                if (isParameter)      // parameters must never be padded
                {
                    throw new AssemblerException("Can not insert padding for propper alignment. Try to reorder the IO parameters that no padding is necessary");
                }
                endofarea++;
            }            

            elements[name] = new DataElement(name, endofarea, datatype);
            endofarea += length*number;
        }

        public int TotalBytes()
        {
            return endofarea;
        }

        public DataElement Get(String name)
        {
            if (!elements.ContainsKey(name))
            {
                return null;
            }
            return elements[name];
        }

        public void print()
        {
            foreach (DataElement el in elements.Values)
            {
                Console.WriteLine(el);
            }
        }
    }



}
