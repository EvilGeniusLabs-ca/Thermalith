using Niimbot.Net;
using Niimbot.Net.Commands;
using Niimbot.Net.Encoding;
using Niimbot.Net.Framing;
using Niimbot.Net.Profiles;
using Niimbot.Net.Tests.Fakes;
using Xunit;

namespace Niimbot.Net.Tests;

public class ClientTests
{
    private static (NiimbotClient client, FakeTransport transport, FakeB1Printer printer) NewB1()
    {
        var printer = new FakeB1Printer();
        var transport = new FakeTransport { Responder = printer.Respond };
        return (new NiimbotClient(transport), transport, printer);
    }

    [Fact]
    public async Task ConnectAsync_identifies_the_B1_and_reads_capabilities()
    {
        var (client, _, _) = NewB1();
        await using var _c = client;

        var caps = await client.ConnectAsync();

        Assert.Equal(PrinterModel.B1, caps.Model);
        Assert.Equal(4096, caps.ModelId);
        Assert.Equal(203, caps.Dpi);
        Assert.InRange(caps.MaxPrintWidthMm, 47.5, 48.5);
        Assert.True(caps.SupportsRfid);
        Assert.Equal("B1TEST01", caps.SerialNumber);
    }

    [Fact]
    public async Task GetStatusAsync_decodes_cover_paper_and_battery()
    {
        var (client, _, _) = NewB1();
        await using var _c = client;
        await client.ConnectAsync();

        var status = await client.GetStatusAsync();

        Assert.Equal(false, status.CoverOpen);
        Assert.Equal(true, status.PaperPresent);
        Assert.Equal(BatteryChargeLevel.Charge100, status.Battery);
    }

    [Fact]
    public async Task PrintAsync_runs_the_B1_sequence_and_streams_rows()
    {
        var (client, transport, printer) = NewB1();
        await using var _c = client;
        await client.ConnectAsync();

        // A 16×4 label with one dense row and the rest blank.
        var packed = new byte[2 * 4];
        packed[2] = 0xFF;
        packed[3] = 0xFF;
        var bitmap = new MonochromeBitmap(16, 4, packed);

        await client.PrintAsync(bitmap, new PrintOptions { Copies = 1 });

        // The print-phase opcodes appear in the right order.
        var sequence = transport.WrittenPackets
            .Select(p => (RequestCommandId)p.Command)
            .Where(IsPrintPhase)
            .ToList();

        Assert.Equal(
        [
            RequestCommandId.SetDensity,
            RequestCommandId.SetLabelType,
            RequestCommandId.PrintStart,
            RequestCommandId.PageStart,
            RequestCommandId.SetPageSize,
            RequestCommandId.PageEnd,
            RequestCommandId.PrintStatus,
            RequestCommandId.PrintEnd,
        ], sequence);

        // Rows were streamed (one empty-row run + one bitmap row).
        Assert.NotEmpty(printer.ReceivedRows);
        Assert.Contains(printer.ReceivedRows, p => p.Command == (byte)RequestCommandId.PrintBitmapRow);
    }

    [Fact]
    public async Task B1_Pro_uses_the_D110MV4_task_and_finishes_when_feed_stops_short()
    {
        // GitHub #21: the B1 Pro (4097) printed the page but held feed progress below 100, and the
        // old print+feed check polled forever, so the job never ended.
        var printer = new FakeB1Printer { ModelId = 4097, FeedProgress = 60 };
        var transport = new FakeTransport { Responder = printer.Respond };
        await using var client = new NiimbotClient(transport);
        await client.ConnectAsync();

        Assert.Equal(PrintTaskVersion.D110MV4, client.Profile!.PrintTaskVersion);

        var bitmap = new MonochromeBitmap(16, 4, new byte[2 * 4]);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.PrintAsync(bitmap, new PrintOptions { Copies = 1 }, ct: cts.Token);

        Assert.Equal(RequestCommandId.PrintEnd, (RequestCommandId)transport.WrittenPackets.Last().Command);
    }

    [Fact]
    public async Task B1_still_waits_for_feed_to_finish()
    {
        // The full print+feed check stays for the B1 task: feed stuck below 100 keeps it polling.
        var printer = new FakeB1Printer { FeedProgress = 60 };
        var transport = new FakeTransport { Responder = printer.Respond };
        await using var client = new NiimbotClient(transport);
        await client.ConnectAsync();

        var bitmap = new MonochromeBitmap(16, 4, new byte[2 * 4]);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.PrintAsync(bitmap, new PrintOptions { Copies = 1 }, ct: cts.Token));
    }

    [Fact]
    public async Task PrintAsync_rejects_a_bitmap_wider_than_the_printhead()
    {
        var (client, _, _) = NewB1();
        await using var _c = client;
        await client.ConnectAsync();

        // 400px > B1's 384px head.
        var tooWide = new MonochromeBitmap(400, 1, new byte[(400 + 7) / 8]);
        await Assert.ThrowsAsync<ArgumentException>(() => client.PrintAsync(tooWide));
    }

    private static bool IsPrintPhase(RequestCommandId id) => id is
        RequestCommandId.SetDensity or RequestCommandId.SetLabelType or RequestCommandId.PrintStart or
        RequestCommandId.PageStart or RequestCommandId.SetPageSize or RequestCommandId.PageEnd or
        RequestCommandId.PrintStatus or RequestCommandId.PrintEnd;
}
