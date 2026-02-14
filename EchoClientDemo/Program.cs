using Neco.Net.Sockets;
using System.Net;
using System.Text;

namespace EchoClientDemo;

internal class Program
{
    static async Task Main(string[] args)
    {
        var socket = await NecoTcpConnector.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 20000));

        var session = new Session();
        session.Initialize(socket);

        for (var i = 0; i < 5; i++) 
        {
            var msg = Encoding.UTF8.GetBytes($"Hello, world! {i}");
            session.Write(msg);

            Thread.Sleep(1000);
        }

        session.Disconnect();
    }
}

class Session : NecoTcpSession
{
    public override int OnReceive(ArraySegment<byte> buff)
    {
        var recvData = Encoding.UTF8.GetString(buff.Array!, buff.Offset, buff.Count);
        Console.WriteLine($"[Recv] {recvData}");

        return buff.Count;
    }

    public override void OnSend(int numOfBytes) { }
    public override void OnDisconnect(EndPoint endPoint) { }
}