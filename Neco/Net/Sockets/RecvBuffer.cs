using System.Buffers.Binary;

namespace Neco.Net.Sockets;

internal sealed class RecvBuffer
{
    private readonly byte[] _buffer;
    private int _readPos;
    private int _writePos;

    /// <summary> 버퍼 사이즈 </summary>
    public int Capacity => _buffer.Length;

    /// <summary>버퍼에 쌓인 데이터 크기</summary>
    public int DataSize => _writePos - _readPos;

    /// <summary>버퍼 뒤쪽에 남은 여유 공간</summary>
    public int FreeSize => Capacity - _writePos;

    /// <summary>현재 데이터를 써야하는 구간</summary>
    public ArraySegment<byte> WriteSegment => new(_buffer, _writePos, FreeSize);

    /// <summary>현재 데이터를 읽어야 할 구간</summary>
    public ArraySegment<byte> ReadSegment => new(_buffer, _readPos, DataSize);

    /// <summary>패킷 파싱/검사용. 비동기로 사용 금지</summary>
    public ReadOnlySpan<byte> ReadSpan => _buffer.AsSpan(_readPos, DataSize);

    /// <summary>직접 쓰기/테스트용. 비동기로 사용 금지</summary>
    public Span<byte> WriteSpan => _buffer.AsSpan(_writePos, FreeSize);

    public RecvBuffer(int bufferSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        _buffer = new byte[bufferSize];
    }

    /// <summary>
    /// size 만큼의 여유 공간이 있으면 true, 공간이 없으면 버퍼 정리 후 재확인.
    /// </summary>
    public bool EnsureFree(int size)
    {
        if (size < 0) return false;
        if (FreeSize >= size) return true;

        Clean();
        return FreeSize >= size;
    }

    /// <summary>
    /// 데이터를 읽은 만큼 버퍼의 읽기 포인터 이동
    /// </summary>
    public bool OnRead(int bytes)
    {
        if (bytes < 0 || bytes > DataSize) return false;

        _readPos += bytes;
                
        if (_readPos == _writePos)
        {
            _readPos = 0;
            _writePos = 0;
        }

        return true;
    }

    /// <summary>
    /// 데이터를 수신한 만큼 버퍼의 쓰기 포인터 이동
    /// </summary>
    public bool OnWrite(int bytes)
    {
        if (bytes < 0 || bytes > FreeSize) return false;

        _writePos += bytes;
        return true;
    }

    /// <summary>
    /// 버퍼를 정리하여 공간 확보
    /// </summary>
    public void Clean()
    {
        int dataSize = DataSize;

        if (dataSize == 0)
        {
            _readPos = 0;
            _writePos = 0;
            return;
        }

        if (_readPos == 0)
            return;

        Buffer.BlockCopy(_buffer, _readPos, _buffer, 0, dataSize);

        _readPos = 0;
        _writePos = dataSize;
    }

    #region 헤더 읽기용 유틸 메서드
    private bool TryPeek(int size, out ReadOnlySpan<byte> span)
    {
        var s = ReadSpan;
        if (s.Length < size)
        {
            span = default;
            return false;
        }

        span = s;
        return true;
    }

    public bool TryPeekUInt16LE(out ushort value)
    {
        if (!TryPeek(2, out var span))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt16LittleEndian(span);
        return true;
    }

    public bool TryPeekInt16LE(out short value)
    {
        if (!TryPeek(2, out var span))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadInt16LittleEndian(span);
        return true;
    }

    public bool TryPeekUInt32LE(out uint value)
    {
        if (!TryPeek(2, out var span))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(span);
        return true;
    }

    public bool TryPeekInt32LE(out int value)
    {
        if (!TryPeek(2, out var span))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(span);
        return true;
    }
    #endregion
}
