using System.Net;
using System.Net.Sockets;

namespace Neco.Net.Sockets;

public sealed class TcpConnector
{
    // Note: 연결 재시도와 같은 로직을 넣어야할지 고민
    public static async Task<Socket> ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken ct)
    {
        var socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            await socket.ConnectAsync(remoteEndPoint, ct).ConfigureAwait(false);
            return socket;
        }
        catch (Exception)
        {
            socket.Close();
            throw;
        }
    }

    public static Task<Socket> ConnectAsync(IPEndPoint remoteEndPoint) => ConnectAsync(remoteEndPoint, CancellationToken.None);
}
