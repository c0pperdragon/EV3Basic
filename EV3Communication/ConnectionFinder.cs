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


namespace EV3Communication
{
    public class ConnectionFinder
    {
        static EV3Connection reuse = null;

        public static EV3Connection CreateConnection()
        {
            // check if can re-use connection
            if (reuse!=null)
            {
                EV3Connection c = reuse;
                reuse = null;
                return c;
            }

            // first try to get an USB connection
            try
            {
                return new EV3ConnectionUSB();
            }
            catch (Exception)
            { }
            

            // when no USB connection available, check the serial ports
            String[] ports = System.IO.Ports.SerialPort.GetPortNames();
            for (int i = 0; i < ports.Length; i++)
            {
                // strange problem: Port names seem to end with an 'o'?
                if (ports[i].EndsWith("o"))
                {
                    ports[i] = ports[i].Substring(0, ports[i].Length - 1);
                }
            }
            // when no serial ports are available at all, stop try
            if (ports.Length<1)
            {
                throw new Exception("Found no brick and no serial ports");
            }


            // when there are serial ports, let user decide
            Array.Sort(ports, StringComparer.InvariantCulture);
            ConnectionTypeDialog dialog = null;
            // Create an extra thread for the dialog window
            Thread newWindowThread = new Thread(new ThreadStart(() =>
            {
                    // Create and show the Window
                    var window = new ConnectionTypeDialog(ports);
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
            while (dialog==null || dialog.IsVisible)
            {
                System.Threading.Thread.Sleep(100);
            }

            String port = dialog.GetSelectedPort();
            if (port == null)
            {
                throw new Exception("User did not select serial port");
            }

            return new EV3ConnectionBluetooth(port);
        }

        public static void CloseConnection(EV3Connection c)
        {
            if (reuse!=null)
            {
                reuse.Close();
                reuse = null;
            }
            // keep open bluetooth connections for further use
            if (c is EV3ConnectionBluetooth && c.IsOpen())
            {
                reuse = c;
            }
            else
            {
                c.Close();
            }
        }
    }
}
