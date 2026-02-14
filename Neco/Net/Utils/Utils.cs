using System;
using System.Buffers.Binary;

namespace Neco.Net.Utils;

public static class Utils
{
    private static bool TryPeek(ReadOnlySpan<byte> bytes, int size, out ReadOnlySpan<byte> span)
    {
        var s = bytes;
        if (s.Length < size)
        {
            span = default;
            return false;
        }

        span = s[..size];
        return true;
    }

    public static bool TryPeekUInt16LE(ReadOnlySpan<byte> bytes, out ushort value)
    {
        if (!TryPeek(bytes, 2, out var span))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt16LittleEndian(span);
        return true;
    }

    public static bool TryPeekInt16LE(ReadOnlySpan<byte> bytes, out short value)
    {
        if (!TryPeek(bytes, 2, out var span))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadInt16LittleEndian(span);
        return true;
    }

    public static bool TryPeekUInt32LE(ReadOnlySpan<byte> bytes, out uint value)
    {
        if (!TryPeek(bytes, 4, out var span))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(span);
        return true;
    }

    public static bool TryPeekInt32LE(ReadOnlySpan<byte> bytes, out int value)
    {
        if (!TryPeek(bytes, 4, out var span))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(span);
        return true;
    }
}
