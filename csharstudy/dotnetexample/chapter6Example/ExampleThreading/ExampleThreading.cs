namespace ExampleThreading;
using System.Threading;
public class ExampleThreading
{
    public class MultiValue
    {
        public int Value1 { get; set; } = 0;
        public int Value2 { get; set; } = 0;
    }
    public ExampleThreading()
    {
        Console.WriteLine("ExampleThreading");
    }
    public void ExampleBasicTrhead()
    {
        Thread thread = Thread.CurrentThread;
        Console.WriteLine($"Thread Name : {thread.Name}, {thread.ThreadState}");
        Console.WriteLine($"{DateTime.Now}");
        Thread.Sleep(1000); //static method 
        Console.WriteLine($"{DateTime.Now}");
    }
    public void ExampleOtherThread()
    {
        Thread thread = new Thread(() =>
        {
            Console.WriteLine("Thread1 Start");
            Thread.Sleep(5000); //after 5 seconds sub thread end
            Console.WriteLine("Thread1 End");
        });
        thread.Start();
        Console.WriteLine("Main Thread1 End");
    }

    public void ExampleOtherThreadUsingJoin()
    {
        Thread thread = new Thread(() =>
        {
            Console.WriteLine("Thread2 Start");
            Thread.Sleep(5000); //after 5 seconds sub thread end
            Console.WriteLine("Thread2 End");
        });
        thread.Start();
        thread.Join(); //wait for sub thread end
        Console.WriteLine("Main Thread2 End");
    }
    public void ExampleThreadUsingParameter()
    {
        Thread thread = new Thread(obj =>
        {
            Console.WriteLine("Thread3 Start");
            Thread.Sleep((int)obj); //after 5 seconds sub thread end
            Console.WriteLine("Thread3 End");
        });
        //changing new Thread(new PaaameterizedThreadStart(threadFunc)) is possible
        //threadFunc(object param) => void 

        thread.Start(5000);
        thread.Join(); //wait for sub thread end
        Console.WriteLine("Main Thread3 End");
    }

    public void ExampleThreadUsingMultiParam()
    {
        MultiValue multiValue = new MultiValue();
        Thread thread = new Thread(obj =>
        {
            var multiValue = obj as MultiValue;
            Console.WriteLine("Thread4 Start");
            Thread.Sleep(multiValue.Value1); //after 5 seconds sub thread end
            Console.WriteLine("Thread4 End");
        });
        //changing new Thread(new PaaameterizedThreadStart(threadFunc)) is possible
        //threadFunc(object param) => void 
        multiValue.Value1 = 5000;
        multiValue.Value2 = 10000;
        thread.Start(multiValue);
        thread.Join(); //wait for sub thread end
        Console.WriteLine("Main Thread4 End");
    }

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
    public void ExampleThreadSafetyUsingInterlocked()
    {
        MyData myData = new MyData();
        Thread[] threads = new Thread[10];
        for (int i = 0; i < 10; i++)
        {
            threads[i] = new Thread(() =>
            {
                for (int j = 0; j < 10; j++)
                {
                    myData.Increment();
                }
            });
            threads[i].Start();
        }
        for (int i = 0; i < 10; i++)
        {
            threads[i].Join();
        }
        Console.WriteLine($"Result : {myData.Number}");
    }
    public void ExampleThreadPool()
    {
        Console.WriteLine("ExampleThreadPool() Start");
        for (int i = 0; i < 5; i++)
        {
            ThreadPool.QueueUserWorkItem((obj) =>
            {
                Console.WriteLine($"Thread{i} Start");
                Thread.Sleep(i*1000); // Simulate work
                Console.WriteLine($"Thread{i} End");
            }, i);
        }
        Console.WriteLine("Main Thread5 End");
    }
}
