using System;
using System.Threading;
using System.Threading.Tasks;
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Main Thread: " + Thread.CurrentThread.ManagedThreadId);
        ThreadPool.QueueUserWorkItem(
            (obj) =>
            {
                Console.WriteLine("Processing workitem");
            }, null
        );

        //Task Type, Action action
        Task task1 = new Task(() =>
        {
            Console.WriteLine("Task1 processing");
        });

        task1.Start();

        //Action<object> action, object state
        Task task2 = new Task((obj) =>
        {
            Console.WriteLine("Task2 processing");
        }, null);
        task2.Start();
        //Console.ReadLine();
    }
}