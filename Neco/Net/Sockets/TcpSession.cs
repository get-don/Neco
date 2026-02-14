using System.Net;
using System.Net.Sockets;

namespace Neco.Net.Sockets;

public abstract class NecoTcpSession
{
    private Socket _socket = null!;

    private SocketAsyncEventArgs _recvArgs = null!;
    private SocketAsyncEventArgs _sendArgs = null!;

    private readonly RecvBuffer _recvBuffer = new(32768);
    private readonly SendBuffer _sendBuffer = new();

    private int _disconnected;
    private int _sending;
    private string _remote = "";

    private bool IsDisconnected => Volatile.Read(ref _disconnected) == 1;
    private void ResetSendingFlag() => Volatile.Write(ref _sending, 0);

    public string Remote => _remote;

    /// <summary>
    /// param: 수신한 데이터,
    /// result:
    /// 음수: 오류,
    /// 0: 데이터 처리 안함,
    /// 양수: 처리한 데이터 크기
    /// </summary>
    public abstract int OnReceive(ArraySegment<byte> buff);

    /// <summary>
    /// param: 송신한 데이터 크기
    /// </summary>
    public abstract void OnSend(int numOfBytes);

    /// <summary>
    /// param: 리모트 EndPoint
    /// </summary>
    public abstract void OnDisconnect(EndPoint endPoint);

    public void Initialize(Socket socket)
    {
        Volatile.Write(ref _disconnected, 0);
        ResetSendingFlag();

        _socket = socket;
        _remote = socket.RemoteEndPoint?.ToString() ?? "unknown";

        _recvBuffer.Reset();
        _sendBuffer.Clear();

        _sendArgs = new SocketAsyncEventArgs();
        _sendArgs.Completed += OnSendCompleted;

        _recvArgs = new SocketAsyncEventArgs();
        _recvArgs.Completed += OnReceiveCompleted;

        Receive();
    }

    public void Disconnect()
    {
        if (Interlocked.Exchange(ref _disconnected, 1) == 1)
            return;

        var endPoint = _socket.RemoteEndPoint;

        _sendBuffer.Clear();
        ResetSendingFlag();

        try { _socket.Shutdown(SocketShutdown.Both); } catch { }
        try { _socket.Close(); } catch { }

        try
        {
            _recvArgs.Completed -= OnReceiveCompleted;
            _recvArgs.Dispose();
        }
        catch { }

        try { _sendArgs.BufferList = null; } catch { }
        try
        {
            _sendArgs.Completed -= OnSendCompleted;
            _sendArgs.Dispose();
        }
        catch { }


        OnDisconnect(endPoint!);
    }

    private void Receive()
    {
        if (IsDisconnected) return;

        // Note: Clean()으로 처리할 지 EnsureFree()로 처리할지 고민
        //_recvBuffer.Clean();

        // Note: MaxPacketSize 사이즈가 있다면 좋겠다.
        if (!_recvBuffer.EnsureFree(1))
        {
            Disconnect();
            return;
        }
       
        var segment = _recvBuffer.WriteSegment;
        _recvArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);

        try
        {
            var pending = _socket.ReceiveAsync(_recvArgs);
            if (!pending)
            {
                OnReceiveCompleted(null, _recvArgs);
            }
        }
        catch (ObjectDisposedException)
        {            
        }
        catch (SocketException)
        {
            Disconnect();
        }
    }

    private void OnReceiveCompleted(object? _, SocketAsyncEventArgs e)
    {
        if (IsDisconnected)
            return;

        if (e.SocketError != SocketError.Success || e.BytesTransferred < 1)
        {
            Disconnect();
            return;
        }

        try
        {
            if (!_recvBuffer.OnWrite(e.BytesTransferred))
            {
                Disconnect();
                return;
            }

            var processLen = OnReceive(_recvBuffer.ReadSegment);
            if (processLen < 0 || _recvBuffer.DataSize < processLen)
            {
                Disconnect();
                return;
            }

            if (!_recvBuffer.OnRead(processLen))
            {
                Disconnect();
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Disconnect();
            return;
        }

        Receive();
    }

    public void Write(byte[] data) => Write(data.AsSpan());

    public void Write(ReadOnlySpan<byte> data)
    {
        if (IsDisconnected) return;

        _sendBuffer.Enqueue(data);

        if (Interlocked.CompareExchange(ref _sending, 1, 0) == 0)
        {
            Send();
        }
    }

    private void Send()
    {
        while (true)
        {
            if (IsDisconnected)
            {
                ResetSendingFlag();
                return;
            }

            _sendBuffer.BuildActiveIfEmpty();
            if (!_sendBuffer.HasActive)
            {
                ResetSendingFlag();
                return;
            }                        

            try
            {
                _sendArgs.BufferList = _sendBuffer.ActiveSegments;

                bool pending = _socket.SendAsync(_sendArgs);
                if (pending)
                    return;

                if (_sendArgs.SocketError != SocketError.Success)
                {
                    Disconnect();
                    return;
                }

                _sendArgs.BufferList = null;
                _sendBuffer.Complete(_sendArgs.BytesTransferred);
                OnSend(_sendArgs.BytesTransferred);
                continue;
            }
            catch (ObjectDisposedException)
            {
                ResetSendingFlag();
                return;
            }
            catch (SocketException)
            {
                Disconnect();
                return;
            }
        }
    }

    private void OnSendCompleted(object? sender, SocketAsyncEventArgs e)
    {
        if (IsDisconnected)
        {
            ResetSendingFlag();
            return;
        }

        if (e.SocketError != SocketError.Success)
        {
            Disconnect();
            return;
        }

        _sendArgs.BufferList = null;
        _sendBuffer.Complete(e.BytesTransferred);
        OnSend(e.BytesTransferred);

        Send();
    }
}
