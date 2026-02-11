using System.Net;
using System.Net.Sockets;

namespace Neco.Net.Sockets;

public sealed class TcpListener : IAsyncDisposable
{
    public Func<Socket, Task>? OnAcceptAsync;

    private Socket? _listenSocket;

    private int _stopping;
    private bool IsStopping => Volatile.Read(ref _stopping) == 1;

    public void Initialize(IPEndPoint endPoint, int backlog = 100)
    {
        if (_listenSocket != null)
        {
            throw new InvalidOperationException("Already initialized.");
        }

        var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        // s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        socket.Bind(endPoint);
        socket.Listen(backlog);

        _listenSocket = socket;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var listen = _listenSocket ?? throw new InvalidOperationException("Call Initialize() first.");
        var handler = OnAcceptAsync ?? throw new InvalidOperationException("OnAcceptAsync must be set before calling RunAsync.");


        var consecutiveErrors = 0;

        while (!ct.IsCancellationRequested && !IsStopping)
        {
            Socket clientSocket;

            try
            {                
                clientSocket = await listen.AcceptAsync(ct).ConfigureAwait(false);
                consecutiveErrors = 0;
            }
            catch (OperationCanceledException)
            {
                // CancellationToken 취소로 인한 정상 종료 상황
                break;
            }
            catch (ObjectDisposedException)
            {
                // Stop()으로 listen socket 닫히는 상황
                break; 
            }
            catch (SocketException)
            {
                consecutiveErrors++;
                if (consecutiveErrors >= 10)
                {
                    // 계속 이러한 상황이 발생하면 무엇인가 잘못된 것이다.
                    Stop();
                    break;
                }

                // 예외가 연속으로 발생 시 약간 시간차를 두기 위해
                await Task.Delay(50, ct).ConfigureAwait(false);
                continue;
            }

            try
            {
                // Note: OnAcceptAsync 처리가 늦어지면 문제될 수 있음.
                await handler(clientSocket).ConfigureAwait(false);
            }
            catch
            {
                clientSocket.Dispose();
            }
        }
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopping, 1) == 1)
            return;

        _listenSocket?.Dispose();
        _listenSocket = null;
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        return ValueTask.CompletedTask;
    }
}
