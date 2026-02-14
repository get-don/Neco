using Neco.Net.Sockets;
using System.Net;
using System.Text;

namespace EchoServerDemo;

internal class Program
{
    static async Task Main(string[] args)
    {
        var cts = new CancellationTokenSource();

        // Ctrl+C
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var listener = new NecoTcpListener();
        listener.Initialize(new IPEndPoint(IPAddress.Any, 20000));
        listener.OnAcceptAsync = clientSocket =>
        {
            var session = new Session();
            session.Initialize(clientSocket);

            Console.WriteLine($"[Accept] {session.Remote}");
            return Task.CompletedTask;
        };

        Console.WriteLine("EchoServer listening on 0.0.0.0:20000 (Ctrl+C to stop)");

        await listener.RunAsync(cts.Token);
        await listener.DisposeAsync();
    }
}

class Session : NecoTcpSession
{
    public override int OnReceive(ArraySegment<byte> buff)
    {
        var recvData = Encoding.UTF8.GetString(buff.Array!, buff.Offset, buff.Count);
        Console.WriteLine($"[Recv] {recvData}");

        var sendData = Encoding.UTF8.GetBytes(recvData);
        Write(sendData);

        return buff.Count;
    }

    public override void OnSend(int numOfBytes) { }

    public override void OnDisconnect(EndPoint endPoint)
    {
        Console.WriteLine($"[Disconnect] {endPoint}");
    }
}