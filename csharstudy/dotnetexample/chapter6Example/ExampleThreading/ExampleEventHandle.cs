using System;
using System.Collections;

namespace ExampleThreading
{
    public class ExampleEventHandle
    {
        public class MyData
        {
            int number = 0;
            public int Number { get { return number; }}
            public void Increment()
            {
                //number++; not safety from thread 
                System.Threading.Interlocked.Increment(ref number);//Atomic operation
            }
        }
        public ExampleEventHandle()
        {
            // Constructor logic here
        }
        public void ExampleEventHandling()
        {
            //Non-Signal(status) -> Set() -> Signal(status) -> Reset() -> Non-Signal(status) -> ... 
            //if Non-signal status, WaitOne() method is blocked.
            //if Signal status, WaitOne() method is unblocked.
            //EventWaitHandle is used to synchronize threads.
            //and then, parameter is false -> Non-Signal status, true -> Signal status
            EventWaitHandle ewh = new EventWaitHandle(false, EventResetMode.ManualReset); 
            Thread t = new Thread(threadFunc);
            t.IsBackground = true;
            t.Start(ewh);
            ewh.WaitOne(); //Non-Signal -> Signal, same join() method
            Console.WriteLine("Main End");
        }
        static void threadFunc(object state)
        {
            EventWaitHandle ewh = state as EventWaitHandle;

            Console.WriteLine("Thread Start");
            Thread.Sleep(1000* 4);
            Console.WriteLine("Thread End");

            //Non-Signal -> Signal  
            ewh.Set();
        }

        public void ExampleThreadpoolUsingEventHandler()
        {
            MyData data = new MyData();
            Hashtable ht1 = new Hashtable();
            ht1["data"] = data;
            ht1["evt"] = new EventWaitHandle(false, EventResetMode.ManualReset);
            ThreadPool.QueueUserWorkItem(threaFuncForEvent, ht1);

            Hashtable ht2 = new Hashtable();
            ht2["data"] = data;
            ht2["evt"] = new EventWaitHandle(false, EventResetMode.ManualReset);

            ThreadPool.QueueUserWorkItem(threaFuncForEvent, ht2);
            (ht1["evt"] as EventWaitHandle).WaitOne();
            (ht2["evt"] as EventWaitHandle).WaitOne();

            Console.WriteLine(data.Number);
        }

        static void threaFuncForEvent(object inst)
        {
            Hashtable ht = inst as Hashtable;
            MyData myData = ht["data"] as MyData;
            EventWaitHandle ewh = ht["evt"] as EventWaitHandle;

            Console.WriteLine("Thread Start");
            for (int i = 0; i < 10000; i++)
            {
                myData.Increment();
            }
            Console.WriteLine("Thread End");
            ewh.Set();
        }
    }
}