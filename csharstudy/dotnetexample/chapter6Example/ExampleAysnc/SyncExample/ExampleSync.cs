using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExampleAysnc.SyncExample
{
    public class ExampleSync
    {
        public ExampleSync()
        {
        }
        static public void ExampleSyncDriver()
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
