namespace Niimbot.Net.Commands;

/// <summary>
/// Sequentially reads big-endian fields out of a response payload, with bounds checks. Mirrors
/// the device's own wire conventions: 16-bit values are big-endian, variable-length strings are
/// a length byte followed by that many UTF-8 bytes.
/// </summary>
public ref struct SequentialDataReader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> _data = data;
    private int _offset = 0;

    /// <summary>True if <paramref name="count"/> more bytes are available.</summary>
    public readonly bool CanRead(int count) => _offset + count <= _data.Length;

    private void Require(int count)
    {
        if (!CanRead(count))
            throw new Framing.NiimbotProtocolException("Tried to read past the end of the payload.");
    }

    public void Skip(int count)
    {
        Require(count);
        _offset += count;
    }

    public byte ReadU8()
    {
        Require(1);
        return _data[_offset++];
    }

    public int ReadU16()
    {
        Require(2);
        int value = (_data[_offset] << 8) | _data[_offset + 1];
        _offset += 2;
        return value;
    }

    public byte[] ReadBytes(int count)
    {
        Require(count);
        var slice = _data.Slice(_offset, count).ToArray();
        _offset += count;
        return slice;
    }

    /// <summary>Read a length-prefixed string (1 length byte + UTF-8 bytes).</summary>
    public string ReadVString()
    {
        var len = ReadU8();
        return System.Text.Encoding.UTF8.GetString(ReadBytes(len));
    }

    /// <summary>Bytes not yet consumed.</summary>
    public readonly int Remaining => _data.Length - _offset;
}
