using Niimbot.Net.Framing;
using Xunit;

namespace Niimbot.Net.Tests;

public class FramingTests
{
    [Fact]
    public void ToBytes_matches_known_vector()
    {
        // niimbluelib documented packet: cmd 0x4A, data [0x04].
        var packet = new NiimbotPacket(0x4A, [0x04]);
        Assert.Equal("5555 4A 01 04 4F AAAA".Replace(" ", ""), Convert.ToHexString(packet.ToBytes()));
    }

    [Fact]
    public void Checksum_is_xor_of_command_length_and_data()
    {
        // 0x4A ^ 0x01 ^ 0x04 == 0x4F
        var packet = new NiimbotPacket(0x4A, [0x04]);
        Assert.Equal(0x4F, packet.Checksum);
    }

    [Fact]
    public void FromBytes_round_trips_ToBytes()
    {
        var original = new NiimbotPacket(0xF6, [0x01]);
        var parsed = NiimbotPacket.FromBytes(original.ToBytes());
        Assert.Equal(original.Command, parsed.Command);
        Assert.Equal(original.Data, parsed.Data);
    }

    [Fact]
    public void FromBytes_rejects_bad_checksum()
    {
        var bytes = new NiimbotPacket(0x4A, [0x04]).ToBytes();
        bytes[^3] ^= 0xFF; // corrupt the checksum byte
        Assert.Throws<NiimbotProtocolException>(() => NiimbotPacket.FromBytes(bytes));
    }

    [Fact]
    public void FromBytes_rejects_bad_head()
    {
        var bytes = new NiimbotPacket(0x4A, [0x04]).ToBytes();
        bytes[0] = 0x00;
        Assert.Throws<NiimbotProtocolException>(() => NiimbotPacket.FromBytes(bytes));
    }

    [Fact]
    public void Connect_request_is_prefixed_with_0x03()
    {
        // Documented device quirk: the Connect packet alone carries a leading 0x03 byte.
        var bytes = Commands.PacketGenerator.Connect().ToBytes();
        Assert.Equal(0x03, bytes[0]);
        Assert.Equal(0x55, bytes[1]);
        Assert.Equal(0x55, bytes[2]);
    }

    [Fact]
    public void Accumulator_parses_a_bundle_of_two_packets()
    {
        // niimbluelib parser example: two packets back to back.
        var bytes = Convert.FromHexString("55554A01044FAAAA5555F60101F6AAAA");
        var acc = new PacketAccumulator();
        acc.Append(bytes);

        var first = acc.TryRead();
        var second = acc.TryRead();
        var third = acc.TryRead();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Null(third);
        Assert.Equal(0x4A, first!.Command);
        Assert.Equal(0xF6, second!.Command);
    }

    [Fact]
    public void Accumulator_waits_for_a_split_packet()
    {
        var bytes = new NiimbotPacket(0x4A, [0x04]).ToBytes();
        var acc = new PacketAccumulator();

        acc.Append(bytes.AsSpan(0, 3)); // partial
        Assert.Null(acc.TryRead());

        acc.Append(bytes.AsSpan(3)); // rest
        var packet = acc.TryRead();
        Assert.NotNull(packet);
        Assert.Equal(0x4A, packet!.Command);
    }

    [Fact]
    public void Accumulator_resyncs_past_leading_noise()
    {
        var good = new NiimbotPacket(0x4A, [0x04]).ToBytes();
        var noisy = new byte[] { 0x11, 0x22, 0x33 }.Concat(good).ToArray();
        var acc = new PacketAccumulator();
        acc.Append(noisy);

        var packet = acc.TryRead();
        Assert.NotNull(packet);
        Assert.Equal(0x4A, packet!.Command);
    }
}
