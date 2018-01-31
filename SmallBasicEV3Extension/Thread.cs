/*  EV3-Basic: A basic compiler to target the Lego EV3 brick
    Copyright (C) 2017 Reinhard Grafl

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

using Microsoft.SmallBasic.Library;

namespace SmallBasicEV3Extension
{
    /// <summary>
    /// This object supports the use of threads in a program. 
    /// A thread is a piece of program code that can run independently and at the same time as other parts of the program. For example, you could create a thread that controls the motors, while a different thread can watch sensors or user input.
    /// Generally speaking, multithreading is quite a complex topic. To really understand it, some extra study is recommended.
    /// </summary>
    [SmallBasicType]
    public class Thread
    {
        // The list of all threads that were triggered from the basic program.
        // A thread is identified by its event handler target and can be triggered multiple times,
        // in which case the handler will be called this many times, but only in sequence.  
        private static Dictionary<SmallBasicCallback, Thread> triggeredThreads = new Dictionary<SmallBasicCallback, Thread>();

        // The list of all mutexes that were created by the basic program. these are accessed using the index,
        // with sensible behaviour if used incorrectly (create immediate full lock to show usage error!)
        private static List<bool> locks = new List<bool>();

        /// <summary>
        /// With this property, new threads are created. Just assign a subprogram to this and the subprogram will start running as an independent thread (for example, Thread.Run = MYSUB). 
        /// Any subprogram can be used to create an independent thread, but you can start the same subprogram only as one thread. A second use of Thread.Run, while the specified subprogram is still running, will just add the call to a queue that is processed after the previous run was finished. No runs will be lost in this case, but probably scheduled for a later time.
        /// Note that even in the presence of running threads, the whole program stops as soon as the main program runs to its end.
        /// </summary>
        public static event SmallBasicCallback Run 
        {
            // do not install an event handler, but start a thread instead
            add
            {
                lock (triggeredThreads)
                {
                    if (!triggeredThreads.ContainsKey(value))
                    {
                        triggeredThreads[value] = new Thread(value, "EV3-"+triggeredThreads.Count);
                    }
                    triggeredThreads[value].Trigger();
                }
            }
            remove
            {
                // no action, because threads can not be removed
            }
        }

        /// <summary>
        /// Explicitly gives up control of the CPU so other threads may do their work.
        /// Threads are often not really running in parallel because there may be not enough CPUs to exclusively do the work for each thread. Instead, the CPU will do a bit of work on one thread and then jump to the next thread and so on very quickly, to make it look like everything is running in parallel.
        /// Whenever a thread has nothing to do just now, but needs to wait for some condition to arrive, it can give up the control of the CPU with the Yield() function, so other threads get the chance to do their work. 
        /// </summary>
        public static void Yield()
        {
            System.Threading.Thread.Sleep(0);
        }

        /// <summary>
        /// Create a mutex (short for "mutual exclusion" handler) that can be used for thread synchronization. 
        /// Only creation of mutexes is supported, but not deletion. Best practice is to create all needed mutexes at program start and keep their numbers in global variables.
        /// </summary>
        /// <returns>A number specifying the new mutex. Use this for calls to Lock and Unlock</returns>
        public static Primitive CreateMutex()
        {
            lock (locks)
            {
                int idx = locks.Count;
                locks.Add(false);
                return new Primitive(idx);
            }
        }

        /// <summary>
        /// Tries to lock the given mutex exclusively so no other thread can acquire a lock on it. 
        /// When another thread already holds a lock on the mutex, the current thread will wait until the lock is released and then acquire the lock itself (once the function call returns, the mutex has been successfully locked).
        /// This locking mechanism is normally used to protect some data structures or other resources from being accessed by two threads concurrently. Every call to Lock must be paired with a call to a subsequent Unlock.
        /// </summary>
        /// <param name="mutex">The number of the mutex (as returned from CreateMutex() )</param>
        public static void Lock(Primitive mutex)
        {
            int idx = mutex;
            lock (locks)
            {
                if (idx >= 0 && idx < locks.Count())
                {
                    // try to aquire a lock.  if not ready, must wait until it gets released
                    while (locks[idx])
                    {
                        System.Threading.Monitor.Wait(locks);
                    }
                    locks[idx] = true;
                    return;
                }
            }
            // when the lock mechanism was incorrectly used, totally lock up the program to make the problem obvious
            for (; ; )
            {
                System.Threading.Thread.Sleep(1000000);
            }
        }

        /// <summary>
        /// Releases a lock on a mutex. This function must only be called when there was indeed a preceding call to Lock. 
        /// </summary>
        /// <param name="mutex">The number of the mutex (as returned from CreateMutex() )</param>
        public static void Unlock(Primitive mutex)
        {
            int idx = mutex;
            lock (locks)
            {
                if (idx >= 0 && idx < locks.Count())
                {
                    locks[idx] = false;
                    System.Threading.Monitor.PulseAll(locks);
                }
            }
        }


        // instance members
        private SmallBasicCallback callback;
        private int triggerCount;

        private Thread(SmallBasicCallback callback, String name)
        {
            this.callback = callback;
            this.triggerCount = 0;
            System.Threading.Thread t = new System.Threading.Thread(run);
            t.Name = name;
            t.Start();
        }

        private void Trigger()
        {
            lock (this)
            {
                triggerCount++;
                System.Threading.Monitor.PulseAll(this);
            }
        }

        private void run()
        {
            for (; ; )       // this c#-thread runs forever for the case that basic wants to trigger the thread once more
            {
                // wait for a trigger
                bool mustrun = false;
                lock (this)
                {
                    if (triggerCount>0)
                    {
                        triggerCount--;
                        mustrun = true;
                    }
                    else
                    {
                        System.Threading.Monitor.Wait(this);
                    }
                }
                // when a trigger is detected, run the basic thread once
                if (mustrun)
                {
                    callback.Invoke();
                }
            }
        }

    }

}
