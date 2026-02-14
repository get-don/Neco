using System.Net;
using System.Net.Sockets;

namespace Neco.Net.Sockets;

public sealed class NecoTcpConnector
{
    public static async Task<Socket> ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken ct)
    {
        var socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            await socket.ConnectAsync(remoteEndPoint, ct).ConfigureAwait(false);
            return socket;
        }
        catch (SocketException) 
        {
            socket.Close();
            throw;
        }
    }

    public static Task<Socket> ConnectAsync(IPEndPoint remoteEndPoint) => ConnectAsync(remoteEndPoint, CancellationToken.None);
}
