using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Task Example 2");
            Task<int> task = new Task<int>(() =>
            {
                Random rand = new Random((int)DateTime.Now.Ticks);
                return rand.Next();
            });
            task.Start();
            task.Wait();
            Console.WriteLine("Random Number: " + task.Result);
        }
    }
}