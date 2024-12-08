using System.Text;

namespace ExampleConsequence
{
    public class ExampleFileControl
    {
        class FileState
        {
            public byte[] Buffer;
            public FileStream File;
        }
        public ExampleFileControl()
        {
        }

        public static void ExampleSyncGetFileInformation()
        {
            using (FileStream fs = new FileStream(@"C:\windows\system32\drivers\etc\HOSTS", FileMode.Open, FileAccess.Read))
            {
                byte[] buf = new byte[fs.Length];
                fs.Read(buf, 0, buf.Length);

                string txt = Encoding.UTF8.GetString(buf);
                Console.WriteLine(txt);
            }
        }

        public void ExampleAsyncGetFileInformation()
        {
            FileStream fs = new FileStream(@"C:\windows\system32\drivers\etc\HOSTS", FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
            FileState state = new FileState();
            state.Buffer = new byte[fs.Length];
            state.File = fs;

            fs.BeginRead(state.Buffer, 0, state.Buffer.Length, readCompleted, state);

            Console.ReadLine();
            fs.Close();
        }

        /// <summary>
        /// Non blocking file read function
        /// </summary>
        /// <param name="ar"></param>
        public static void readCompleted(IAsyncResult ar)
        {
            FileState state = ar.AsyncState as FileState;
            state.File.EndRead(ar);

            string txt = Encoding.UTF8.GetString(state.Buffer);
            Console.WriteLine(txt);
        }

        public void ExampleUsingQueueUserWorkItem()
        {
            ThreadPool.QueueUserWorkItem(readCompletedWorkItem);

            Console.ReadLine();
        }

        public static void readCompletedWorkItem(object ar)
        {
            using (FileStream fs = new FileStream(@"C:\windows\system32\drivers\etc\HOSTS", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] buf = new byte[fs.Length];
                fs.Read(buf, 0, buf.Length);

                string txt = Encoding.UTF8.GetString(buf);
                Console.WriteLine(txt);
            }
        }
    }
}
