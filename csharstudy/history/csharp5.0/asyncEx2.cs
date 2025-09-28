using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp;

class Program
{
    static async Task Main(string[] args)
    {
        Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Any, 11200));
        listener.Listen(10);
        while (true)
        {
            var client = listener.Accept();
            ProcessTcpClient(client);
            Console.WriteLine("Client connected: " + client.RemoteEndPoint);
        }
    }

    private static async void ProcessTcpClient(Socket client)
    {
        byte[] buffer = new byte[1024];
        int received = await client.ReceiveAsync(buffer); //await
        string txt = Encoding.UTF8.GetString(buffer, 0, received);
        byte[] sendBuffer = Encoding.UTF8.GetBytes("Hello " + txt);
        await client.SendAsync(sendBuffer); //await
        client.Close();
    }
}