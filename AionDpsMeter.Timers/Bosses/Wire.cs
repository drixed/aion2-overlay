namespace AionDpsMeter.Timers.Bosses;

internal static class Wire
{
    /// <summary>LEB128 varint at <paramref name="offset"/>.</summary>
    public static bool TryVarint(ReadOnlySpan<byte> b, int offset, out long value, out int length)
    {
        value = 0;
        length = 0;
        var shift = 0;
        while (offset + length < b.Length && length < 10)
        {
            var x = b[offset + length++];
            value |= (long)(x & 0x7F) << shift;
            if ((x & 0x80) == 0) return true;
            shift += 7;
        }
        value = 0;
        length = 0;
        return false;
    }
}
