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
        public static void ExampleIPAddress()
        {
            IPAddress ipAddr = IPAddress.Parse("192.168.1.10");
            IPEndPoint endpoint = new IPEndPoint(ipAddr, 9000);
        }
        public static void ExampleGetHostEntry()
        {
            Console.WriteLine("Google DNS IP");
            IPHostEntry entry = Dns.GetHostEntry("www.google.com");
            foreach (var ipAddress in entry.AddressList)
            {
                Console.WriteLine(ipAddress);
            }
            Console.WriteLine("Naver DNS IP");
            IPHostEntry entry2 = Dns.GetHostEntry("www.naver.com");
            foreach (var ipAddress in entry2.AddressList)
            {
                Console.WriteLine(ipAddress);
            }
        }
        public static void ExampleGetMyIPEntry()
        {
            Console.WriteLine("My IP Address");
            string myComputer = Dns.GetHostName();
            Console.WriteLine($"My Computer Name : {myComputer}");
            IPHostEntry entry = Dns.GetHostEntry(myComputer);
            foreach (var ipAddress in entry.AddressList)
            {
                Console.WriteLine(ipAddress.AddressFamily  + ":" + ipAddress);
            }
        }
    }
}
