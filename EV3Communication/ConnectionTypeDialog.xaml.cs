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
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;

namespace EV3Communication
{
    /// <summary>
    /// Interaction logic for ConnectionTypeDialog.xaml
    /// </summary>
    public partial class ConnectionTypeDialog : Window
    {
        private int[] usbdevices;
        private String[] ports;
        private IPAddress[] addresses;
        private object selected;

        public ConnectionTypeDialog(int[] usbdevices, String[] ports, IPAddress[] addresses, String preferred)
        {
            this.usbdevices = usbdevices;
            this.ports = ports;
            this.addresses = addresses;
            this.selected = null;

            int pref = 0;

            InitializeComponent();

            foreach (int i in usbdevices)
            {
                String txt = "USB " + i;
                if (txt.Equals(preferred))
                {
                    pref = PortList.Items.Count;
                }
                PortList.Items.Add(txt);
            }
            foreach (String p in ports)
            {
                if (p.Equals(preferred))
                {
                    pref = PortList.Items.Count;
                }
                PortList.Items.Add(p);
            }
            foreach (IPAddress a in addresses)
            {
                String txt = a.ToString();
                if (txt.Equals(preferred))
                {
                    pref = PortList.Items.Count;
                }
                PortList.Items.Add(txt);
            }

            if (PortList.Items.Count>0)
            {
                PortList.Focus();
                PortList.SelectedIndex = pref;
            }
        }

        public object GetSelectedPort()
        {
            return selected;
        }

        private void CancelButton_clicked(object sender, System.Windows.RoutedEventArgs e)
        {
            Close();
        }

        private void RetryButton_clicked(object sender, System.Windows.RoutedEventArgs e)
        {
            selected = true;
            Close();
        }

        private void WiFiButton_clicked(object sender, System.Windows.RoutedEventArgs e)
        {
            IPAddressDialog dialog = new IPAddressDialog();
            dialog.ShowDialog();
            IPAddress a = dialog.GetAddress();
            if (a != null)
            {
                selected = a;
                Close();
            }
        }


        private void PortList_selected(Object sender, EventArgs e)
        {
            int idx = PortList.SelectedIndex;
            if (idx >= 0 && idx < usbdevices.Length)
            {
                selected = usbdevices[idx];
                Close();
            }
            else if (idx >= usbdevices.Length && idx < usbdevices.Length + ports.Length)
            {
                selected = ports[idx - usbdevices.Length];
                Close();
            }
            else if (idx >= usbdevices.Length + ports.Length && idx < usbdevices.Length + ports.Length + addresses.Length)
            {
                selected = addresses[idx - usbdevices.Length - ports.Length];
                Close();
            }
        }

        private void PortList_keydown(Object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PortList_selected(sender, e);
            }
        }

    }
}
