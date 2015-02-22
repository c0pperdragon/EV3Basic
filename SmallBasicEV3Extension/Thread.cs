using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.SmallBasic.Library;

namespace SmallBasicEV3Extension
{

    [SmallBasicType]
    public class Thread
    {
        // The list of all threads that were triggered from the basic program.
        // A thread is identified by its event handler target and can be 
        // triggered multiple times, in which case the handler will be called this many times,
        // but only in sequence.  
        private static Dictionary<SmallBasicCallback, Thread> triggeredThreads = new Dictionary<SmallBasicCallback, Thread>();

        // the list of all mutexes that were created by the basic program. these are accessed using the index,
        // with sensible behaviour if used incorrectly (create immediate full lock to show usage error!)
        private static List<bool> locks = new List<bool>();

        // This does not install an event handler, but triggers a new thread instead.
        // That was the only way to get a nice API for threading in Small Basic.
        public static event SmallBasicCallback Run 
        {
            // do not install an event handler, but start a thread instead
            add
            {
                lock (triggeredThreads)
                {
                    if (!triggeredThreads.ContainsKey(value))
                    {
                        triggeredThreads[value] = new Thread(value);
                    }
                    triggeredThreads[value].Trigger();
                }
            }
            remove
            {
                // no action, because threads can not be removed
            }
        }

        public static void Yield()
        {
            System.Threading.Thread.Sleep(0);
        }

        public static Primitive CreateMutex()
        {
            lock (locks)
            {
                int idx = locks.Count;
                locks.Add(false);
                return new Primitive(idx);
            }
        }

        public static void Lock(Primitive mutex)
        {
            int idx;
            if (int.TryParse(mutex.ToString(), out idx))
            {
                lock (locks)
                {
                    if (idx >= 0 && idx < locks.Count())
                    {
                        // try to aquire a lock.  if not ready, must wait until it gets released
                        while (locks[idx])
                        {
                            Monitor.Wait(locks);
                        }
                        locks[idx] = true;
                        return;
                    }
                }
            }
            // when the lock mechanism was incorrectly used, totally lock up the program to make the problem obvious
            for (; ; )
            {
                System.Threading.Thread.Sleep(1000000);
            }
        }

        public static void Unlock(Primitive mutex)
        {
            int idx;
            if (int.TryParse(mutex.ToString(), out idx))
            {
                lock (locks)
                {
                    if (idx >= 0 && idx < locks.Count())
                    {
                        locks[idx] = false;
                        Monitor.PulseAll(locks);
                    }
                }
            }
        }


        // instance members
        private SmallBasicCallback callback;
        private int triggerCount;

        private Thread(SmallBasicCallback callback)
        {
            this.callback = callback;
            this.triggerCount = 0;
            (new System.Threading.Thread(run)).Start();
        }

        private void Trigger()
        {
            lock (this)
            {
                triggerCount++;
                Monitor.PulseAll(this);
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
                        Monitor.Wait(this);
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
