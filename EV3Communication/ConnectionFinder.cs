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
using System.Windows;
using System.Windows.Threading;
using System.Threading;
using Microsoft.Win32;


namespace EV3Communication
{
    public class ConnectionFinder
    {

        public static EV3Connection CreateConnection(bool isUIThread, bool automaticallyUseSingleUSB)
        {

            // retry multiple times to open connection
            for (; ; )
            {
                // check which EV3 devices are connected via USB
                int[] usbdevices = EV3ConnectionUSB.FindEV3s();

                // if there is exactly one, try to open it
                if (automaticallyUseSingleUSB && usbdevices.Length == 1)
                try
                {
                    return TestConnection(new EV3ConnectionUSB(usbdevices[0]));
                }
                catch (Exception)
                { }

                // when not able to open the one single USB connection, try also the serial ports
                String[] ports = System.IO.Ports.SerialPort.GetPortNames(); 

                // when no USB ports and no serial ports are available at all, show message to user any let him decide for retry
                if (usbdevices.Length<1 && ports.Length<1)
                {
                    object response = DoModalConnectionTypeDialog(isUIThread,usbdevices, ports);
                    if (response==null)
                    {
                        throw new Exception("Found no brick and no serial ports, user cancel try");
                    }
                    continue;  // user clicked retry 
                }

                // because of a strange bug in .net 3.5, sometimes the port name gets an extra letter of unpredicable content  - try to fix it in some cases
                for (int i = 0; i < ports.Length; i++)
                {
                    String n = ports[i];
                    if (n.StartsWith("COM"))
                    {
                        char last = n[n.Length - 1];        // trim away last letter if it is not a digit (but this does not always help)
                        if (last < '0' || last > '9')
                        {
                            ports[i] = n.Substring(0, n.Length - 1);
                        }
                    }
                }

                Array.Sort(ports, StringComparer.InvariantCulture);

                // Create and show the window to select one of the connection possibilities
                object port_or_device = DoModalConnectionTypeDialog(isUIThread,usbdevices, ports);

                if (port_or_device == null)
                {
                    throw new Exception("User canceled connection selection");
                }
                try
                {
                    if (port_or_device is String)
                    {
                        return TestConnection(new EV3ConnectionBluetooth((String)port_or_device));
                    }
                    else if (port_or_device is int)
                    {
                        return TestConnection(new EV3ConnectionUSB((int)port_or_device));
                    }
                }
                catch (Exception)
                { }     // if not possible, retry
            }
        }

        public static EV3Connection TestConnection(EV3Connection con)
        {
            try
            {
                // perform a tiny direct command to check if communication works
                ByteCodeBuffer c = new ByteCodeBuffer();
                c.OP(0x30);           // Move8_8
                c.CONST(74);
                c.GLOBVAR(0);
                byte[] response = con.DirectCommand(c,1,0);
                if (response==null || response.Length!=1 || response[0]!=74)
                {
                    throw new Exception("Test DirectCommand delivers wrong result");
                }
                return con;
            }
            catch (Exception e)
            {
                con.Close();
                throw e;
            }
        }

        public static object DoModalConnectionTypeDialog(bool isUIThread, int[] usbdevices, String[] ports)
        {            
            Window dialog = null;

            // simple operation when called from an UI thread
            if (isUIThread)
            {
                dialog = (usbdevices.Length > 0 || ports.Length > 0) ? (Window)new ConnectionTypeDialog(usbdevices, ports) : (Window)new NoBrickFoundDialog();
                dialog.ShowDialog();
            }
            // when being called from a non-UI-thread, must create an own thread here
            else
            {
                // Create an extra thread for the dialog window
                Thread newWindowThread = new Thread(new ThreadStart(() =>
                {
                    Window window = (usbdevices.Length > 0 || ports.Length > 0) ? (Window)new ConnectionTypeDialog(usbdevices, ports) : (Window)new NoBrickFoundDialog();
                    // When the window closes, shut down the dispatcher
                    window.Closed += (s, e) =>
                       Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                    window.Show();
                    // let other thread get hold the window to check for finish
                    dialog = window;
                    // Start the Dispatcher Processing
                    System.Windows.Threading.Dispatcher.Run();
                }));
                // Set the apartment state
                newWindowThread.SetApartmentState(ApartmentState.STA);
                // Make the thread a background thread
                newWindowThread.IsBackground = true;
                // Start the thread
                newWindowThread.Start();

                // wait here until the window actually was created and user has answered the prompt or closed the window...
                while (dialog == null || dialog.IsVisible)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            
            if (dialog is ConnectionTypeDialog)
            {
                return ((ConnectionTypeDialog)dialog).GetSelectedPort();
            }
            if (dialog is NoBrickFoundDialog)
            {
                return ((NoBrickFoundDialog)dialog).GetRetry() ? ((object) true) : ((object)null);
            }
            return null;
        }

    }
}
