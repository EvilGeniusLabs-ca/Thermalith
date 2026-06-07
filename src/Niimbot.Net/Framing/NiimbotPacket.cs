namespace Niimbot.Net.Framing;

/// <summary>
/// A single NIIMBOT wire packet.
///
/// <para>On-wire layout:
/// <c>[0x55 0x55] [cmd] [dataLen] [data…] [checksum] [0xAA 0xAA]</c>
/// where <c>checksum = cmd XOR dataLen XOR (each data byte)</c>.</para>
///
/// <para>The <see cref="RequestCommandId.Connect"/> request is additionally prefixed with a
/// single <c>0x03</c> byte before the head — a documented device quirk carried over from the
/// reference implementations. PENDING-HARDWARE-VERIFICATION against a real B1 capture.</para>
///
/// <para>Framing is verified against both niimbluelib and niimprint, which agree byte-for-byte.
/// See build spec §5.</para>
/// </summary>
public sealed class NiimbotPacket
{
    /// <summary>Packet head marker.</summary>
    public static readonly byte[] Head = [0x55, 0x55];

    /// <summary>Packet tail marker.</summary>
    public static readonly byte[] Tail = [0xAA, 0xAA];

    /// <summary>Fixed per-packet overhead: head(2) + cmd(1) + len(1) + checksum(1) + tail(2).</summary>
    public const int Overhead = 7;

    public NiimbotPacket(byte command, ReadOnlySpan<byte> data, ResponseCommandId[]? validResponseIds = null)
    {
        Command = command;
        Data = data.ToArray();
        ValidResponseIds = validResponseIds ?? [];
    }

    public NiimbotPacket(RequestCommandId command, ReadOnlySpan<byte> data)
        : this((byte)command, data, CommandMap.ResponsesFor(command))
    {
        OneWay = CommandMap.IsOneWay(command);
    }

    /// <summary>The opcode byte (a <see cref="RequestCommandId"/> or <see cref="ResponseCommandId"/>).</summary>
    public byte Command { get; }

    /// <summary>The payload (excludes head, opcode, length, checksum, tail).</summary>
    public byte[] Data { get; }

    /// <summary>Response opcodes that satisfy this request; empty for responses or one-way packets.</summary>
    public ResponseCommandId[] ValidResponseIds { get; set; }

    /// <summary>True when no response is expected after sending (bitmap/empty rows).</summary>
    public bool OneWay { get; set; }

    /// <summary>The XOR checksum byte: <c>cmd XOR dataLen XOR (each data byte)</c>.</summary>
    public byte Checksum
    {
        get
        {
            byte checksum = Command;
            checksum ^= (byte)Data.Length;
            foreach (var b in Data)
                checksum ^= b;
            return checksum;
        }
    }

    /// <summary>Serialize to the full on-wire byte sequence (including the Connect prefix quirk).</summary>
    public byte[] ToBytes()
    {
        var buf = new byte[Overhead + Data.Length];
        var pos = 0;

        buf[pos++] = Head[0];
        buf[pos++] = Head[1];
        buf[pos++] = Command;
        buf[pos++] = (byte)Data.Length;
        Data.CopyTo(buf, pos);
        pos += Data.Length;
        buf[pos++] = Checksum;
        buf[pos++] = Tail[0];
        buf[pos] = Tail[1];

        if (Command == (byte)RequestCommandId.Connect)
        {
            var prefixed = new byte[buf.Length + 1];
            prefixed[0] = 0x03;
            buf.CopyTo(prefixed, 1);
            return prefixed;
        }

        return buf;
    }

    /// <summary>
    /// Parse a single complete standard packet from <paramref name="buffer"/>, validating the
    /// head, tail, length, and checksum. Throws <see cref="NiimbotProtocolException"/> on any
    /// inconsistency. (Firmware CRC32 packets are out of initial scope — see spec §12.)
    /// </summary>
    public static NiimbotPacket FromBytes(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < Overhead)
            throw new NiimbotProtocolException($"Packet too small ({buffer.Length} < {Overhead}).");

        if (buffer[0] != Head[0] || buffer[1] != Head[1])
            throw new NiimbotProtocolException("Invalid packet head.");

        var cmd = buffer[2];
        var dataLen = buffer[3];

        if (buffer.Length != Overhead + dataLen)
            throw new NiimbotProtocolException($"Invalid packet size ({buffer.Length} != {Overhead + dataLen}).");

        if (buffer[^2] != Tail[0] || buffer[^1] != Tail[1])
            throw new NiimbotProtocolException("Invalid packet tail.");

        var data = buffer.Slice(4, dataLen);
        var checksum = buffer[4 + dataLen];
        var packet = new NiimbotPacket(cmd, data);

        if (packet.Checksum != checksum)
            throw new NiimbotProtocolException($"Invalid checksum (computed {packet.Checksum:X2} != {checksum:X2}).");

        return packet;
    }

    public override string ToString()
    {
        var name = Enum.IsDefined((ResponseCommandId)Command)
            ? ((ResponseCommandId)Command).ToString()
            : Enum.IsDefined((RequestCommandId)Command)
                ? ((RequestCommandId)Command).ToString()
                : $"0x{Command:X2}";
        return $"{name} [{Convert.ToHexString(Data)}]";
    }
}
