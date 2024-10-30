using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExampleCollection
{
    public class ExampleFileTest
    {
        public ExampleFileTest()
        {

        }
        public static void ExampleFile()
        {
            using(FileStream fs = new FileStream("test.log", FileMode.Create))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    sw.WriteLine("Hello, World!");
                    sw.WriteLine("Hello, World!");
                    sw.WriteLine("Hello, World!");
                    sw.Flush();
                }
            }
        }
    }
}
