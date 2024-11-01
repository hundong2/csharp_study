using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExampleCollection
{
    /// <summary>
    /// File Example
    /// </summary>
    public class ExampleFileTest
    {
        public ExampleFileTest()
        {

        }
        public static void ExampleFile()
        {
            Console.WriteLine("ExampleFile()");
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

        public static void ExampleDirectory()
        {
            Console.WriteLine("ExampleDirectory()");
            //Get Disk drives in this computer
            foreach (var local in Directory.GetLogicalDrives())
            {
                Console.WriteLine(local);
            }
            //specific folder list
            string targetPath = @"C:\";
            Console.WriteLine("Target Path(file search): " + targetPath);
            foreach (var local in Directory.GetFiles(targetPath))
            {
                Console.WriteLine(local);
            }
            Console.WriteLine("Target Path(directory search): " + targetPath);
            foreach (var local in Directory.GetDirectories(targetPath))
            {
                Console.WriteLine(local);
            }
            Console.WriteLine("Target Path(all directory, file info)");
            foreach (var local in Directory.GetFileSystemEntries(targetPath))
            {
                Console.WriteLine(local);      
            }
            Console.WriteLine("Total path child node(directory)");
            targetPath = "D:\\workspace\\private";
            foreach (var local in Directory.GetDirectories(targetPath, "*", SearchOption.AllDirectories))
            {
                Console.WriteLine(local); //local full path
            }
            Console.WriteLine("Total path child node(Files)");
            foreach (var local in Directory.GetFiles(targetPath, "*.exe", SearchOption.AllDirectories))
            {
                Console.WriteLine(local); //local full path
            }


        }
    }
}
