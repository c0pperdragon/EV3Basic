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

using Microsoft.SmallBasic.Library;
using EV3Communication;

namespace SmallBasicEV3Extension
{

    /// <summary>
    /// Control the Motors connected to the Brick.
    /// At any function you need to specify one or more motor ports that should be affected (for example, "A", "BC", "ABD").
    /// When additional bricks are daisy-chained to the master brick, address the correct port by adding the layer number to the specifier (for example, "3BC", "2A"). In this case only the motors of one brick can be accessed with a single command. 
    /// </summary>
    [SmallBasicType]
    public static class Motor
    {
        /// <summary>
        /// Sets one or multiple motors to interpret all subsequent speed or power values as negative. 
        /// While all motors could be controlled with negative values anyway, this can make the program more readable. A positive speed value could always denote a "forward" motion of the robot, no matter how the motor is built into the robot. You only need to make the propper RevertDirection() calls once at program start.
        /// </summary>
        /// <param name="ports">Motor port name(s)</param>
        /// <param name="revert">"True" to set revert or "False" to remove the setting</param>
        public static void RevertDirection(Primitive ports, Primitive revert)
        {
            int layer;
            int nos;
            DecodePortsDescriptor(ports == null ? "" : ports.ToString(), out layer, out nos);
            int dir = (revert == null ? "" : revert.ToString()).Equals("true", StringComparison.OrdinalIgnoreCase) ? -1 : 1;

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xA7);            // opOutput_Polarity
            c.CONST(layer);
            c.CONST(nos);
            c.CONST(dir);
            EV3RemoteControler.DirectCommand(c, 0, 0);
        }

 
        /// <summary>
        /// Stop one or multiple motors. This will also cancel any scheduled motor movements.
        /// </summary>
        /// <param name="ports">Motor port name(s)</param>
        /// <param name="brake">"True", if the motor should use the brake</param>
        public static void Stop(Primitive ports, Primitive brake)
        {
            int layer;
            int nos;
            DecodePortsDescriptor(ports==null?"":ports.ToString(), out layer, out nos);
            int brk = (brake==null?"":brake.ToString()).Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xA3);
            c.CONST(layer);
            c.CONST(nos);
            c.CONST(brk);
            EV3RemoteControler.DirectCommand(c, 0, 0);
        }

        /// <summary>
        /// Set a power level to use for one or multiple motors.
        /// When the motor is currently stopped, this will preset the power level to be use when it is finally started.
        /// </summary>
        /// <param name="ports">Motor port name(s)</param>
        /// <param name="power">Power level from -100 (full reverse) to 100 (full forward)</param>
        public static void Power(Primitive ports, Primitive power)
        {
            int layer;
            int nos;
            DecodePortsDescriptor(ports==null?"":ports.ToString(), out layer, out nos);
            int pwr = clamp(power, -100, 100);

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xA4);
            c.CONST(layer);
            c.CONST(nos);
            c.CONST(pwr);
            EV3RemoteControler.DirectCommand(c, 0, 0);
        }

        /// <summary>
        /// Turn on speed regulation for one or multiple motors and select a speed level. Once started, the device will try to maintain this constant speed of the motor regardless of (minor) resistance.
        /// </summary>
        /// <param name="ports">Motor port name(s)</param>
        /// <param name="speed">Speed level from -100 (full reverse) to 100 (full forward)</param>
        public static void Speed(Primitive ports, Primitive speed)
        {
            int layer;
            int nos;
            DecodePortsDescriptor(ports == null ? "" : ports.ToString(), out layer, out nos);
            int spd = clamp(speed, -100, 100);

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xA5);
            c.CONST(layer);
            c.CONST(nos);
            c.CONST(spd);
            EV3RemoteControler.DirectCommand(c, 0, 0);
        }

        /// <summary>
        /// Starts one or more motors with the preselected speed or power.
        /// </summary>
        /// <param name="ports">Motor port name(s)</param>
        public static void Start(Primitive ports)
        {
            int layer;
            int nos;
            DecodePortsDescriptor(ports == null ? "" : ports.ToString(), out layer, out nos);

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xA6);
            c.CONST(layer);
            c.CONST(nos);
            EV3RemoteControler.DirectCommand(c, 0, 0);
        }

        /// <summary>
        /// Start two motors to run synchronized at equal or different speeds. 
        /// The two motors will be synchronized, that means, when one motor experiences some resistance and can not keep up its speed, the other motor will also slow down or stop altogether. This is expecially useful for vehicles with two independently driven wheels which still needs to go straight or make a specified turn.
        /// The motors will keep running until stopped by another command.
        /// </summary>
        /// <param name="ports">Name of two motor ports (for example "AB" or "CD").</param>
        /// <param name="speed1">Speed value from -100 (full reverse) to 100 (full forward) of the motor with the lower port letter.</param>
        /// <param name="speed2">Speed value from -100 (full reverse) to 100 (full forward) of the motor with the higher port letter.</param>
        public static void StartSynchronized(Primitive ports, Primitive speed1, Primitive speed2)
        {
            int layer, nos;
            DecodePortsDescriptor(ports == null ? "" : ports.ToString(), out layer, out nos);
            double spd1 = fclamp(speed1, -100, 100);
            double spd2 = fclamp(speed2, -100, 100);

            // the computed values that will be needed by the firmware function
            int spd;
            int trn;

            // motor with lower letter is faster or equally fast and must become master      
            if ((spd1 >= 0 ? spd1 : -spd1) >= (spd2 >= 0 ? spd2 : -spd2))
            {
                spd = (int)spd1;
                trn = 100 - (int)((100.0 * spd2) / spd1);
            }
            // motor with higher letter is faster and must become master
            else
            {
                spd = (int)spd2;
                trn = -(100 - (int)((100.0 * spd1) / spd2));
            }

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xB0);
            c.CONST(layer);
            c.CONST(nos);
            c.CONST(spd);
            c.CONST(trn);
            c.CONST(0);
            c.CONST(0);
            EV3RemoteControler.DirectCommand(c, 0, 0);
        }


        /// <summary>
        /// Query the current speed of a single motor.
        /// </summary>
        /// <param name="port">Motor port name</param>
        /// <returns>Current speed in range -100 to 100</returns>
        public static Primitive GetSpeed(Primitive port)
        {
            int layer;
            int no;
            DecodePortDescriptor(port == null ? "" : port.ToString(), out layer, out no);

            if (no < 0)
            {
                return new Primitive(0);
            }
            else
            {
                ByteCodeBuffer c = new ByteCodeBuffer();
                c.OP(0xA8);
                c.CONST(layer);
                c.CONST(no);
                c.GLOBVAR(4);
                c.GLOBVAR(0);
                byte[] reply = EV3RemoteControler.DirectCommand(c, 5, 0);

                int spd = reply == null ? 0 : (sbyte)reply[4];
                return new Primitive(spd);
            }
        }

        /// <summary>
        /// Checks if one or more motors are still busy with a scheduled motor movement. 
        /// </summary>
        /// <param name="ports">Motor port name(s)</param>
        /// <returns>"True" if at least one of the motors is busy, "False" otherwise.</returns>
        public static Primitive IsBusy(Primitive ports)
        {
            int layer;
            int nos;
            DecodePortsDescriptor(ports == null ? "" : ports.ToString(), out layer, out nos);

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xA9);
            c.CONST(layer);
            c.CONST(nos);
            c.GLOBVAR(0);
            byte[] reply = EV3RemoteControler.DirectCommand(c, 1, 0);

            return new Primitive((reply!=null && reply[0]!=0) ? "True" : "False");
        }

        
        /// <summary>
        /// Starts to move one or more motors a defined number of counts (=degrees). 
        /// The movement can be fine-tuned by defining acceleration and deceleration portions of the total path.
        /// This function returns immediately. Use IsBusy() to detect the end of the movement or call Wait() to wait until movement is finished.
        /// </summary>
        /// <param name="ports">Motor port name(s)</param>
        /// <param name="power">Power level from -100 (full reverse) to 100 (full forward)</param>
        /// <param name="step1">Number of counts to accelerate</param>
        /// <param name="step2">Number of counts in uniform motion</param>
        /// <param name="step3">Number of counts to decelerate</param>
        /// <param name="brake">"True", if the motor(s) should switch on the brake after movement</param>
        public static void SchedulePower (Primitive ports, Primitive power, Primitive step1, Primitive step2, Primitive step3, Primitive brake)
        {
            int layer, nos;
            DecodePortsDescriptor(ports == null ? "" : ports.ToString(), out layer, out nos);
            int pwr = clamp(power, -100, 100);
            int stp1 = step1;
            int stp2 = step2;
            int stp3 = step3;
            int brk = (brake==null?"":brake.ToString()).Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xAC);
            c.CONST(layer);
            c.CONST(nos);
            c.CONST(pwr);
            c.CONST(stp1);
            c.CONST(stp2);
            c.CONST(stp3);
            c.CONST(brk);
            EV3RemoteControler.DirectCommand(c, 0, 0);
        }

        /// <summary>
        /// Starts to move one or more motors by the specified number of counts. 
        /// The speed regulator will try to hold the motor at the specified speed even if there is some resistance. This will be done by increasing the power if needed (as long as power can still be raised).
        /// This function returns immediately. Use IsBusy() to detect the end of the movement or call Wait() to wait until movement is finished.
        /// </summary>
        /// <param name="ports">Motor port name(s)</param>
        /// <param name="speed">Speed level from -100 (full reverse) to 100 (full forward)</param>
        /// <param name="step1">Number of counts to accelerate</param>
        /// <param name="step2">Number of counts in uniform motion</param>
        /// <param name="step3">Number of counts to decelerate</param>
        /// <param name="brake">"True", if the motor(s) should switch on the brake after movement</param>
        public static void ScheduleSpeed(Primitive ports, Primitive speed, Primitive step1, Primitive step2, Primitive step3, Primitive brake)
        {
            int layer, nos;
            DecodePortsDescriptor(ports == null ? "" : ports.ToString(), out layer, out nos);
            int spd = clamp(speed,-100,100);
            int stp1 = step1;
            int stp2 = step2;
            int stp3 = step3;
            int brk = (brake == null ? "" : brake.ToString()).Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xAE);
            c.CONST(layer);
            c.CONST(nos);
            c.CONST(spd);
            c.CONST(stp1);
            c.CONST(stp2);
            c.CONST(stp3);
            c.CONST(brk);
            EV3RemoteControler.DirectCommand(c, 0, 0);
        }

        /// <summary>
        /// Starts to synchroniously move 2 motors a defined number of counts (=degrees). 
        /// The two motors are synchronized, that means, when one motor experiences some resistance and can not keep up its speed, the other motor will also slow down or stop altogether. This is expecially useful for vehicles with two independently driven wheels which still needs to go straight or make a specified turn.
        /// The number of counts to move will be measured at the motor with the higher specified speed.
        /// </summary>
        /// <param name="ports">Names of 2 motor ports (for example "AB" or "CD"</param>
        /// <param name="speed1">Speed value from -100 (full reverse) to 100 (full forward) of the motor with the lower port letter.</param>
        /// <param name="speed2">Speed value from -100 (full reverse) to 100 (full forward) of the motor with the higher port letter.</param>
        /// <param name="count">Number of counts for the faster motor to move</param>
        /// <param name="brake">"True", if the motors should switch on the brake after movement</param>
        public static void ScheduleSynchronized(Primitive ports, Primitive speed1, Primitive speed2, Primitive count, Primitive brake)
        {
            int layer, nos;
            DecodePortsDescriptor(ports == null ? "" : ports.ToString(), out layer, out nos);
            double spd1 = fclamp(speed1,-100,100);
            double spd2 = fclamp(speed2,-100,100);
            int cnt = count;
            int brk = (brake==null?"":brake.ToString()).Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            if (cnt > 0)
            {
                // the computed values that will be needed by the firmware function
                int spd;   
                int trn;

                // motor with lower letter is faster or equally fast and must become master      
                if ( (spd1>=0?spd1:-spd1) >= (spd2>=0?spd2:-spd2))
                {
                    spd = (int)spd1;
                    trn = 100 - (int)((100.0*spd2)/spd1);
                }
                // motor with higher letter is faster and must become master
                else   
                {
                    spd = (int)spd2;
                    trn = - (100 - (int)((100.0*spd1) / spd2));
                }

                ByteCodeBuffer c = new ByteCodeBuffer();
                c.OP(0xB0);
                c.CONST(layer);
                c.CONST(nos);
                c.CONST(spd);
                c.CONST(trn);
                c.CONST(cnt);
                c.CONST(brk);
                EV3RemoteControler.DirectCommand(c, 0, 0);
            }
        }


        /// <summary>
        /// Set the rotation count of one or more motors to 0.
        /// </summary>
        /// <param name="ports">Motor port name(s)</param>
        public static void ResetCount (Primitive ports)
        {
            int layer, nos;
            DecodePortsDescriptor(ports == null ? "" : ports.ToString(), out layer, out nos);

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xB2);
            c.CONST(layer);
            c.CONST(nos);
            EV3RemoteControler.DirectCommand(c, 0, 0);
        }

        /// <summary>
        /// Query the current rotation count of a single motor.
        /// </summary>
        /// <param name="port">Motor port name</param>
        /// <returns>The current rotation count in degrees</returns>
        public static Primitive GetCount (Primitive port)
        {
            int layer, no;
            DecodePortDescriptor(port == null ? "" : port.ToString(), out layer, out no);

            if (no < 0)
            {
                return new Primitive(0);
            }
            else
            {
                ByteCodeBuffer c = new ByteCodeBuffer();
                c.OP(0xB3);
                c.CONST(layer);
                c.CONST(no);
                c.GLOBVAR(0);
                byte[] reply = EV3RemoteControler.DirectCommand(c, 4, 0);

                int tacho = 0;
                if (reply != null)
                {
                    tacho = ((int)reply[0]) | (((int)reply[1]) << 8) | (((int)reply[2]) << 16) | (((int)reply[3]) << 24);
                }
                return new Primitive(tacho);
            }
        }

        /// <summary>
        /// Move one or more motors by the specified number of counts (=degrees). 
        /// The motor will apply the specified power to reach the target position. This command will block execution until the motor has reached its destination.
        /// When you need finer control over the movement, consider using one of the Schedule.. functions.
        /// </summary>
        /// <param name="ports">Motor port name(s)</param>
        /// <param name="power">Power level from -100 (full reverse) to 100 (full forward)</param>
        /// <param name="count">Number of counts to move the motor. Only the magnitude of the value is taken when a negative number is given here.</param>
        /// <param name="brake">"True", if the motor(s) should switch on the brake after movement</param>
        public static void Move(Primitive ports, Primitive power, Primitive count, Primitive brake)
        {
            SchedulePower(ports, power, new Primitive(0), count, new Primitive(0), brake);
            Wait(ports);
        }

       
       
        /// <summary>
        /// Wait until the specified motor(s) has finished a scheduled operation.
        /// Using this method is normally better than calling IsBusy() in a tight loop.
        /// </summary>
        /// <param name="ports">Motor port name(s)</param>
        public static void Wait(Primitive ports)
        {
            int layer, nos;
            DecodePortsDescriptor(ports == null ? "" : ports.ToString(), out layer, out nos);

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.Clear();
            c.OP(0xA9);
            c.CONST(layer);
            c.CONST(nos);
            c.GLOBVAR(0);

            for (;;)
            {
                byte[] reply = EV3RemoteControler.DirectCommand(c, 1, 0);
                if (reply==null || reply[0] == 0)
                {
                    break;
                }
                System.Threading.Thread.Sleep(2);
            }
        }



        private static void DecodePortDescriptor(String descriptor, out int layer, out int no)
        {
            layer = 0;
            no = -1;
            for (int i = 0; i < descriptor.Length; i++)
            {
                switch (descriptor[i])
                {
                    case '2': layer = 1; break;
                    case '3': layer = 2; break;
                    case '4': layer = 3; break;
                    case 'a':
                    case 'A': no = 0; break;
                    case 'b':
                    case 'B': no = 1; break;
                    case 'c':
                    case 'C': no = 2; break;
                    case 'd':
                    case 'D': no = 3; break;
                }
            }
        }

        private static void DecodePortsDescriptor(String descriptor, out int layer, out int nos)
        {
            layer = 0;
            nos = 0;
            for (int i = 0; i < descriptor.Length; i++)
            {
                switch (descriptor[i])
                {
                    case '2': layer = 1; break;
                    case '3': layer = 2; break;
                    case '4': layer = 3; break;
                    case 'a':
                    case 'A': nos = nos | 1; break;
                    case 'b':
                    case 'B': nos = nos | 2; break;
                    case 'c':
                    case 'C': nos = nos | 4; break;
                    case 'd':
                    case 'D': nos = nos | 8; break;
                }
            }
        }

        private static int clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }
        private static double fclamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }
    }

}
