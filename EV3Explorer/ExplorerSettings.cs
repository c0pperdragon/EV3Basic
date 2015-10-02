using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace EV3Explorer
{
    public class ExplorerSettings
    {
        public int windowWidth;
        public int windowHeight;
        public int splitterPosition;
        public String localDirectory;
        public Boolean onlyShowPrograms;

        public ExplorerSettings()
        {
            windowWidth = 800;
            windowHeight = 600;
            splitterPosition = 400;
            localDirectory = "";
            onlyShowPrograms = false;
        }

        public void Load()
        {
            try
            {
                string fileName = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EV3Basic"), "settings.txt");
                System.IO.StreamReader file = new System.IO.StreamReader(fileName);
                String line;
                while ((line = file.ReadLine()) != null)
                {
                    line = line.Trim();
                    try
                    {
                        if (line.StartsWith("WIDTH=", StringComparison.InvariantCultureIgnoreCase))
                        {
                            Int32.TryParse(line.Substring(6).Trim(), out windowWidth);
                        }
                        if (line.StartsWith("HEIGHT=", StringComparison.InvariantCultureIgnoreCase))
                        {
                            Int32.TryParse(line.Substring(7).Trim(), out windowHeight);
                        }
                        if (line.StartsWith("SPLITTER=", StringComparison.InvariantCultureIgnoreCase))
                        {
                            Int32.TryParse(line.Substring(9).Trim(), out splitterPosition);
                        }
                        if (line.StartsWith("LOCALDIR=", StringComparison.InvariantCultureIgnoreCase))
                        {
                            localDirectory = line.Substring(9).Trim();
                        }
                        if (line.StartsWith("ONLYSHOWPROGRAMS", StringComparison.InvariantCultureIgnoreCase))
                        {
                            onlyShowPrograms = true;
                        }
                    }
                    catch (Exception) { }
                }
                file.Close();
            }
            catch (Exception) { }
        }

        public void Save()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EV3Basic");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string fileName = Path.Combine(dir, "settings.txt");
                System.IO.StreamWriter file = new System.IO.StreamWriter(fileName);
                file.WriteLine("WIDTH="+windowWidth);
                file.WriteLine("HEIGHT=" + windowHeight);
                file.WriteLine("SPLITTER=" + splitterPosition);
                file.WriteLine("LOCALDIR=" + localDirectory);
                if (onlyShowPrograms)
                {
                    file.WriteLine("ONLYSHOWPROGRAMS");
                }
                file.Close();
            }
            catch (Exception) { }
        }

    }
}
