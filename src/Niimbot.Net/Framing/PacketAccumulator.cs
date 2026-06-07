namespace Niimbot.Net.Framing;

/// <summary>
/// Accumulates raw bytes off the transport and yields complete standard packets as they arrive.
/// The client feeds every read into <see cref="Append"/>, then drains <see cref="TryRead"/> until
/// it returns <c>null</c>. Partial trailing bytes are retained for the next read.
///
/// <para>This is the byte-to-frame seam kept above the dumb transport (spec §5.1). Only standard
/// packets are recognized; firmware-exchange CRC32 packets are out of initial scope (§12).</para>
/// </summary>
public sealed class PacketAccumulator
{
    private readonly List<byte> _buffer = new(256);

    /// <summary>Append freshly-read bytes to the internal buffer.</summary>
    public void Append(ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
            _buffer.Add(b);
    }

    /// <summary>
    /// Try to extract the next complete packet. Returns <c>null</c> when no full packet is buffered
    /// yet. Skips leading bytes that cannot begin a valid head (resynchronization after noise).
    /// </summary>
    public NiimbotPacket? TryRead()
    {
        while (true)
        {
            // Resync: drop bytes until a head marker is at index 0.
            var headIdx = FindHead();
            if (headIdx < 0)
            {
                // No head present; keep at most the last byte (could be the first 0x55 of a head).
                if (_buffer.Count > 1)
                    _buffer.RemoveRange(0, _buffer.Count - 1);
                return null;
            }

            if (headIdx > 0)
                _buffer.RemoveRange(0, headIdx);

            // Need head(2)+cmd(1)+len(1) before we know the full size.
            if (_buffer.Count < 4)
                return null;

            int dataLen = _buffer[3];
            int total = NiimbotPacket.Overhead + dataLen;

            if (_buffer.Count < total)
                return null;

            // Tail must line up; if not, this head was spurious — drop it and resync.
            if (_buffer[total - 2] != NiimbotPacket.Tail[0] || _buffer[total - 1] != NiimbotPacket.Tail[1])
            {
                _buffer.RemoveRange(0, 1);
                continue;
            }

            var raw = _buffer.GetRange(0, total).ToArray();
            _buffer.RemoveRange(0, total);
            return NiimbotPacket.FromBytes(raw);
        }
    }

    private int FindHead()
    {
        for (var i = 0; i + 1 < _buffer.Count; i++)
        {
            if (_buffer[i] == NiimbotPacket.Head[0] && _buffer[i + 1] == NiimbotPacket.Head[1])
                return i;
        }
        return -1;
    }
}
