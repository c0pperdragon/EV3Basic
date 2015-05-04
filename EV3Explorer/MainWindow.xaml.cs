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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Collections.ObjectModel;

using EV3Communication;
using LMSAssembler;
using EV3BasicCompiler;

namespace EV3Explorer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // connection to brick
        EV3Connection connection;

        // assembler and compiler instances
        Assembler assembler;
        Compiler compiler;

        // application data
        String ev3path;
        DirectoryInfo pcdirectory;


        // startup         
        public MainWindow()
        {
            // find connected brick
            try
            {
                connection = ConnectionFinder.CreateConnection(true,true);
            }
            catch (Exception)
            {
                System.Environment.Exit(1);
            }

            // create the compiler and assembler instances
            assembler = new Assembler();
            compiler = new Compiler();

            // initialize common data
            ev3path = "/home/root/lms2012/prjs/";
            try
            {
                pcdirectory = new DirectoryInfo(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal));
            } catch (Exception)
            { 
                pcdirectory = null;
            }

            // set up all UI controls
            InitializeComponent();

            // retrieve initial data from brick
            EV3Path.Text = ev3path;
            ReadEV3Directory(true);
            ReadEV3DeviceName();

            PCPath.Text = pcdirectory == null ? "Computer" : pcdirectory.FullName;
            RefreshPCList(true);
        }

        void Reconnect()
        {
            connection.Close();
            connection = null;

            EV3Directory.Visibility = Visibility.Hidden;
            BrickNotFound.Visibility = Visibility.Visible;

            ev3path = "/home/root/lms2012/prjs/";
            EV3Path.Text = ev3path;

            // find connected brick
            try
            {
                connection = ConnectionFinder.CreateConnection(true,false);
                ReadEV3Directory(true);
                ReadEV3DeviceName();
            }
            catch (Exception)
            {
                System.Environment.Exit(1);
            }

            EV3Directory.Visibility = Visibility.Visible;
            BrickNotFound.Visibility = Visibility.Hidden;

            AdjustDisabledStates();
        }

        // --------------- UI event handlers ---------------
        void EV3RefreshList_clicked(object sender, RoutedEventArgs e)
        {
            try
            {
                ReadEV3Directory(false);
            }
            catch (Exception)
            {
                Reconnect();
            }
        }


        private void EV3NavigateUp_clicked(object sender, RoutedEventArgs e)
        {
            try
            {            
                if (ev3path.Length > 1)
                {
                    int idx = ev3path.LastIndexOf('/', ev3path.Length - 2);
                    if (idx >= 0)
                    {
                        ev3path = ev3path.Substring(0, idx+1);
                        EV3Path.Text = ev3path;

                        ReadEV3Directory(true);
                    }
                    AdjustDisabledStates();
                }
            }
            catch (Exception)
            {
                Reconnect();
            }
        }

        private void EV3SwitchDevice_clicked(object sender, RoutedEventArgs e)
        {
            Reconnect();
        }
    
        private void DeviceName_focuslost(object sender, RoutedEventArgs e)
        {
            if (EV3DeviceName.Text.Length>0)
            {
                try
                {
                    SetEV3DeviceName(EV3DeviceName.Text);
                    ReadEV3DeviceName();
                }
                catch (Exception)
                {
                    Reconnect();
                }
            }
        }

        private void DeviceName_keydown(object sender, KeyEventArgs e)
        {   
            if (e.Key == Key.Return)
            {
                EV3Directory.Focus();
            }
        }

        private void EV3Directory_SelectionChanged(Object sender, EventArgs e)
        {
            try
            {
                DirectoryEntry de = (DirectoryEntry)EV3Directory.SelectedItem;
                if (de != null && de.IsDirectory)
                {
                    String newpath = ev3path + de.FileName + "/";
                    // prevent to navigate into folder that would lock up the brick
                    if (!newpath.Equals("/proc/"))
                    {
                        ev3path = newpath;
                        EV3Path.Text = newpath;
                        ReadEV3Directory(true);
                    }
                }
                AdjustDisabledStates();
            }
            catch (Exception)
            {
                Reconnect();
            }
        }

        private void DeleteFile_clicked(Object sender, EventArgs e)
        {
            // determine which things to delete
            List<DirectoryEntry> del = new List<DirectoryEntry>();
            foreach (DirectoryEntry de in EV3Directory.SelectedItems)
            {
                if (de!=null && !de.IsDirectory)
                { del.Add(de);
                }
            }
            if (del.Count>0)
            { 
                try
                {
                    foreach (DirectoryEntry de in del)
                    {
                        DeleteEV3File(de.FileName);
                    }
                    ReadEV3Directory(false);
                    AdjustDisabledStates();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception: " + ex.Message);
                    Reconnect();
                }
            }
        }

        private void NewFolder_clicked(Object sender, EventArgs e)
        {
            QuestionBox qb = new QuestionBox("Name of new directory:", "");
            if (qb.ShowDialog() == true)
            {
                String dirname = qb.Answer;
                try
                {
                    CreateEV3Directory(dirname);
                    ReadEV3Directory(false);
                    AdjustDisabledStates();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception: " + ex.Message);
                    Reconnect();
                }
            }
        }

        private void DeleteDirectory_clicked(Object sender, EventArgs e)
        {
            if (EV3Directory.Items.Count==0 && ev3path.Length>1)  // can only remove empty directories but not the topmost element
            { 

                try
                {
                    DeleteCurrentEV3Directory();

                    int idx = ev3path.LastIndexOf('/', ev3path.Length - 2);
                    if (idx >= 0)
                    {
                        ev3path = ev3path.Substring(0, idx + 1);
                        EV3Path.Text = ev3path;
                    }
                    ReadEV3Directory(true);
                    AdjustDisabledStates();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception: " + ex.Message);
                    Reconnect();
                }
            }
        }

        private void Upload_clicked(Object sender, EventArgs e)
        {
            // determine which things to upload
            List<DirectoryEntry> tr = new List<DirectoryEntry>();
            foreach (DirectoryEntry de in EV3Directory.SelectedItems)
            {
                if (de!=null && !de.IsDirectory)
                { tr.Add(de);
                }
            }
            if (tr.Count>0)
            {
                try
                {
                    foreach (DirectoryEntry de in tr)
                    {
                        byte[] data = null;
                        data = connection.ReadEV3File(internalPath(ev3path) + de.FileName);

                        if (data != null)
                        {
                            FileStream fs = new FileStream(pcdirectory.FullName + Path.DirectorySeparatorChar + de.FileName, FileMode.Create, FileAccess.Write);
                            fs.Write(data, 0, data.Length);
                            fs.Close();

                            RefreshPCList(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception: " + ex.Message);
                    Reconnect();
                }
            }            
        }


        void PCRefreshList_clicked(object sender, RoutedEventArgs e)
        {
            RefreshPCList(false);
        }

        private void PCNavigateUp_clicked(object sender, RoutedEventArgs e)
        {
            pcdirectory = (pcdirectory==null) ? null:pcdirectory.Parent;
            PCPath.Text = (pcdirectory==null) ? "Computer" : pcdirectory.FullName;
            RefreshPCList(true);
        }

        private void PCDirectory_SelectionChanged(Object sender, EventArgs e)
        {
            DirectoryEntry de = (DirectoryEntry)PCDirectory.SelectedItem;
            if (de!=null && de is PCDirectory)
            {
                pcdirectory = ((PCDirectory)de).directoryinfo;
                PCPath.Text = pcdirectory.FullName;
                RefreshPCList(true);
            }
            AdjustDisabledStates();
        }

        private void Download_clicked(Object sender, EventArgs e)
        {
            List<PCFile> tr = new List<PCFile>();
            foreach (DirectoryEntry de in PCDirectory.SelectedItems)
            {
                if (de!=null && (de is PCFile))
                { tr.Add((PCFile)de);
                }
            }
            if (tr.Count>0)
            {
                try {
                    foreach (PCFile pcfile in tr)
                    {
                        FileInfo fi = pcfile.fileinfo;

                        byte[] content = new byte[fi.Length];
                        FileStream fs = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read);
                        int pos = 0;
                        while (pos < content.Length)
                        {
                            int didread = fs.Read(content, pos, content.Length - pos);
                            if (didread <= 0)
                            {
                                throw new Exception("Unexpected end of file");
                            }
                            pos += didread;
                        }
                        fs.Close();

                        connection.CreateEV3File(internalPath(ev3path) + fi.Name, content);
                    }
                    ReadEV3Directory(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception: " + ex.Message);
                    Reconnect();
                }
                AdjustDisabledStates();
            }
        }

        private void Compile_clicked(Object sender, EventArgs e)
        {
            CompileAndDownload(false);
        }

        private void CompileAndRun_clicked(Object sender, EventArgs e)
        {
            CompileAndDownload(true);
        }



        private void CompileAndDownload(bool run)
        {
            DirectoryEntry de = (DirectoryEntry)PCDirectory.SelectedItem;
            if (de != null && (de is PCFile))
            {
                FileInfo pcfile = ((PCFile)de).fileinfo;

                byte[] content = null;
                String targetfilename = null;
//       Console.WriteLine("compile clicked for: " + pcfile.Name);

                if (pcfile.Name.EndsWith(".lms", StringComparison.InvariantCultureIgnoreCase))
                {
                    targetfilename = pcfile.Name.Substring(0, pcfile.Name.Length - 4) + ".rbf";
                    List<String> errors = new List<String>();

                    try
                    {                        
                        FileStream fs = new FileStream(pcfile.FullName, FileMode.Open, FileAccess.Read);
                        MemoryStream ms = new MemoryStream();

//                        Console.WriteLine("starting assembler for: " + pcfile.FullName+"  target="+targetfilename);
                        assembler.Assemble(fs, ms, errors);
                        fs.Close();

                        if (errors.Count == 0)
                        {
                            content = ms.ToArray();
                        }
                        else
                        {
                            Console.WriteLine("finished with " + errors.Count + " errors");
                            foreach (String s in errors)
                            { Console.WriteLine(s); }
                            ShowErrorMessages(errors);
                        }
                    }
                    catch (Exception)
                    { }
                }
                else if (pcfile.Name.EndsWith(".sb", StringComparison.InvariantCultureIgnoreCase))
                {
                    targetfilename = pcfile.Name.Substring(0, pcfile.Name.Length - 3) + ".rbf";
                    List<String> errors = new List<String>();

                    try
                    { 
                        FileStream fs = new FileStream(pcfile.FullName, FileMode.Open, FileAccess.Read);
                        MemoryStream ms1 = new MemoryStream();
                        MemoryStream ms = new MemoryStream();

                        compiler.Compile(fs, ms1, errors);
                        fs.Close();

                        ms1.Position = 0;
                        assembler.Assemble(ms1, ms, errors);

                        if (errors.Count == 0)
                        {
                            content = ms.ToArray();
                        }
                        else
                        {
                            Console.WriteLine("finished with " + errors.Count + " errors");
                            foreach (String s in errors)
                            { Console.WriteLine(s); }
                            ShowErrorMessages(errors);
                        }
                    }
                    catch (Exception)
                    { }

                }

                if (content != null)
                {
                    try
                    {
                        connection.CreateEV3File(internalPath(ev3path) + targetfilename, content);
                        ReadEV3Directory(false);
                        if (run)
                        {
                            RunEV3File(targetfilename);
                        }
                        AdjustDisabledStates();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception: " + ex.Message);
                        Reconnect();
                    }                                
                }
            }
        }


        private void RefreshPCList(bool resetposition)
        {
            PCDirectory.Items.Clear();
            try
            {
                FileSystemInfo[] infos;

                if (pcdirectory != null)
                {
                    infos = pcdirectory.GetFileSystemInfos();
                }
                else
                {
                    DriveInfo[] di = DriveInfo.GetDrives();
                    infos = new FileSystemInfo[di.Length];
                    for (int i = 0; i < di.Length; i++)
                    {
                        infos[i] = di[i].RootDirectory;
                    }
                }

                foreach (FileSystemInfo info in infos)
                {
                    if (info is FileInfo)
                    {
                        PCDirectory.Items.Add(new PCFile((FileInfo)info));
                    }
                    else if (info is DirectoryInfo)
                    {
                        PCDirectory.Items.Add(new PCDirectory((DirectoryInfo)info));
                    }
                }
            }
            catch (Exception)
            { }

            // let the WPF system re-calculate all column widths so everthing fits as good as possible
            foreach (var gvc in PCDirectoryGridView.Columns)
            {
                gvc.Width = gvc.ActualWidth;
                gvc.Width = Double.NaN;
            }

            // move the controls scroller to top position
            if (resetposition)
            {
                if(PCDirectory.Items.Count > 0)
                {
                    PCDirectory.ScrollIntoView(PCDirectory.Items[0]);
                }
            }

            AdjustDisabledStates();
        }

        // ---------- perform enabling/disabling of buttons and such

        private void AdjustDisabledStates()
        {
            DirectoryEntry de = (DirectoryEntry)EV3Directory.SelectedItem;                    
            EV3Directory.IsEnabled = true;
            EV3NavigateUp.IsEnabled = !ev3path.Equals("/");
            BrickNotFound.Visibility = Visibility.Hidden;
            DeleteFile.IsEnabled = de != null && !de.IsDirectory;
            DeleteDirectory.IsEnabled = EV3Directory.Items.Count == 0;
            NewFolder.IsEnabled = true;
            Upload.IsEnabled = de != null && !de.IsDirectory;

            de = (DirectoryEntry)PCDirectory.SelectedItem;
            PCNavigateUp.IsEnabled = pcdirectory != null;
            Download.IsEnabled = de != null && !de.IsDirectory;
            Compile.IsEnabled = de != null && de.IsCompileable && PCDirectory.SelectedItems.Count==1;
            CompileAndRun.IsEnabled = de != null && de.IsCompileable && PCDirectory.SelectedItems.Count == 1; 
        }


        // -------------- window showing error messages ------------

        private void ShowErrorMessages(List<String> lines)
        {
            String s = "";
            foreach (String l in lines)
            {
                if (s.Length == 0)
                {
                    s = l;
                }
                else
                {
                    s = s + "/n" + l;
                }
            }
            MessageBox.Show(s, "Compile errors"); 
        }



        // -------------- do the communication with the brick --------------------

//        private const String basepath = "/.";   // ".."

        private String internalPath(String absolutePath)
        {
            if (absolutePath.StartsWith("/home/root/lms2012/"))
            {
                return ".." + absolutePath.Substring(18);
            }
            else
            {
                return "/." + absolutePath;
            }

        }

        private void ReadEV3Directory(bool resetposition)
        {
            MemoryStream data = new MemoryStream();

//            if (!ev3path.Equals("/proc/"))       // avoid locking up the brick
            {
                // get data from brick
                BinaryBuffer b = new BinaryBuffer();
                b.Append16(500);  // expect max 500 bytes per packet
                b.AppendZeroTerminated(internalPath(ev3path));
                byte[] response = connection.SystemCommand(EV3Connection.LIST_FILES, b);

                if (response == null)
                {
                    throw new Exception("No response to LIST_FILES");
                }
                if (response.Length < 6)
                {
                    throw new Exception("Response too short for LIST_FILES");
                }
                if (response[0] != EV3Connection.SUCCESS && response[0] != EV3Connection.END_OF_FILE)
                {
                    throw new Exception("Unexpected status at LIST_FILES: " + response[0]);
                }
                int handle = response[5] & 0xff;
                data.Write(response, 6, response.Length - 6);

                // continue reading until have total buffer
                while (response[0] != EV3Connection.END_OF_FILE)
                {
                    b.Clear();
                    b.Append8(handle);
                    b.Append16(500);  // expect max 500 bytes per packet
                    response = connection.SystemCommand(EV3Connection.CONTINUE_LIST_FILES, b);
                    //                    Console.WriteLine("follow-up response length: " + response.Length);

                    if (response == null)
                    {
                        throw new Exception("No response to CONTINUE_LIST_FILES");
                    }
                    if (response.Length < 2)
                    {
                        throw new Exception("Too short response to CONTINUE_LIST_FILES");
                    }
                    if (response[0] != EV3Connection.SUCCESS && response[0] != EV3Connection.END_OF_FILE)
                    {
                        throw new Exception("Unexpected status at CONTINUE_LIST_FILES: " + response[0]);
                    }
                    //                    Console.WriteLine("subsequent response length: " + response.Length);
                    data.Write(response, 2, response.Length - 2);
                }
            }

            List<DirectoryEntry> list = new List<DirectoryEntry>();

            data.Position = 0;  // start reading at beginning
            StreamReader tr = new StreamReader(data, Encoding.GetEncoding("iso-8859-1"));
            String l;
            while ((l = tr.ReadLine()) != null)
            {
                if (l.EndsWith("/"))
                {
                    String n = l.Substring(0, l.Length - 1);
                    if ((!n.Equals(".")) && (!n.Equals("..")))
                    {
                        list.Add(new DirectoryEntry(n, 0, true));
                    }
                }
                else
                {
                    int firstspace = l.IndexOf(' ');
                    if (firstspace < 0)
                    {
                        continue;
                    }
                    int secondspace = l.IndexOf(' ', firstspace + 1);
                    if (secondspace < 0)
                    {
                        continue;
                    }
                    int size = int.Parse(l.Substring(firstspace, secondspace - firstspace).Trim(), System.Globalization.NumberStyles.HexNumber);

                    list.Add(new DirectoryEntry(l.Substring(secondspace + 1), size, false));
                }
            }

            // sort list
            list.Sort((x, y) => x.FileName.CompareTo(y.FileName));

            // put data into listview
            EV3Directory.Items.Clear();
            foreach (DirectoryEntry de in list)
            {
                EV3Directory.Items.Add(de);
            }       
        
            // let the WPF system re-calculate all column widths so everthing fits as good as possible
            foreach (var gvc in EV3DirectoryGridView.Columns)
            {
                gvc.Width = gvc.ActualWidth;
                gvc.Width = Double.NaN;
            }

            // move the controls scroller to top position
            if (resetposition)
            {
                if (EV3Directory.Items.Count > 0)
                {
                    EV3Directory.ScrollIntoView(EV3Directory.Items[0]);
                }
            }

        }

        private void ReadEV3DeviceName()
        {
            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xD3);         // Com_Get
            c.CONST(0x0D);      // GET_BRICKNAME = 0x0D
            c.CONST(127);       // maximum string length
            c.GLOBVAR(0);       // where to store name
            byte[] response = connection.DirectCommand(c, 128, 0);

            String devicename = "?";
            if (response != null && response.Length > 1)
            {
                // find the null-termination
                for (int len = 0; len < response.Length; len++)
                {
                    if (response[len] == 0)
                    {
                        // extract the message text
                        char[] msg = new char[len];
                        for (int i = 0; i < len; i++)
                        {
                            msg[i] = (char)response[i];
                        }
                        devicename = new String(msg);
                        break;
                    }
                }
            }

            EV3DeviceName.Text = devicename;
        }

        private void SetEV3DeviceName(String name)
        {
            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xD4);         // Com_Set
            c.CONST(0x08);      // SET_BRICKNAME = 0x08
            c.STRING(name); 
            connection.DirectCommand(c, 0, 0);
        }


        private void DeleteEV3File(String filename)
        {
            BinaryBuffer b = new BinaryBuffer();
            b.AppendZeroTerminated(internalPath(ev3path) + filename);
            connection.SystemCommand(EV3Connection.DELETE_FILE, b); 
        }


        private void DeleteCurrentEV3Directory()
        {
            BinaryBuffer b = new BinaryBuffer();
            b.AppendZeroTerminated(internalPath(ev3path));
            connection.SystemCommand(EV3Connection.DELETE_FILE, b); 
        }

        private void CreateEV3Directory(String directoryname)
        {
            BinaryBuffer b = new BinaryBuffer();
            b.AppendZeroTerminated(internalPath(ev3path) + directoryname);
            connection.SystemCommand(EV3Connection.CREATE_DIR, b);
        }


        private void RunEV3File(String filename)
        {
            String fullname = internalPath(ev3path) + filename;
            Console.WriteLine("Trying to start: " + fullname);

            ByteCodeBuffer c = new ByteCodeBuffer();

            // load and start it
            c.OP(0xC0);       // opFILE
            c.CONST(0x08);    // CMD: LOAD_IMAGE = 0x08
            c.CONST(1);       // slot 1 = user program slot
            c.STRING(fullname);
            c.GLOBVAR(0);
            c.GLOBVAR(4);
            c.OP(0x03);       // opPROGRAM_START
            c.CONST(1);       // slot 1 = user program slot
            c.GLOBVAR(0);
            c.GLOBVAR(4);
            c.CONST(0);

            connection.DirectCommand(c, 10, 0);
        }

    }                            
}
