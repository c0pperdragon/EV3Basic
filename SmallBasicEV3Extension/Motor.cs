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
            EV3Communicator.DirectCommand(c, 0, 0);
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
            EV3Communicator.DirectCommand(c, 0, 0);
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
            int pwr;
            Int32.TryParse(power==null?"":power.ToString(), out pwr);
            if (pwr < -100)
            {
                pwr = -100;
            }
            if (pwr > 100)
            {
                pwr = 100;
            }


            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xA4);
            c.CONST(layer);
            c.CONST(nos);
            c.CONST(pwr);
            EV3Communicator.DirectCommand(c, 0, 0);
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
            int spd;
            Int32.TryParse(speed == null ? "" : speed.ToString(), out spd);
            if (spd < -100)
            {
                spd = -100;
            }
            if (spd > 100)
            {
                spd = 100;
            }

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xA5);
            c.CONST(layer);
            c.CONST(nos);
            c.CONST(spd);
            EV3Communicator.DirectCommand(c, 0, 0);
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
            EV3Communicator.DirectCommand(c,0,0);
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
                byte[] reply = EV3Communicator.DirectCommand(c, 5, 0);

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
            byte[] reply = EV3Communicator.DirectCommand(c,1, 0);

            return new Primitive((reply!=null && reply[0]!=0) ? "True" : "False");
        }

        
        /// <summary>
        /// Starts to move one or more motors a defined number of degrees. 
        /// The movement can be fine-tuned by defining acceleration and deceleration portions of the total path.
        /// This function returns immediately. Use IsBusy() to detect the end of the movement.
        /// </summary>
        /// <param name="ports">Motor port name(s)</param>
        /// <param name="power">Power level from -100 (full reverse) to 100 (full forward)</param>
        /// <param name="step1">Number of degrees to accelerate</param>
        /// <param name="step2">Number of degrees in uniform motion</param>
        /// <param name="step3">Number of degrees to decelerate</param>
        /// <param name="brake">"True", if the motor(s) should switch on the brake after movement</param>
        public static void SchedulePowerForCount (Primitive ports, Primitive power, Primitive step1, Primitive step2, Primitive step3, Primitive brake)
        {
            int layer, nos, pwr, stp1, stp2, stp3, brk;
            DecodePortsDescriptor(ports == null ? "" : ports.ToString(), out layer, out nos);
            Int32.TryParse(power == null ? "" : power.ToString(), out pwr);
            Int32.TryParse(step1 == null ? "" : step1.ToString(), out stp1);
            Int32.TryParse(step2 == null ? "" : step2.ToString(), out stp2);
            Int32.TryParse(step3 == null ? "" : step3.ToString(), out stp3);
            brk = (brake==null?"":brake.ToString()).Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xAC);
            c.CONST(layer);
            c.CONST(nos);
            c.CONST(pwr);
            c.CONST(stp1);
            c.CONST(stp2);
            c.CONST(stp3);
            c.CONST(brk);
            EV3Communicator.DirectCommand(c,0, 0);
        }

        /// <summary>
        /// Starts to move one or more motors a defined number of milliseconds.
        /// The movement can be fine-tuned by defining acceleration and deceleration portions of the total time.
        /// This function returns immediately. Use IsBusy() to detect the end of the movement.
        /// </summary>
        /// <param name="ports">Motor port name(s)</param>
        /// <param name="power">Power level from -100 (full reverse) to 100 (full forward)</param>
        /// <param name="step1">Milliseconds to accelerate</param>
        /// <param name="step2">Milliseconds in uniform motion</param>
        /// <param name="step3">Milliseconds to decelerate</param>
        /// <param name="brake">"True", if the motor(s) should switch on the brake after movement</param>
        public static void SchedulePowerForTime(Primitive ports, Primitive power, Primitive step1, Primitive step2, Primitive step3, Primitive brake)
        {
            int layer, nos, pwr, stp1, stp2, stp3, brk;
            DecodePortsDescriptor(ports == null ? "" : ports.ToString(), out layer, out nos);
            Int32.TryParse(power == null ? "" : power.ToString(), out pwr);
            Int32.TryParse(step1 == null ? "" : step1.ToString(), out stp1);
            Int32.TryParse(step2==null?"":step2.ToString(), out stp2);
            Int32.TryParse(step3 == null ? "" : step3.ToString(), out stp3);
            brk = (brake==null?"":brake.ToString()).Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xAD);
            c.CONST(layer);
            c.CONST(nos);
            c.CONST(pwr);
            c.CONST(stp1);
            c.CONST(stp2);
            c.CONST(stp3);
            c.CONST(brk);
            EV3Communicator.DirectCommand(c,0, 0);
        }

        /// <summary>
        /// Starts to move one or more motors a defined number of degrees. 
        /// The movement can be fine-tuned by defining acceleration and deceleration portions of the total path.
        /// This function returns immediately. Use IsBusy() to detect the end of the movement.
        /// </summary>
        /// <param name="ports">Motor port name(s)</param>
        /// <param name="speed">Speed level from -100 (full reverse) to 100 (full forward)</param>
        /// <param name="step1">Number of degrees to accelerate</param>
        /// <param name="step2">Number of degrees in uniform motion</param>
        /// <param name="step3">Number of degrees to decelerate</param>
        /// <param name="brake">"True", if the motor(s) should switch on the brake after movement</param>
        public static void ScheduleSpeedForCount(Primitive ports, Primitive speed, Primitive step1, Primitive step2, Primitive step3, Primitive brake)
        {
            int layer, nos, spd, stp1, stp2, stp3, brk;
            DecodePortsDescriptor(ports == null ? "" : ports.ToString(), out layer, out nos);
            Int32.TryParse(speed == null ? "" : speed.ToString(), out spd);
            Int32.TryParse(step1 == null ? "" : step1.ToString(), out stp1);
            Int32.TryParse(step2 == null ? "" : step2.ToString(), out stp2);
            Int32.TryParse(step3 == null ? "" : step3.ToString(), out stp3);
            brk = (brake==null?"":brake.ToString()).Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xAE);
            c.CONST(layer);
            c.CONST(nos);
            c.CONST(spd);
            c.CONST(stp1);
            c.CONST(stp2);
            c.CONST(stp3);
            c.CONST(brk);
            EV3Communicator.DirectCommand(c,0, 0);
        }

        /// <summary>
        /// Starts to move one or more motors a defined number of milliseconds.
        /// The movement can be fine-tuned by defining acceleration and deceleration portions of the total time.
        /// This function returns immediately. Use IsBusy() to detect the end of the movement.
        /// </summary>
        /// <param name="ports">Motor port name(s)</param>
        /// <param name="speed">Speed level from -100 (full reverse) to 100 (full forward)</param>
        /// <param name="step1">Milliseconds to accelerate</param>
        /// <param name="step2">Milliseconds in uniform motion</param>
        /// <param name="step3">Milliseconds to decelerate</param>
        /// <param name="brake">"True", if the motor(s) should switch on the brake after movement</param>
        public static void ScheduleSpeedForTime(Primitive ports, Primitive speed, Primitive step1, Primitive step2, Primitive step3, Primitive brake)
        {
            int layer, nos, spd, stp1, stp2, stp3, brk;
            DecodePortsDescriptor(ports == null ? "" : ports.ToString(), out layer, out nos);
            Int32.TryParse(speed == null ? "" : speed.ToString(), out spd);
            Int32.TryParse(speed == null ? "" : step1.ToString(), out stp1);
            Int32.TryParse(speed == null ? "" : step2.ToString(), out stp2);
            Int32.TryParse(speed == null ? "" : step3.ToString(), out stp3);
            brk = (brake==null?"":brake.ToString()).Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xAF);
            c.CONST(layer);
            c.CONST(nos);
            c.CONST(spd);
            c.CONST(stp1);
            c.CONST(stp2);
            c.CONST(stp3);
            c.CONST(brk);
            EV3Communicator.DirectCommand(c,0, 0);
        }

        /// <summary>
        /// Starts to synchroniously move 2 motors a defined number of degrees. 
        /// The motor synchronization can be set to any relative speed ratio. One of the motors will be the 'master' to which the speed and the number of degrees will apply. 
        /// For positive values of 'turn', the motor with the lower port letter becomes the master. The absolute magnitude of 'turn' specifies how much the 'slave' motor is reduced in its speed. For example: 0 = same speed, 50 = half speed, 100 = stop, 150 = half speed opposite, 200 = full speed opposite
        /// </summary>
        /// <param name="ports">Names of 2 motor ports</param>
        /// <param name="speed">Speed level from -100 (full reverse) to 100 (full forward) for the 'master' motor</param>
        /// <param name="turn">Value from -200 to 200</param>
        /// <param name="count">Number of degrees for the 'master' motor to move</param>
        /// <param name="brake">"True", if the motor(s) should switch on the brake after movement</param>
        public static void ScheduleSyncForCount(Primitive ports, Primitive speed, Primitive turn, Primitive count, Primitive brake)
        {
            int layer, nos, spd, trn, cnt, brk;
            DecodePortsDescriptor(ports == null ? "" : ports.ToString(), out layer, out nos);
            Int32.TryParse(speed == null ? "" : speed.ToString(), out spd);
            Int32.TryParse(turn == null ? "" : turn.ToString(), out trn);
            Int32.TryParse(count == null ? "" : count.ToString(), out cnt);
            brk = (brake==null?"":brake.ToString()).Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            if (cnt > 0)
            {
                ByteCodeBuffer c = new ByteCodeBuffer();
                c.OP(0xB0);
                c.CONST(layer);
                c.CONST(nos);
                c.CONST(spd);
                c.CONST(trn);
                c.CONST(cnt);
                c.CONST(brk);
                EV3Communicator.DirectCommand(c,0, 0);
            }
        }

        /// <summary>
        /// Starts to synchroniously move 2 motors a defined number of milliseconds. 
        /// The motor synchronization can be set to any relative speed ratio. One of the motors will be the 'master' to which the speed will apply. 
        /// For positive values of 'turn', the motor with the lower port letter becomes the master. The absolute magnitude of 'turn' specifies how much the 'slave' motor is reduced in its speed. For example: 0 = same speed, 50 = half speed, 100 = stop, 150 = half speed opposite, 200 = full speed opposite
        /// </summary>
        /// <param name="ports">Names of 2 motor ports</param>
        /// <param name="speed">Speed level from -100 (full reverse) to 100 (full forward) for the 'master' motor</param>
        /// <param name="turn">Value from -200 to 200</param>
        /// <param name="time">Milliseconds to run. May be 0, in which case the motors run indefinitely.</param>
        /// <param name="brake">"True", if the motor(s) should switch on the brake after movement</param>
        public static void ScheduleSyncForTime(Primitive ports, Primitive speed, Primitive turn, Primitive time, Primitive brake)
        {
            int layer, nos, spd, trn, tim, brk;
            DecodePortsDescriptor(ports == null ? "" : ports.ToString(), out layer, out nos);
            Int32.TryParse(speed == null ? "" : speed.ToString(), out spd);
            Int32.TryParse(turn == null ? "" : turn.ToString(), out trn);
            Int32.TryParse(time == null ? "" : time.ToString(), out tim);
            brk = (brake==null?"":brake.ToString()).Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            ByteCodeBuffer c = new ByteCodeBuffer();
            c.OP(0xB1);
            c.CONST(layer);
            c.CONST(nos);
            c.CONST(spd);
            c.CONST(trn);
            c.CONST(tim);
            c.CONST(brk);
            EV3Communicator.DirectCommand(c,0, 0);
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
            EV3Communicator.DirectCommand(c,0, 0);
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
                byte[] reply = EV3Communicator.DirectCommand(c, 4, 0);

                int tacho = 0;
                if (reply != null)
                {
                    tacho = ((int)reply[0]) | (((int)reply[1]) << 8) | (((int)reply[2]) << 16) | (((int)reply[3]) << 24);
                }
                return new Primitive(tacho);
            }
        }

        /// <summary>
        /// Move one or more motors by a the specified count. 
        /// The motor will apply full power to reach the target position as quickly as possible. This function will wait until the motor has reached its destination.
        /// When you need finer control over the movement, consider using one of the Schedule.. functions.
        /// </summary>
        /// <param name="ports">Motor port name(s)</param>
        /// <param name="count">Number of counts to move the motor. Can be positive (forward) or negative (reverse)</param>
        /// <param name="brake">"True", if the motor(s) should switch on the brake after movement</param>
        public static void Move(Primitive ports, Primitive count, Primitive brake)
        {
            int layer, nos, cnt, brk;
            DecodePortsDescriptor(ports == null ? "" : ports.ToString(), out layer, out nos);
            Int32.TryParse(count == null ? "" : count.ToString(), out cnt);
            brk = (brake==null?"":brake.ToString()).Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            if (count != 0)
            {
                ByteCodeBuffer c = new ByteCodeBuffer();
                c.OP(0xAC);
                c.CONST(layer);
                c.CONST(nos);
                c.CONST((count < 0) ? -100 : 100);
                c.CONST(0);
                c.CONST(count);
                c.CONST(0);
                c.CONST(brk);
                EV3Communicator.DirectCommand(c,0, 0);

                for (; ; )
                {
                    System.Threading.Thread.Sleep(2);

                    c.Clear();
                    c.OP(0xA9);
                    c.CONST(layer);
                    c.CONST(nos);
                    c.GLOBVAR(0);
                    byte[] reply = EV3Communicator.DirectCommand(c,1, 0);
                    if (reply==null || reply[0] == 0)
                    {   break;
                    }
                }
            }
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
                byte[] reply = EV3Communicator.DirectCommand(c, 1, 0);
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
    }

}
