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
using System.Windows.Shapes;
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
        // assembler and compiler instances
        Assembler assembler;
        Compiler compiler;


        // application data
        bool brickavailable;
        String ev3path;
        DirectoryInfo pcdirectory;


        // startup         
        public MainWindow()
        {
            // create the compiler and assembler instances
            assembler = new Assembler();
            compiler = new Compiler();


            // initialize common data
            brickavailable = false;
            ev3path = "/prjs/";
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
            EV3RefreshList_clicked(null, null);

            PCPath.Text = pcdirectory == null ? "Computer" : pcdirectory.FullName;
            PCRefreshList_clicked(null, null);
        }


        // --------------- UI event handlers ---------------
        void EV3RefreshList_clicked(object sender, RoutedEventArgs e)
        {
            try
            {
                EV3Connection c = new EV3ConnectionUSB();
                try
                {
                    ReadEV3Directory(c);
                }
                finally
                {
                    c.Close();
                }
                brickavailable = true;
            }
            catch (Exception ex)
            {
                brickavailable = false;
                Console.WriteLine("Exception: " + ex.Message);
            }
            AdjustDisabledStates();
        }


        private void EV3NavigateUp_clicked(object sender, RoutedEventArgs e)
        {
            if (ev3path.Length > 1)
            {
                int idx = ev3path.LastIndexOf('/', ev3path.Length - 2);
                if (idx >= 0)
                {
                    ev3path = ev3path.Substring(0, idx+1);
                    EV3Path.Text = ev3path;

                    EV3RefreshList_clicked(null, null);
                }
                AdjustDisabledStates();
            }
        }

        private void EV3Directory_SelectionChanged(Object sender, EventArgs e)
        {
            DirectoryEntry de = (DirectoryEntry) EV3Directory.SelectedItem;
            if (de!=null && de.IsDirectory)
            {
                ev3path = ev3path + de.FileName + "/";
                EV3Path.Text = ev3path;

                EV3RefreshList_clicked(null, null);
            }
            AdjustDisabledStates();
        }

        private void DeleteFile_clicked(Object sender, EventArgs e)
        {
            DirectoryEntry de = (DirectoryEntry) EV3Directory.SelectedItem;
            if (de!=null && !de.IsDirectory)
            {
                try
                {
                    EV3Connection c = new EV3ConnectionUSB();
                    try
                    {
                        DeleteEV3File(c, de.FileName);
                        ReadEV3Directory(c);
                    }
                    finally
                    {
                        c.Close();
                    }
                    brickavailable = true;
                }
                catch (Exception ex)
                {
                    brickavailable = false;
                    Console.WriteLine("Exception: " + ex.Message);
                }
                AdjustDisabledStates();
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
                    EV3Connection c = new EV3ConnectionUSB();
                    try
                    {
                        CreateEV3Directory(c, dirname);
                        ReadEV3Directory(c);
                    }
                    finally
                    {
                        c.Close();
                    }
                    brickavailable = true;
                }
                catch (Exception ex)
                {
                    brickavailable = false;
                    Console.WriteLine("Exception: " + ex.Message);
                }
                AdjustDisabledStates();
            }
        }

        private void DeleteDirectory_clicked(Object sender, EventArgs e)
        {
            if (EV3Directory.Items.Count==0 && ev3path.Length>1)  // can only remove empty directories but not the topmost element
            { 

                try
                {
                    EV3Connection c = new EV3ConnectionUSB();
                    try
                    {
                        DeleteCurrentEV3Directory(c);

                        int idx = ev3path.LastIndexOf('/', ev3path.Length - 2);
                        if (idx >= 0)
                        {
                            ev3path = ev3path.Substring(0, idx + 1);
                            EV3Path.Text = ev3path;
                        }

                        ReadEV3Directory(c);
                    }
                    finally
                    {
                        c.Close();
                    }
                    brickavailable = true;
                }
                catch (Exception ex)
                {
                    brickavailable = false;
                    Console.WriteLine("Exception: " + ex.Message);
                }
                AdjustDisabledStates();
            }
        }



        void PCRefreshList_clicked(object sender, RoutedEventArgs e)
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
                    for (int i=0; i<di.Length; i++)
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
            AdjustDisabledStates();
        }

        private void PCNavigateUp_clicked(object sender, RoutedEventArgs e)
        {
            pcdirectory = (pcdirectory==null) ? null:pcdirectory.Parent;
            PCPath.Text = (pcdirectory==null) ? "Computer" : pcdirectory.FullName;
            PCRefreshList_clicked(null, null);
        }

        private void PCDirectory_SelectionChanged(Object sender, EventArgs e)
        {
            DirectoryEntry de = (DirectoryEntry)PCDirectory.SelectedItem;
            if (de!=null && de is PCDirectory)
            {
                pcdirectory = ((PCDirectory)de).directoryinfo;
                PCPath.Text = pcdirectory.FullName;
                PCRefreshList_clicked(null, null);
            }
            AdjustDisabledStates();
        }

        private void Download_clicked(Object sender, EventArgs e)
        {
            DirectoryEntry de = (DirectoryEntry)PCDirectory.SelectedItem;
            if (de != null && (de is PCFile))
            {
                FileInfo pcfile = ((PCFile)de).fileinfo;

                byte[] content = new byte[pcfile.Length];
                FileStream fs = new FileStream(pcfile.FullName, FileMode.Open, FileAccess.Read);
                int pos = 0;
                while (pos < content.Length)
                {
                    int didread = fs.Read(content, pos, content.Length - pos);
                    if (didread<=0)
                    {
                        throw new Exception("Unexpected end of file");
                    }
                    pos += didread;
                }
                fs.Close();


                try
                {
                    EV3Connection c = new EV3ConnectionUSB();
                    try
                    {
                        CreateEV3File(c, pcfile.Name, content);
                        ReadEV3Directory(c);
                    }
                    finally
                    {
                        c.Close();
                    }
                    brickavailable = true;
                }
                catch (Exception ex)
                {
                    brickavailable = false;
                    Console.WriteLine("Exception: " + ex.Message);
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
                        EV3Connection c = new EV3ConnectionUSB();
                        try
                        {
                            CreateEV3File(c, targetfilename, content);
                            ReadEV3Directory(c);
                            if (run)
                            {
                                RunEV3File(c, targetfilename);
                            }
                        }
                        finally
                        {
                            c.Close();
                        }
                        brickavailable = true;
                    }
                    catch (Exception ex)
                    {
                        brickavailable = false;
                        Console.WriteLine("Exception: " + ex.Message);
                    }                                
                }
                AdjustDisabledStates();
            }
        }






        // ---------- perform enabling/disabling of buttons and such

        private void AdjustDisabledStates()
        {
            if (!brickavailable)
            {
                // no brick found
                EV3Directory.Items.Clear();
                EV3Directory.IsEnabled = false;
                EV3NavigateUp.IsEnabled = false;
                BrickNotFound.Visibility = Visibility.Visible;
                DeleteFile.IsEnabled = false;
                DeleteDirectory.IsEnabled = false;
                NewFolder.IsEnabled = false;
                Download.IsEnabled = false;
                Compile.IsEnabled = false;
                CompileAndRun.IsEnabled = false;
            }
            else
            {
                DirectoryEntry de = (DirectoryEntry)EV3Directory.SelectedItem;                    
                EV3Directory.IsEnabled = true;
                EV3NavigateUp.IsEnabled = true;
                BrickNotFound.Visibility = Visibility.Hidden;
                DeleteFile.IsEnabled = de != null && !de.IsDirectory;
                DeleteDirectory.IsEnabled = EV3Directory.Items.Count == 0;
                NewFolder.IsEnabled = true;
                de = (DirectoryEntry)PCDirectory.SelectedItem;
                Download.IsEnabled = de != null && de.IsDirectory;
                Compile.IsEnabled = de != null && de.IsCompileable;
                CompileAndRun.IsEnabled = de != null && de.IsCompileable;
            }
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

        private const String basepath = "..";

        private void ReadEV3Directory(EV3Connection con)
        {
            MemoryStream data = new MemoryStream();

            // get data from brick
                BinaryBuffer b = new BinaryBuffer();
                b.Append16(500);  // expect max 500 bytes per packet
                b.AppendZeroTerminated(basepath+ev3path);
//                b.AppendNonZeroTerminated("./" + ev3path);
                byte[] response = con.SystemCommand(EV3Connection.LIST_FILES, b); 

                if (response == null)
                {
                    throw new Exception("No response to LIST_FILES");
                }
                if (response.Length < 6)
                {
                    throw new Exception("Response too short for LIST_FILES");
                }
                if (response[0] != EV3Connection.SUCCESS && response[0]!=EV3Connection.END_OF_FILE)   
                {
                    throw new Exception("Unexpected status at LIST_FILES: "+response[0]);
                }
//                Console.WriteLine("initial response length: " + response.Length);
                int handle = response[5] & 0xff;
                data.Write(response, 6, response.Length - 6);

                // continue reading until have total buffer
                for (;;)
                {
                    b.Clear();
                    b.Append8(handle);
                    b.Append16(500);  // expect max 500 bytes per packet
                    response = con.SystemCommand(EV3Connection.CONTINUE_LIST_FILES, b);

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
                        throw new Exception("Unexpected status at CONTINUE_LIST_FILES: "+response[0]);
                    }
//                    Console.WriteLine("subsequent response length: " + response.Length);
                    data.Write(response, 2, response.Length - 2);
                    
                    if (response[0] == EV3Connection.END_OF_FILE)
                    { break; }
                }
                
                List<DirectoryEntry> list = new List<DirectoryEntry>();

                data.Position = 0;  // start reading a beginning
                StreamReader tr = new StreamReader(data, Encoding.ASCII);
                String l;
                while ((l = tr.ReadLine()) != null)
                {
//                    Console.WriteLine("line found: " + l);
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
                
        }

        private void DeleteEV3File(EV3Connection con, String filename)
        {
            BinaryBuffer b = new BinaryBuffer();
            b.AppendZeroTerminated(basepath+ev3path+filename);
            con.SystemCommand(EV3Connection.DELETE_FILE, b); 
        }


        private void DeleteCurrentEV3Directory(EV3Connection con)
        {
            BinaryBuffer b = new BinaryBuffer();
            b.AppendZeroTerminated(basepath + ev3path);
            con.SystemCommand(EV3Connection.DELETE_FILE, b); 
        }

        private void CreateEV3Directory(EV3Connection con, String directoryname)
        {
            BinaryBuffer b = new BinaryBuffer();
            b.AppendZeroTerminated(basepath + ev3path+ directoryname);
            con.SystemCommand(EV3Connection.CREATE_DIR, b);
        }


        private void CreateEV3File(EV3Connection con, String filename, byte[] content)
        {
            int chunksize = 500;
            int pos = 0;
            int transfernow = Math.Min(content.Length - pos, chunksize - filename.Length);
            String fullname = basepath + ev3path + filename;
            
            Console.WriteLine("Transfering " + content.Length + " bytes to " + fullname);

            // start the transfer
            BinaryBuffer b = new BinaryBuffer();
            b.Append32(content.Length);
            b.AppendZeroTerminated(fullname);
            b.AppendBytes(content, pos, transfernow);


            byte[] response = con.SystemCommand(EV3Connection.BEGIN_DOWNLOAD, b);

            if (response == null)
            {
                throw new Exception("No response to BEGIN_DOWNLOAD");
            }
            if (response.Length < 2)
            {
                throw new Exception("Response too short for BEGIN_DOWNLOAD");
            }
            if (response[0] != EV3Connection.SUCCESS && response[0] != EV3Connection.END_OF_FILE)
            {
                throw new Exception("Unexpected status at BEGIN_DOWNLOAD: " + response[0]);
            }

            pos += transfernow;

            int handle = response[1] & 0xff;

            // transfer bytes in small chunks
            while (pos<content.Length)
            {
                transfernow = Math.Min(content.Length - pos, chunksize);
                b.Clear();
                b.Append8(handle);
                b.AppendBytes(content, pos, transfernow);
                response = con.SystemCommand(EV3Connection.CONTINUE_DOWNLOAD, b);

                if (response == null)
                {
                    throw new Exception("No response to CONTINUE_DOWNLOAD");
                }
                if (response.Length < 2)
                {
                    throw new Exception("Response too short for CONTINUE_DOWNLOAD");
                }
                if (response[0] != EV3Connection.SUCCESS && response[0] != EV3Connection.END_OF_FILE)
                {
                    throw new Exception("Unexpected status at CONTINUE_DOWNLOAD: " + response[0]);
                }

                pos += transfernow;
            }
        }

        private void RunEV3File(EV3Connection con, String filename)
        {
            String fullname = basepath + ev3path + filename;
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

            con.DirectCommand(c, 10, 0);
        }

    }                            
}
