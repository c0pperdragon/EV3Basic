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
