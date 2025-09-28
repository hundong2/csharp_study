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

        //Task Type 
        Task task1 = new Task(() =>
        {
            Console.WriteLine("Task1 processing");
        });

        task1.Start();

        Task task2 = new Task((obj) =>
        {
            Console.WriteLine("Task2 processing");
        });
        task2.Start();
        //Console.ReadLine();
    }
}