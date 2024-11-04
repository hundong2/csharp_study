using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExampleAysnc.AsyncExample
{
    public class ExampleAync
    {
        public ExampleAync()
        {

        }
        class FileState
        {
            public byte[] Buffer;
            public FileStream File;
        }

        static public void ExampleAsyncTest()
        {
            FileStream fs = new FileStream(@"C:\windows\system32\drivers\etc\HOSTS", FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);

            FileState state = new FileState();
            state.Buffer = new byte[fs.Length];
            state.File = fs;

            fs.BeginRead(state.Buffer, 0, state.Buffer.Length, readCompleted, state);

            //todo anything 
            //...


            Console.ReadLine();
            fs.Close();
        }

        static void readCompleted(IAsyncResult ar)
        {
            FileState state = ar.AsyncState as FileState;
            state.File.EndRead(ar);
            string txt = Encoding.UTF8.GetString(state.Buffer);
            Console.WriteLine(txt);
        }
    }
}
