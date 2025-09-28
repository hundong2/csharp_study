using System;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Async Example");
            Console.WriteLine("Main Thread: " + System.Threading.Thread.CurrentThread.ManagedThreadId);
            //OldVersionExample();
            var text = await ReadFileAsync("README.md");
            Console.WriteLine(text);
        }

        public delegate string ReadAllTextDelegate(string path);
        public static void OldVersionExample()
        {
            string filePath = "README.md";

            //using delegate
            ReadAllTextDelegate func = System.IO.File.ReadAllText;
            func.BeginInvoke(filePath, actionCompleted, func);
        }
        static void actionCompleted(IAsyncResult ar)
        {
            Console.WriteLine("Action Completed");
            Console.WriteLine("Thread: " + System.Threading.Thread.CurrentThread.ManagedThreadId);
            ReadAllTextDelegate func = ar.AsyncState as ReadAllTextDelegate;
            string fileText = func.EndInvoke(ar);
            Console.WriteLine(fileText);
        }
        static Task<string> ReadFileAsync(string path)
        {
            return Task.Run<string>(() =>
            {
                return System.IO.File.ReadAllText(path);
            });
        }
    }

}