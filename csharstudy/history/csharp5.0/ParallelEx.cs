using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Thread Example");
            ExampleThreadSleep();
            Console.WriteLine("Using Thread Example");
            UsingThread();
            Console.WriteLine("Using Async Example");
            RunAsync();
        }
        private static async Task RunAsync()
        {
            var task3 = Method3Async();
            var task5 = Method5Async();
            var result = await Task.WhenAll(task3, task5);
            Console.WriteLine("Result total: " + (result[0] + result[1]));
        }
        private static async Task<int> Method3Async()
        {
            await Task.Delay(3000);
            return 3;
        }
        private static async Task<int> Method5Async()
        {
            await Task.Delay(5000);
            return 5;
        }
        public static void ExampleThreadSleep()
        {
            int result1 = Method(3);
            int result2 = Method(2);
            Console.WriteLine("Result total: " + (result1 + result2));
        }
        public static void UsingThread()
        {
            Dictionary<string, int> dict = new();
            Thread t3 = new Thread(() =>
            {
                Thread.Sleep(3000);
                dict.Add("t3Result", 3);
            });
            Thread t5 = new Thread(() =>
            {
                Thread.Sleep(5000);
                dict.Add("t5Result", 5);
            });

            t3.Start();
            t5.Start();
            t3.Join();
            t5.Join();
            Console.WriteLine("Result total: " + (dict["t3Result"] + dict["t5Result"]));
        }

        public static int Method(int sleepTime)
        {
            Thread.Sleep(sleepTime * 1000);
            return sleepTime;
        }
    }
}