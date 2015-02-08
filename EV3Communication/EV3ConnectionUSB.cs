using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EV3Communication
{

    public class EV3ConnectionUSB : EV3Connection
    {
        private const UInt16 VID = 0x0694;
        private const UInt16 PID = 0x0005;

        // read/write handle to the device
        private SafeFileHandle _handle;

        // a pretty .NET stream to read/write from/to
        private FileStream _stream;

        // full-size report
        private byte[] _inputReport;
        private byte[] _outputReport;


        public EV3ConnectionUSB()
            : base()
        {
            int index = 0;
            bool found = false;
            Guid guid;

            // get the GUID of the HID class
            HidImports.HidD_GetHidGuid(out guid);

            // get a handle to all devices that are part of the HID class
            IntPtr hDevInfo = HidImports.SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, HidImports.DIGCF_DEVICEINTERFACE | HidImports.DIGCF_PRESENT);

            // create a new interface data struct and initialize its size
            HidImports.SP_DEVICE_INTERFACE_DATA diData = new HidImports.SP_DEVICE_INTERFACE_DATA();
            diData.cbSize = Marshal.SizeOf(diData);

            // get a device interface to a single device (enumerate all devices)
            while (HidImports.SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, ref guid, index, ref diData))
            {
                UInt32 size;

                // get the buffer size for this device detail instance (returned in the size parameter)
                HidImports.SetupDiGetDeviceInterfaceDetail(hDevInfo, ref diData, IntPtr.Zero, 0, out size, IntPtr.Zero);

                // create a detail struct and set its size
                HidImports.SP_DEVICE_INTERFACE_DETAIL_DATA diDetail = new HidImports.SP_DEVICE_INTERFACE_DETAIL_DATA();

                // yeah, yeah...well, see, on Win x86, cbSize must be 5 for some reason.  On x64, apparently 8 is what it wants.
                // someday I should figure this out.  Thanks to Paul Miller on this...
                diDetail.cbSize = (uint)(IntPtr.Size == 8 ? 8 : 5);

                // actually get the detail struct
                if (HidImports.SetupDiGetDeviceInterfaceDetail(hDevInfo, ref diData, ref diDetail, size, out size, IntPtr.Zero))
                {
                    // Console.WriteLine("{0}: {1} - {2}", index, diDetail.DevicePath, Marshal.GetLastWin32Error());

                    // open a read/write handle to our device using the DevicePath returned
                    _handle = HidImports.CreateFile(diDetail.DevicePath, FileAccess.ReadWrite, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, 0 /*HidImports.EFileAttributes.Overlapped*/, IntPtr.Zero);

                    // create an attributes struct and initialize the size
                    HidImports.HIDD_ATTRIBUTES attrib = new HidImports.HIDD_ATTRIBUTES();
                    attrib.Size = Marshal.SizeOf(attrib);

                    // get the attributes of the current device
                    if (HidImports.HidD_GetAttributes(_handle.DangerousGetHandle(), ref attrib))
                    {
                        // if the vendor and product IDs match up
                        if (attrib.VendorID == VID && attrib.ProductID == PID)
                        {
                            // it's a Ev3
                            // Console.WriteLine("Found brick!");
                            found = true;

                            IntPtr preparsedData;
                            if (!HidImports.HidD_GetPreparsedData(_handle.DangerousGetHandle(), out preparsedData))
                                throw new Exception("Could not get preparsed data for HID device");

                            HidImports.HIDP_CAPS caps;
                            if (HidImports.HidP_GetCaps(preparsedData, out caps) != HidImports.HIDP_STATUS_SUCCESS)
                                throw new Exception("Could not get CAPS for HID device");

                            HidImports.HidD_FreePreparsedData(ref preparsedData);

                            _inputReport = new byte[caps.InputReportByteLength];
                            _outputReport = new byte[caps.OutputReportByteLength];

                            // create a nice .NET FileStream wrapping the handle above
                            _stream = new FileStream(_handle, FileAccess.ReadWrite, _inputReport.Length);

                            break;
                        }

                        _handle.Close();
                    }
                }
                else
                {
                    // failed to get the detail struct
                    throw new Exception("SetupDiGetDeviceInterfaceDetail failed on index " + index);
                }

                // move to the next device
                index++;
            }

            // clean up our list
            HidImports.SetupDiDestroyDeviceInfoList(hDevInfo);

            // if we didn't find a EV3, throw an exception
            if (!found)
                throw new Exception("No LEGO EV3s found in HID device list.");
        }

        public override void SendPacket(byte[] data)
        {
            if (_stream != null)
            {
                // send data (leading 0 byte and 16 bit data length)
                _outputReport[1] = (byte)(data.Length & 0xff);
                _outputReport[2] = (byte)((data.Length >> 8) & 0xff);
                data.CopyTo(_outputReport, 3);
                _stream.Write(_outputReport, 0, _outputReport.Length);
            }
        }

        public override byte[] ReceivePacket()
        {
            if (_stream != null)
            {   // receive data (leading 0 byte and 16 bit data length)
                _stream.Read(_inputReport, 0, _inputReport.Length);

                short size = (short)(_inputReport[1] | _inputReport[2] << 8);
                if (size != 0)
                {
                    byte[] data = new byte[size];
                    System.Array.Copy(_inputReport, 3, data, 0, size);
                    return data;
                }
            }
            return new byte[0];
        }

        public override void Close()
        {
            if (_handle!=null)
            {
                _handle.Close();
                _handle = null;
                _stream = null;
            }
        }

    }


    internal class HidImports
    {
        //
        // Flags controlling what is included in the device information set built
        // by SetupDiGetClassDevs
        //
        public const int DIGCF_DEFAULT = 0x00000001; // only valid with DIGCF_DEVICEINTERFACE
        public const int DIGCF_PRESENT = 0x00000002;
        public const int DIGCF_ALLCLASSES = 0x00000004;
        public const int DIGCF_PROFILE = 0x00000008;
        public const int DIGCF_DEVICEINTERFACE = 0x00000010;

        [Flags]
        public enum EFileAttributes : uint
        {
            Readonly = 0x00000001,
            Hidden = 0x00000002,
            System = 0x00000004,
            Directory = 0x00000010,
            Archive = 0x00000020,
            Device = 0x00000040,
            Normal = 0x00000080,
            Temporary = 0x00000100,
            SparseFile = 0x00000200,
            ReparsePoint2F = 0x00000400,
            Compressed = 0x00000800,
            Offline = 0x00001000,
            NotContentIndexed = 0x00002000,
            Encrypted = 0x00004000,
            Write_Through = 0x80000000,
            Overlapped = 0x40000000,
            NoBuffering = 0x20000000,
            RandomAccess = 0x10000000,
            SequentialScan = 0x08000000,
            DeleteOnClose = 0x04000000,
            BackupSemantics = 0x02000000,
            PosixSemantics = 0x01000000,
            OpenReparsePoint2F = 0x00200000,
            OpenNoRecall = 0x00100000,
            FirstPipeInstance = 0x00080000
        }

        public const int HIDP_STATUS_SUCCESS = (0x00 << 28) | (0x11 << 16) | 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr RESERVED;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SP_DEVICE_INTERFACE_DETAIL_DATA
        {
            public UInt32 cbSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string DevicePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDD_ATTRIBUTES
        {
            public int Size;
            public short VendorID;
            public short ProductID;
            public short VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct HIDP_CAPS
        {
            public short Usage;
            public short UsagePage;
            public short InputReportByteLength;
            public short OutputReportByteLength;
            public short FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public short[] Reserved;
            public short NumberLinkCollectionNodes;
            public short NumberInputButtonCaps;
            public short NumberInputValueCaps;
            public short NumberInputDataIndices;
            public short NumberOutputButtonCaps;
            public short NumberOutputValueCaps;
            public short NumberOutputDataIndices;
            public short NumberFeatureButtonCaps;
            public short NumberFeatureValueCaps;
            public short NumberFeatureDataIndices;
        }

        [DllImport(@"hid.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void HidD_GetHidGuid(out Guid gHid);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern Boolean HidD_GetAttributes(IntPtr HidDeviceObject, ref HIDD_ATTRIBUTES Attributes);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetPreparsedData(IntPtr hFile, out IntPtr lpData);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern int HidP_GetCaps(IntPtr lpData, out HIDP_CAPS oCaps);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_FreePreparsedData(ref IntPtr pData);

        [DllImport("hid.dll", SetLastError = true)]
        internal extern static bool HidD_SetOutputReport(
            IntPtr HidDeviceObject,
            byte[] lpReportBuffer,
            uint ReportBufferLength);

        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(
            ref Guid ClassGuid,
            [MarshalAs(UnmanagedType.LPTStr)] string Enumerator,
            IntPtr hwndParent,
            UInt32 Flags
            );

        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern Boolean SetupDiEnumDeviceInterfaces(
            IntPtr hDevInfo,
            //ref SP_DEVINFO_DATA devInfo,
            IntPtr devInvo,
            ref Guid interfaceClassGuid,
            Int32 memberIndex,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData
        );

        [DllImport(@"setupapi.dll", SetLastError = true)]
        public static extern Boolean SetupDiGetDeviceInterfaceDetail(
            IntPtr hDevInfo,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
            IntPtr deviceInterfaceDetailData,
            UInt32 deviceInterfaceDetailDataSize,
            out UInt32 requiredSize,
            IntPtr deviceInfoData
        );

        [DllImport(@"setupapi.dll", SetLastError = true)]
        public static extern Boolean SetupDiGetDeviceInterfaceDetail(
            IntPtr hDevInfo,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
            ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData,
            UInt32 deviceInterfaceDetailDataSize,
            out UInt32 requiredSize,
            IntPtr deviceInfoData
        );

        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern UInt16 SetupDiDestroyDeviceInfoList(IntPtr hDevInfo);

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern SafeFileHandle CreateFile(
            string fileName,
            [MarshalAs(UnmanagedType.U4)] FileAccess fileAccess,
            [MarshalAs(UnmanagedType.U4)] FileShare fileShare,
            IntPtr securityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
            [MarshalAs(UnmanagedType.U4)] EFileAttributes flags,
            IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);
    }

}
