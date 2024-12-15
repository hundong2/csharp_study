using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BaseLibrary.BaseLibrary_IP
{
    public class ExampleIP
    {
        public ExampleIP()
        {
        }
        public static void ExampleIPParsing()
        {
            IPAddress ipAddr = IPAddress.Parse("2001:0000:85a3:0000:0000:8a2e:0370:7334");
            Console.WriteLine(ipAddr.ToString());

            IPAddress ipAddr2 = IPAddress.Parse("2001::7334");
            Console.WriteLine(ipAddr2.ToString());
            Console.WriteLine(ipAddr2.MapToIPv4().ToString());
            Console.WriteLine(ipAddr);
        }
    }
}
