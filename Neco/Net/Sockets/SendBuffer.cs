using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neco.Net.Sockets;
internal class SendBuffer
{
    private readonly struct Item
    {
        public readonly byte[] Array;   // 전송할 데이터 (풀에서 빌린 공간)
        public readonly int Length;     // 데이터의 실제 크기 (풀에서 공간을 빌릴 때 실제 데이터 크기와 다를 수 있기 때문에 실제 크기 보관)
        public Item(byte[] array, int length) { Array = array; Length = length; }
    }

    // 전송할 데이터 대기열
    private readonly ConcurrentQueue<Item> _queue = new();

    // 실제 전송할 데이터 목록
    private readonly List<ArraySegment<byte>> _activeSegments = [];

    // _activeSegments가 가리키는 실제 데이터 목록. 전송 완료 후 풀 반환용이므로 반환 전까지 제거되면 안된다.
    private readonly List<byte[]> _activeOwners = [];

    // 전송중인 데이터 크기
    private int _activeTotalBytes;

    /// <summary> Send에 사용할 데이터 </summary>
    public List<ArraySegment<byte>> ActiveSegments => _activeSegments;

    /// <summary> 전송 중인 데이터 크기 (몇 바이트 남았는가?) </summary>
    public int ActiveTotalBytes => _activeTotalBytes;

    /// <summary> 전송중인 데이터가 남아있는지 유무 </summary>
    public bool HasActive => _activeTotalBytes > 0;

    /// <summary>
    /// 멀티스레드에서 호출 가능. data를 풀 버퍼에 복사하여 큐에 저장.
    /// </summary>
    public void Enqueue(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0) return;

        var arr = ArrayPool<byte>.Shared.Rent(data.Length);
        data.CopyTo(arr.AsSpan(0, data.Length));

        _queue.Enqueue(new Item(arr, data.Length));
    }

    /// <summary>
    /// 전송중인 데이터가 없을 시, 대기 큐에서 데이터를 꺼내 전송할 목록을 만든다.
    /// </summary>
    public void BuildActiveIfEmpty()
    {
        // 아직 전송중인 데이터가 존재한다.
        if (HasActive) return;

        _activeSegments.Clear();
        _activeOwners.Clear();
        _activeTotalBytes = 0;

        // Note: 큐에서 데이터를 전부 꺼내야 할지 상한선을 두어야 할지는 고민이 필요한 부분이다.
        while (_queue.TryDequeue(out var item))
        {
            _activeSegments.Add(new ArraySegment<byte>(item.Array, 0, item.Length));
            _activeOwners.Add(item.Array);
            _activeTotalBytes += item.Length;
        }
    }

    /// <summary>
    /// 전송할 데이터가 남아있는지 확인 후, 완료 되었다면 풀에서 빌린 공간을 반환한다.    
    /// </summary>
    public void Complete(int bytesSent)
    {
        if (!HasActive)
            return;

        if (bytesSent < ActiveTotalBytes)
        {
            AdvanceBy(bytesSent);
            _activeTotalBytes -= bytesSent;
            return;
        }
                      
        for (var i = 0; i < _activeOwners.Count; i++)
        {
            ArrayPool<byte>.Shared.Return(_activeOwners[i]);
        }

        _activeOwners.Clear();
        _activeSegments.Clear();
        _activeTotalBytes = 0;
    }

    /// <summary>
    /// Disconnect 등으로 모두 정리
    /// </summary>
    public void Clear()
    {
        while (_queue.TryDequeue(out var item))
        {
            ArrayPool<byte>.Shared.Return(item.Array);
        }

        // 전송중인 데이터가 있는 공간들도 반환
        for (var i = 0; i < _activeOwners.Count; i++)
        { 
            ArrayPool<byte>.Shared.Return(_activeOwners[i]);
        }

        _activeOwners.Clear();
        _activeSegments.Clear();
        _activeTotalBytes = 0;
    }

    // 미전송 데이터가 있다면 남은 데이터로 리스트 조정
    private void AdvanceBy(int bytesSent)
    {
        var i = 0;
        while (i < _activeSegments.Count && bytesSent > 0)
        {
            var segment = _activeSegments[i];
            if (bytesSent >= segment.Count)
            {
                bytesSent -= segment.Count;
                i++;
                continue;
            }

            _activeSegments[i] = new ArraySegment<byte>(segment.Array!, segment.Offset + bytesSent, segment.Count - bytesSent);            
            break;
        }
                
        if (i > 0)
        {
            _activeSegments.RemoveRange(0, i);
        }
    }
}
