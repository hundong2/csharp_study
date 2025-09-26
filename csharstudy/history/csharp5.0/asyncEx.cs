using System.Text;

class Program
{
    static void Main(string[] args)
    {
        AwaitRead();
        //Console.ReadLine();
    }
    private static async Task AwaitRead()
    {
        using (FileStream fs =
            new FileStream("README.md", FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true))
        {
            byte[] buf = new byte[fs.Length];
            Console.WriteLine("Before ReadAsync: " + Thread.CurrentThread.ManagedThreadId);
            await fs.ReadAsync(buf, 0, buf.Length);
            Console.WriteLine("After ReadAsync: " + Thread.CurrentThread.ManagedThreadId);
            string text = Encoding.UTF8.GetString(buf);
            Console.WriteLine(text);
        
        }
    }
}